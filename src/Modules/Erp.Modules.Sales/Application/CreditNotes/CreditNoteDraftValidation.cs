using System.Globalization;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Invoices;
using Erp.Modules.Sales.Domain.References;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.CreditNotes;

/// <summary>
/// A requested credit note line. A return names a <see cref="Quantity"/> (and, for serial-tracked goods, which delivered
/// serial numbers come back); a value reduction names a <see cref="NetAmount"/> in KM.
/// </summary>
public sealed record CreditNoteDraftLine(Guid InvoiceLineId, CreditKind Kind, decimal? Quantity, decimal? NetAmount, IReadOnlyList<string> SerialNumbers);

public interface ICreditNoteDraft
{
    CompanyId CompanyId { get; }

    Guid InvoiceId { get; }

    DateOnly CreditNoteDate { get; }

    Guid? WarehouseId { get; }

    string? Note { get; }

    IReadOnlyList<CreditNoteDraftLine> Lines { get; }
}

internal sealed class CreditNoteDraftValidator : AbstractValidator<ICreditNoteDraft>
{
    public CreditNoteDraftValidator(IClock clock)
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.CreditNoteDate).Must(date => date <= clock.Today).WithErrorCode(SalesValidationCodes.DateInFuture);
        RuleFor(x => x.WarehouseId).Must(id => id is { } warehouse && warehouse != Guid.Empty).When(x => x.Lines.Any(l => l.Kind == CreditKind.Return))
            .WithErrorCode(SalesValidationCodes.WarehouseRequired);
        RuleFor(x => x.Note).MaximumLength(FieldLengths.Note);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= Limits.MaxLines).WithErrorCode("Lines.TooMany");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.InvoiceLineId).NotEmpty();
            line.RuleFor(l => l.Kind).IsInEnum();
            line.When(l => l.Kind == CreditKind.Return, () =>
            {
                line.RuleFor(l => l.Quantity).NotNull().GreaterThan(0m)
                    .PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
                line.RuleFor(l => l.NetAmount).Null();
                line.RuleForEach(l => l.SerialNumbers).NotEmpty().MaximumLength(FieldLengths.SerialNumber);
            });
            line.When(l => l.Kind == CreditKind.ValueReduction, () =>
            {
                line.RuleFor(l => l.NetAmount).NotNull().GreaterThan(0m)
                    .PrecisionScale(FieldLengths.ValuePrecision, FieldLengths.ValueScale, ignoreTrailingZeros: true);
                line.RuleFor(l => l.Quantity).Must(q => q is null or 0m);
                line.RuleFor(l => l.SerialNumbers).Empty();
            });
        });
    }
}

/// <summary>
/// FR-SAL-05 rules: a credit note corrects an open invoice and never takes off more than is left on a line. A line's
/// creditable amount is its net amount minus what confirmed credit notes took and what credit notes being confirmed are
/// taking; its returnable quantity likewise. Returned goods come back at the delivery's cost per unit, with the
/// delivered LOT, and only serial numbers that were delivered on that line.
/// </summary>
internal static class CreditNoteRules
{
    public static async Task<Result<IReadOnlyList<CreditNoteLineInput>>> ResolveAsync(
        ISalesDbContext db, SalesInvoice invoice, Guid? creditNoteId, ICreditNoteDraft draft, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (draft.CreditNoteDate < invoice.InvoiceDate)
        {
            errors.Add(nameof(draft.CreditNoteDate), SalesValidationCodes.BeforeInvoiceDate);
        }

        var deliveryLineIds = invoice.Lines.Where(l => l.DeliveryLineId is not null).Select(l => l.DeliveryLineId!.Value).ToList();
        var deliveryLines = await db.DeliveryNotes.AsNoTracking().SelectMany(d => d.Lines).Where(l => deliveryLineIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);
        var articleIds = invoice.Lines.Select(l => l.ArticleId).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var pending = await PendingAsync(db, invoice.Id, creditNoteId, cancellationToken);

        var requestedAmount = new Dictionary<Guid, decimal>();
        var requestedQuantity = new Dictionary<Guid, decimal>();
        var lines = new List<CreditNoteLineInput>();
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Lines[{index}]");
            var invoiceLine = invoice.Lines.SingleOrDefault(l => l.Id == line.InvoiceLineId);
            if (invoiceLine is null)
            {
                errors.Add($"{prefix}.InvoiceLineId", SalesValidationCodes.InvoiceLineNotFound);
                continue;
            }

            var (pendingAmount, pendingQuantity) = pending.GetValueOrDefault(invoiceLine.Id);
            if (line.Kind == CreditKind.Return)
            {
                if (invoiceLine.IsService)
                {
                    errors.Add($"{prefix}.InvoiceLineId", SalesValidationCodes.ServiceNotReturned);
                    continue;
                }

                var quantity = line.Quantity ?? 0m;
                var net = Amounts.LineAmount(quantity, invoiceLine.UnitPrice);
                requestedQuantity[invoiceLine.Id] = requestedQuantity.GetValueOrDefault(invoiceLine.Id) + quantity;
                if (requestedQuantity[invoiceLine.Id] > invoiceLine.ReturnableQuantity - pendingQuantity)
                {
                    errors.Add($"{prefix}.Quantity", SalesValidationCodes.QuantityExceedsReturnable);
                }

                requestedAmount[invoiceLine.Id] = requestedAmount.GetValueOrDefault(invoiceLine.Id) + net;
                if (requestedAmount[invoiceLine.Id] > invoiceLine.CreditableAmount - pendingAmount)
                {
                    errors.Add($"{prefix}.Quantity", SalesValidationCodes.AmountExceedsCreditable);
                }

                var deliveryLine = deliveryLines[invoiceLine.DeliveryLineId!.Value];
                var tracking = articles[invoiceLine.ArticleId].Tracking;
                TrackingRules.Check(tracking, quantity, deliveryLine.LotNumber, line.SerialNumbers, prefix, errors);
                if (line.SerialNumbers.Any(serial => !deliveryLine.SerialNumbers.Contains(serial.Trim(), StringComparer.Ordinal)))
                {
                    errors.Add($"{prefix}.SerialNumbers", SalesValidationCodes.SerialNumberNotDelivered);
                }

                lines.Add(new CreditNoteLineInput(
                    invoiceLine.Id, invoiceLine.ArticleId, CreditKind.Return, quantity, invoiceLine.UnitPrice, net, invoiceLine.VatCode,
                    deliveryLine.LotNumber, line.SerialNumbers, deliveryLine.UnitCost));
            }
            else
            {
                var net = Amounts.Km(line.NetAmount ?? 0m);
                requestedAmount[invoiceLine.Id] = requestedAmount.GetValueOrDefault(invoiceLine.Id) + net;
                if (requestedAmount[invoiceLine.Id] > invoiceLine.CreditableAmount - pendingAmount)
                {
                    errors.Add($"{prefix}.NetAmount", SalesValidationCodes.AmountExceedsCreditable);
                }

                lines.Add(new CreditNoteLineInput(
                    invoiceLine.Id, invoiceLine.ArticleId, CreditKind.ValueReduction, 0m, 0m, net, invoiceLine.VatCode, null, [], 0m));
            }
        }

