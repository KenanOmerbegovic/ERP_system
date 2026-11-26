using System.Globalization;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Invoices;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Invoices;

/// <summary>A requested invoice line; <see cref="VatCode"/> null means the order line's VAT code.</summary>
public sealed record SupplierInvoiceDraftLine(Guid ReceiptLineId, decimal Quantity, decimal UnitPrice, string? VatCode);

public interface ISupplierInvoiceDraft
{
    CompanyId CompanyId { get; }

    Guid PurchaseOrderId { get; }

    string SupplierInvoiceNumber { get; }

    DateOnly InvoiceDate { get; }

    DateOnly DueDate { get; }

    string? Note { get; }

    IReadOnlyList<SupplierInvoiceDraftLine> Lines { get; }
}

internal sealed class SupplierInvoiceDraftValidator : AbstractValidator<ISupplierInvoiceDraft>
{
    public SupplierInvoiceDraftValidator(IClock clock)
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.SupplierInvoiceNumber).NotEmpty().MaximumLength(FieldLengths.DocumentNumber);
        RuleFor(x => x.InvoiceDate).Must(date => date <= clock.Today).WithErrorCode(PurchasingValidationCodes.DateInFuture);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.InvoiceDate).WithErrorCode(PurchasingValidationCodes.DueBeforeInvoice);
        RuleFor(x => x.Note).MaximumLength(FieldLengths.Note);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= 500).WithErrorCode("Lines.TooMany");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ReceiptLineId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0m).PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0m).PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
            line.RuleFor(l => l.VatCode).MaximumLength(FieldLengths.VatCode);
        });
    }
}

/// <summary>
/// FR-PUR-02 rules: an invoice lines up with the order's confirmed, not reversed receipts; no receipt line is invoiced
/// beyond what was received; the supplier's invoice number is registered once per supplier; and the PDV is computed by
/// the country's calculator for the invoice date.
/// </summary>
internal static class SupplierInvoiceRules
{
    public static async Task<Result<IReadOnlyList<SupplierInvoiceLineInput>>> ResolveAsync(
        IPurchasingDbContext db, PurchaseOrder order, Guid? invoiceId, ISupplierInvoiceDraft draft, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        var number = draft.SupplierInvoiceNumber.Trim();
        if (await db.SupplierInvoices.AnyAsync(
                i => i.CompanyId == draft.CompanyId && i.SupplierId == order.SupplierId && i.SupplierInvoiceNumber == number
                    && i.Status == DocumentStatus.Confirmed && i.ReversesInvoiceId == null && i.ReversedByInvoiceId == null && i.Id != invoiceId,
                cancellationToken))
        {
            return PurchasingErrors.DuplicateSupplierInvoiceNumber;
        }

        var receiptLineIds = draft.Lines.Select(l => l.ReceiptLineId).Distinct().ToList();
        var receiptLines = await db.GoodsReceipts.AsNoTracking()
            .Where(r => r.PurchaseOrderId == order.Id && r.Status == DocumentStatus.Confirmed && r.ReversesReceiptId == null && r.ReversedByReceiptId == null)
            .SelectMany(r => r.Lines)
            .Where(l => receiptLineIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var requested = new Dictionary<Guid, decimal>();
        var lines = new List<SupplierInvoiceLineInput>();
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var prefix = $"{nameof(draft.Lines)}[{index}]";
            if (!receiptLines.TryGetValue(line.ReceiptLineId, out var receiptLine))
            {
                errors.Add($"{prefix}.{nameof(line.ReceiptLineId)}", PurchasingValidationCodes.ReceiptLineNotFound);
                continue;
            }

            requested[receiptLine.Id] = requested.GetValueOrDefault(receiptLine.Id) + line.Quantity;
            if (requested[receiptLine.Id] > receiptLine.UninvoicedQuantity)
            {
                errors.Add($"{prefix}.{nameof(line.Quantity)}", PurchasingValidationCodes.QuantityExceedsUninvoiced);
            }

            var vatCode = string.IsNullOrWhiteSpace(line.VatCode) ? order.Line(receiptLine.OrderLineId).VatCode : line.VatCode.Trim();
            lines.Add(new SupplierInvoiceLineInput(
                receiptLine.ReceiptId, receiptLine.Id, receiptLine.OrderLineId, receiptLine.ArticleId, receiptLine.UnitCost, line.Quantity, line.UnitPrice, vatCode));
        }

        return errors.Any ? errors.ToError() : lines;
    }

    /// <summary>Computes and records the invoice's PDV; a VAT code without a rate on the invoice date is a validation error.</summary>
    public static async Task<Result> ApplyVatAsync(IVatCalculator calculator, SupplierInvoice invoice, CancellationToken cancellationToken)
    {
        var request = new VatCalculationRequest(
            invoice.CompanyId,
            invoice.InvoiceDate,
            invoice.Lines.Select(l => new VatLineInput(l.LineNumber.ToString(CultureInfo.InvariantCulture), l.NetAmount, l.VatCode)).ToList());
        var calculation = await calculator.CalculateAsync(request, cancellationToken);
        if (calculation.IsFailure)
        {
            if (calculation.Error is not VatRateNotFoundError notFound)
            {
                return calculation.Error;
            }

            var errors = new ValidationErrors();
            foreach (var key in notFound.LineKeys)
            {
                errors.Add($"Lines[{int.Parse(key, CultureInfo.InvariantCulture) - 1}].VatCode", PurchasingValidationCodes.VatRateNotFound);
            }

            return errors.ToError();
        }

        invoice.ApplyVat(
            calculation.Value.Lines.ToDictionary(
                l => int.Parse(l.Key, CultureInfo.InvariantCulture),
                l => new VatAmounts(l.VatCode, l.Rate, l.NetAmount, l.VatAmount)),
            calculation.Value.Totals.Select(t => new VatAmounts(t.VatCode, t.Rate, t.Base, t.VatAmount)).ToList());
        return Result.Success();
    }
}
