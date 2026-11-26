using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.Modules.Purchasing.Domain.References;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Receipts;

public interface IGoodsReceiptDraft
{
    CompanyId CompanyId { get; }

    Guid PurchaseOrderId { get; }

    Guid WarehouseId { get; }

    DateOnly ReceiptDate { get; }

    string? Note { get; }

    IReadOnlyList<GoodsReceiptLineInput> Lines { get; }
}

internal sealed class GoodsReceiptDraftValidator : AbstractValidator<IGoodsReceiptDraft>
{
    public GoodsReceiptDraftValidator(IClock clock)
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ReceiptDate).Must(date => date <= clock.Today).WithErrorCode(PurchasingValidationCodes.DateInFuture);
        RuleFor(x => x.Note).MaximumLength(FieldLengths.Note);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= 500).WithErrorCode("Lines.TooMany");
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
/// The receipt-side rules (FR-PUR-01): lines belong to the order, the article's tracking type is respected, and no line
/// receives more than is still open on the order line. Open = ordered − received − pending, where pending is what other
/// receipts being confirmed are bringing in; so two receipts confirmed at once cannot both take the last pieces.
/// </summary>
internal static class GoodsReceiptRules
{
    public static async Task<ValidationError?> CheckAsync(
        IPurchasingDbContext db, PurchaseOrder order, Guid? receiptId, IGoodsReceiptDraft draft, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (draft.ReceiptDate < order.OrderDate)
        {
            errors.Add(nameof(draft.ReceiptDate), PurchasingValidationCodes.BeforeOrderDate);
        }

        var articleIds = order.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var pending = await PendingAsync(db, order.Id, receiptId, cancellationToken);
        var requested = new Dictionary<Guid, decimal>();
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var prefix = $"{nameof(draft.Lines)}[{index}]";
            var orderLine = order.Lines.SingleOrDefault(l => l.Id == line.OrderLineId);
            if (orderLine is null)
            {
                errors.Add($"{prefix}.{nameof(line.OrderLineId)}", PurchasingValidationCodes.OrderLineNotFound);
                continue;
            }

            requested[orderLine.Id] = requested.GetValueOrDefault(orderLine.Id) + line.Quantity;
            if (requested[orderLine.Id] > orderLine.OpenQuantity - pending.GetValueOrDefault(orderLine.Id))
            {
                errors.Add($"{prefix}.{nameof(line.Quantity)}", PurchasingValidationCodes.QuantityExceedsOpen);
            }

            CheckTracking(articles[orderLine.ArticleId].Tracking, line, prefix, errors);
        }

        return errors.Any ? errors.ToError() : null;
    }

    /// <summary>Quantities per order line in other receipts of the order that are being confirmed (not stornos).</summary>
    private static async Task<Dictionary<Guid, decimal>> PendingAsync(IPurchasingDbContext db, Guid orderId, Guid? exceptReceiptId, CancellationToken cancellationToken) =>
        await db.GoodsReceipts.AsNoTracking()
            .Where(r => r.PurchaseOrderId == orderId && r.Status == DocumentStatus.Confirming && r.ReversesReceiptId == null && r.Id != exceptReceiptId)
            .SelectMany(r => r.Lines)
            .GroupBy(l => l.OrderLineId)
            .Select(g => new { g.Key, Quantity = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(g => g.Key, g => g.Quantity, cancellationToken);

    private static void CheckTracking(TrackingType tracking, GoodsReceiptLineInput line, string prefix, ValidationErrors errors)
    {
        var hasLot = !string.IsNullOrWhiteSpace(line.LotNumber);
        if (tracking == TrackingType.Lot && !hasLot)
        {
            errors.Add($"{prefix}.{nameof(line.LotNumber)}", PurchasingValidationCodes.LotNumberRequired);
        }

        if (tracking != TrackingType.Lot && (hasLot || line.LotExpiryDate is not null))
        {
            errors.Add($"{prefix}.{nameof(line.LotNumber)}", PurchasingValidationCodes.LotNumberNotAllowed);
        }

        if (tracking == TrackingType.Serial && line.SerialNumbers.Count != line.Quantity)
        {
            errors.Add($"{prefix}.{nameof(line.SerialNumbers)}", PurchasingValidationCodes.SerialNumberCountMismatch);
        }

        if (tracking != TrackingType.Serial && line.SerialNumbers.Count > 0)
        {
            errors.Add($"{prefix}.{nameof(line.SerialNumbers)}", PurchasingValidationCodes.SerialNumbersNotAllowed);
        }
    }
}

internal static class OrderLookup
{
    /// <summary>The company's order, for a receipt or invoice draft: a validation error if missing, a conflict if not open.</summary>
    public static async Task<Result<PurchaseOrder>> OpenOrderAsync(
        IPurchasingDbContext db, CompanyId companyId, Guid orderId, bool tracked, CancellationToken cancellationToken)
    {
        var orders = tracked ? db.PurchaseOrders : db.PurchaseOrders.AsNoTracking();
        var order = await orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.CompanyId == companyId && o.Id == orderId, cancellationToken);
        if (order is null)
        {
            var errors = new ValidationErrors();
            errors.Add("PurchaseOrderId", "PurchaseOrder.NotFound");
            return errors.ToError();
        }

        return order.IsOpen ? order : PurchasingErrors.OrderNotOpen;
    }
}