        return errors.Any ? errors.ToError() : lines;
    }

    /// <summary>Computes the credit note's PDV at the rates of the invoice's date.</summary>
    public static async Task<Result> ApplyVatAsync(IVatCalculator calculator, CreditNote creditNote, CancellationToken cancellationToken)
    {
        var vat = await VatApplication.CalculateAsync(
            calculator, creditNote.CompanyId, creditNote.VatDate, creditNote.Lines.Select(l => (l.LineNumber, l.NetAmount, l.VatCode)), cancellationToken);
        if (vat.IsFailure)
        {
            return vat.Error;
        }

        creditNote.ApplyVat(vat.Value.Lines, vat.Value.Totals);
        return Result.Success();
    }

    /// <summary>Net amounts and returned quantities per invoice line in other credit notes of the invoice being confirmed.</summary>
    private static async Task<Dictionary<Guid, (decimal Amount, decimal Quantity)>> PendingAsync(
        ISalesDbContext db, Guid invoiceId, Guid? exceptId, CancellationToken cancellationToken)
    {
        var pending = await db.CreditNotes.AsNoTracking()
            .Where(c => c.InvoiceId == invoiceId && c.Status == DocumentStatus.Confirming && c.ReversesCreditNoteId == null && c.Id != exceptId)
            .SelectMany(c => c.Lines)
            .GroupBy(l => l.InvoiceLineId)
            .Select(g => new { g.Key, Amount = g.Sum(l => l.NetAmount), Quantity = g.Sum(l => l.Quantity) })
            .ToListAsync(cancellationToken);
        return pending.ToDictionary(p => p.Key, p => (p.Amount, p.Quantity));
    }
}

internal static class CreditedAmounts
{
    /// <summary>Takes (or, for a storno, gives back) the credit note's amounts and returns on its invoice's lines.</summary>
    public static void Apply(SalesInvoice invoice, CreditNote creditNote)
    {
        var sign = creditNote.IsStorno ? -1m : 1m;
        foreach (var line in creditNote.Lines)
        {
            invoice.Line(line.InvoiceLineId).AddCredit(sign * line.NetAmount, sign * line.Quantity);
        }
    }
}

internal static class InvoiceLookup
{
    /// <summary>The company's invoice, for a credit note draft: a validation error if missing, a conflict if not open.</summary>
    public static async Task<Result<SalesInvoice>> OpenInvoiceAsync(
        ISalesDbContext db, CompanyId companyId, Guid invoiceId, bool tracked, CancellationToken cancellationToken)
    {
        var invoices = tracked ? db.SalesInvoices : db.SalesInvoices.AsNoTracking();
        var invoice = await invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.CompanyId == companyId && i.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            var errors = new ValidationErrors();
            errors.Add("InvoiceId", "SalesInvoice.NotFound");
            return errors.ToError();
        }

        return invoice.IsOpen ? invoice : SalesErrors.InvoiceNotOpen;
    }
}
