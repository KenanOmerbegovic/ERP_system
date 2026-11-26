using System.Globalization;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.Modules.Sales.Domain.Orders;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Deliveries;

public interface IDeliveryNoteDraft
{
    CompanyId CompanyId { get; }

    Guid SalesOrderId { get; }

    Guid WarehouseId { get; }

    DateOnly DeliveryDate { get; }

    string? Note { get; }

    IReadOnlyList<DeliveryNoteLineInput> Lines { get; }
}

internal sealed class DeliveryNoteDraftValidator : AbstractValidator<IDeliveryNoteDraft>
{
    public DeliveryNoteDraftValidator(IClock clock)
    {
        RuleFor(x => x.SalesOrderId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.DeliveryDate).Must(date => date <= clock.Today).WithErrorCode(SalesValidationCodes.DateInFuture);
        RuleFor(x => x.Note).MaximumLength(FieldLengths.Note);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= Limits.MaxLines).WithErrorCode("Lines.TooMany");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.OrderLineId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0m).PrecisionScale(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale, ignoreTrailingZeros: true);
            line.RuleFor(l => l.LotNumber).MaximumLength(FieldLengths.LotNumber);
            line.RuleForEach(l => l.SerialNumbers).NotEmpty().MaximumLength(FieldLengths.SerialNumber);
        });
    }
}

/// <summary>
/// The delivery-side rules (FR-SAL-03): lines are goods lines of the order, the article's tracking type is respected,
/// and no line delivers more than is still open. Open = ordered − delivered − pending, where pending is what other
/// delivery notes being confirmed are taking; so two notes confirmed at once cannot both take the last pieces. Whether
/// the goods are in stock is the warehouse's check, made when the note is confirmed (FR-WH-03).
/// </summary>
internal static class DeliveryNoteRules
{
    public static async Task<ValidationError?> CheckAsync(
        ISalesDbContext db, SalesOrder order, Guid? deliveryNoteId, IDeliveryNoteDraft draft, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (draft.DeliveryDate < order.OrderDate)
        {
            errors.Add(nameof(draft.DeliveryDate), SalesValidationCodes.BeforeOrderDate);
        }

        var articleIds = order.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var pending = await PendingAsync(db, order.Id, deliveryNoteId, cancellationToken);
        var requested = new Dictionary<Guid, decimal>();
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var prefix = string.Create(CultureInfo.InvariantCulture, $"Lines[{index}]");
            var orderLine = order.Lines.SingleOrDefault(l => l.Id == line.OrderLineId);
            if (orderLine is null)
            {
                errors.Add($"{prefix}.OrderLineId", SalesValidationCodes.OrderLineNotFound);
                continue;
            }

            if (orderLine.IsService)
            {
                errors.Add($"{prefix}.OrderLineId", SalesValidationCodes.ServiceNotDelivered);
                continue;
            }

            requested[orderLine.Id] = requested.GetValueOrDefault(orderLine.Id) + line.Quantity;
            if (requested[orderLine.Id] > orderLine.OpenQuantity - pending.GetValueOrDefault(orderLine.Id))
            {
                errors.Add($"{prefix}.Quantity", SalesValidationCodes.QuantityExceedsOpen);
            }

            TrackingRules.Check(articles[orderLine.ArticleId].Tracking, line.Quantity, line.LotNumber, line.SerialNumbers, prefix, errors);
        }

        return errors.Any ? errors.ToError() : null;
    }

    /// <summary>Quantities per order line in other delivery notes of the order that are being confirmed (not stornos).</summary>
    private static async Task<Dictionary<Guid, decimal>> PendingAsync(ISalesDbContext db, Guid orderId, Guid? exceptId, CancellationToken cancellationToken) =>
        await db.DeliveryNotes.AsNoTracking()
            .Where(d => d.SalesOrderId == orderId && d.Status == DocumentStatus.Confirming && d.ReversesDeliveryNoteId == null && d.Id != exceptId)
            .SelectMany(d => d.Lines)
            .GroupBy(l => l.OrderLineId)
            .Select(g => new { g.Key, Quantity = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(g => g.Key, g => g.Quantity, cancellationToken);
}
