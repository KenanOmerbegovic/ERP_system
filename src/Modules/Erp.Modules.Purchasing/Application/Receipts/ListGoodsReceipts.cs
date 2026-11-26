using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Contracts.Receipts;
using Erp.Modules.Purchasing.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Receipts;

/// <summary>Newest first; <see cref="Search"/> matches the receipt or order number.</summary>
public sealed record ListGoodsReceipts(CompanyId CompanyId, Guid? PurchaseOrderId, DocumentStatus? Status, string? Search, int Page, int PageSize);

internal sealed class ListGoodsReceiptsValidator : AbstractValidator<ListGoodsReceipts>
{
    public ListGoodsReceiptsValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListGoodsReceiptsHandler(IPurchasingDbContext db, IValidator<ListGoodsReceipts> validator)
{
    public async Task<Result<PagedResponse<GoodsReceiptSummaryResponse>>> HandleAsync(ListGoodsReceipts query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var receipts =
            from receipt in db.GoodsReceipts.AsNoTracking()
            join order in db.PurchaseOrders.AsNoTracking() on receipt.PurchaseOrderId equals order.Id
            join supplier in db.Suppliers.AsNoTracking() on receipt.SupplierId equals supplier.Id
            where receipt.CompanyId == query.CompanyId
            select new { receipt, OrderNumber = order.Number, SupplierName = supplier.Name };
        if (query.PurchaseOrderId is { } orderId)
        {
            receipts = receipts.Where(r => r.receipt.PurchaseOrderId == orderId);
        }

        if (query.Status is { } status)
        {
            receipts = receipts.Where(r => r.receipt.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            receipts = receipts.Where(r =>
                (r.receipt.Number != null && r.receipt.Number.ToLower().Contains(search)) || (r.OrderNumber != null && r.OrderNumber.ToLower().Contains(search)));
        }

        var totalCount = await receipts.CountAsync(cancellationToken);
        var page = await receipts
            .OrderByDescending(r => r.receipt.ReceiptDate)
            .ThenByDescending(r => r.receipt.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new { r.receipt, r.OrderNumber, r.SupplierName, Value = r.receipt.Lines.Sum(l => l.Value) })
            .ToListAsync(cancellationToken);
        var items = page
            .Select(r => new GoodsReceiptSummaryResponse(
                r.receipt.Id, r.receipt.Status.ToState(), r.receipt.Number, r.receipt.PurchaseOrderId, r.OrderNumber, r.SupplierName,
                r.receipt.WarehouseId, r.receipt.ReceiptDate, r.receipt.ReversesReceiptId, r.receipt.ReversedByReceiptId,
                r.receipt.Status == DocumentStatus.Confirmed ? r.Value ?? 0m : null))
            .ToList();
        return new PagedResponse<GoodsReceiptSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
