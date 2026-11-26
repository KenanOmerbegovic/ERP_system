using System.Globalization;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Invoices;
using Erp.Modules.Sales.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Invoices;

/// <summary>A requested invoice line: goods by <see cref="DeliveryLineId"/>, or a service by <see cref="OrderLineId"/>.</summary>
public sealed record SalesInvoiceDraftLine(Guid? DeliveryLineId, Guid? OrderLineId, decimal Quantity);

public interface ISalesInvoiceDraft
{
    CompanyId CompanyId { get; }

    Guid SalesOrderId { get; }

    DateOnly InvoiceDate { get; }

    DateOnly DueDate { get; }

    string? Note { get; }

    IReadOnlyList<SalesInvoiceDraftLine> Lines { get; }
}

internal sealed class SalesInvoiceDraftValidator : AbstractValidator<ISalesInvoiceDraft>
{
    public SalesInvoiceDraftValidator(IClock clock)
    {
        RuleFor(x => x.SalesOrderId).NotEmpty();
        RuleFor(x => x.InvoiceDate).Must(date => date <= clock.Today).WithErrorCode(SalesValidationCodes.DateInFuture);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.InvoiceDate).WithErrorCode(SalesValidationCodes.DueBeforeInvoice);
        RuleFor(x => x.Note).MaximumLength(FieldLengths.Note);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= Limits.MaxLines).WithErrorCode("Lines.TooMany");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l)
                .Must(l => (l.DeliveryLineId is { } d && d != Guid.Empty) != (l.OrderLineId is { } o && o != Guid.Empty))
                .WithName("DeliveryLineId")
                .WithErrorCode(SalesValidationCodes.LineSourceRequired);
            line.RuleFor(l => l.Quantity).GreaterThan(0m).PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
        });
    }
}

/// <summary>
/// FR-SAL-03 rules: goods are invoiced from the order's confirmed, not reversed delivery notes, never beyond what was
/// delivered and not yet invoiced; services from their order lines, never beyond what was ordered and not yet invoiced.
/// Prices and VAT codes come from the order line. The PDV is computed by the country's calculator (FR-SAL-04).
/// </summary>
internal static class SalesInvoiceRules
{
    public static async Task<Result<IReadOnlyList<SalesInvoiceLineInput>>> ResolveAsync(
        ISalesDbContext db, SalesOrder order, ISalesInvoiceDraft draft, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (draft.InvoiceDate < order.OrderDate)
        {
            errors.Add(nameof(draft.InvoiceDate), SalesValidationCodes.BeforeOrderDate);
        }

        var deliveryLineIds = draft.Lines.Where(l => l.DeliveryLineId is not null).Select(l => l.DeliveryLineId!.Value).Distinct().ToList();
        var deliveryLines = await db.DeliveryNotes.AsNoTracking()
            .Where(d => d.SalesOrderId == order.Id && d.Status == DocumentStatus.Confirmed && d.ReversesDeliveryNoteId == null && d.ReversedByDeliveryNoteId == null)
            .SelectMany(d => d.Lines)
            .Where(l => deliveryLineIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var requested = new Dictionary<Guid, decimal>();
        var lines = new List<SalesInvoiceLineInput>();
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Lines[{index}]");
            if (line.DeliveryLineId is { } deliveryLineId)
            {
                if (!deliveryLines.TryGetValue(deliveryLineId, out var deliveryLine))
                {
                    errors.Add($"{prefix}.DeliveryLineId", SalesValidationCodes.DeliveryLineNotFound);
                    continue;
                }

                requested[deliveryLine.Id] = requested.GetValueOrDefault(deliveryLine.Id) + line.Quantity;
                if (requested[deliveryLine.Id] > deliveryLine.UninvoicedQuantity)
                {
                    errors.Add($"{prefix}.Quantity", SalesValidationCodes.QuantityExceedsUninvoiced);
                }

                var orderLine = order.Line(deliveryLine.OrderLineId);
                lines.Add(new SalesInvoiceLineInput(
                    orderLine.Id, deliveryLine.DeliveryNoteId, deliveryLine.Id, orderLine.ArticleId, false, line.Quantity, orderLine.UnitPrice, orderLine.VatCode));
            }
            else
            {
                var orderLine = order.Lines.SingleOrDefault(l => l.Id == line.OrderLineId);
                if (orderLine is null)
                {
                    errors.Add($"{prefix}.OrderLineId", SalesValidationCodes.OrderLineNotFound);
                    continue;
                }

                if (!orderLine.IsService)
                {
                    errors.Add($"{prefix}.OrderLineId", SalesValidationCodes.GoodsNeedDelivery);
                    continue;
                }

                requested[orderLine.Id] = requested.GetValueOrDefault(orderLine.Id) + line.Quantity;
                if (requested[orderLine.Id] > orderLine.InvoiceableQuantity)
                {
                    errors.Add($"{prefix}.Quantity", SalesValidationCodes.QuantityExceedsUninvoiced);
                }

                lines.Add(new SalesInvoiceLineInput(orderLine.Id, null, null, orderLine.ArticleId, true, line.Quantity, orderLine.UnitPrice, orderLine.VatCode));
            }
        }

        return errors.Any ? errors.ToError() : lines;
    }

    /// <summary>Computes and records the invoice's PDV for its date (FR-SAL-04); a code without a rate is a validation error on its lines.</summary>
    public static async Task<Result> ApplyVatAsync(IVatCalculator calculator, SalesInvoice invoice, CancellationToken cancellationToken)
    {
        var vat = await VatApplication.CalculateAsync(
            calculator, invoice.CompanyId, invoice.InvoiceDate, invoice.Lines.Select(l => (l.LineNumber, l.NetAmount, l.VatCode)), cancellationToken);
        if (vat.IsFailure)
        {
            return vat.Error;
        }

        invoice.ApplyVat(vat.Value.Lines, vat.Value.Totals);
        return Result.Success();
    }
}

internal static class InvoicedQuantities
{
    /// <summary>Adds (or, for a storno, subtracts) the invoice's quantities on its delivery lines and order lines.</summary>
    public static async Task ApplyAsync(ISalesDbContext db, SalesOrder order, SalesInvoice invoice, CancellationToken cancellationToken)
    {
        var sign = invoice.IsStorno ? -1m : 1m;
        var noteIds = invoice.Lines.Where(l => l.DeliveryNoteId is not null).Select(l => l.DeliveryNoteId!.Value).Distinct().ToList();
        var notes = await db.DeliveryNotes.Include(d => d.Lines).Where(d => noteIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);
        foreach (var line in invoice.Lines)
        {
            if (line.DeliveryNoteId is { } noteId)
            {
                notes[noteId].Line(line.DeliveryLineId!.Value).AddInvoiced(sign * line.Quantity);
            }

            order.RecordInvoiced(line.OrderLineId, sign * line.Quantity);
        }
    }
}
