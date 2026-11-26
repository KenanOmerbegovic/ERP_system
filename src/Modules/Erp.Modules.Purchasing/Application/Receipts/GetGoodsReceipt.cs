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

public sealed record GetGoodsReceipt(CompanyId CompanyId, Guid ReceiptId);

public sealed class GetGoodsReceiptHandler(IPurchasingDbContext db)
{
    public async Task<Result<GoodsReceiptResponse>> HandleAsync(GetGoodsReceipt query, CancellationToken cancellationToken)
    {
        var receipt = await db.GoodsReceipts.AsNoTracking().Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.CompanyId == query.CompanyId && r.Id == query.ReceiptId, cancellationToken);
        if (receipt is null)
        {
            return PurchasingErrors.ReceiptNotFound;
        }

        var order = await db.PurchaseOrders.AsNoTracking().FirstAsync(o => o.Id == receipt.PurchaseOrderId, cancellationToken);
        var supplier = await db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == receipt.SupplierId, cancellationToken);
        var articleIds = receipt.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return new GoodsReceiptResponse(
            receipt.Id, receipt.Status.ToState(), receipt.Number, receipt.PurchaseOrderId, order.Number, receipt.SupplierId, supplier.Name,
            receipt.WarehouseId, receipt.ReceiptDate, receipt.Note, receipt.StockDocumentId, receipt.StockDocumentNumber,
            receipt.ReversesReceiptId, receipt.ReversedByReceiptId, receipt.ConfirmedAt, receipt.TotalValue,
            receipt.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new GoodsReceiptLineResponse(
                    l.Id, l.LineNumber, l.OrderLineId, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name, l.Quantity, l.UnitCost,
                    l.Value, l.InvoicedQuantity, l.LotNumber, l.LotExpiryDate, l.SerialNumbers))
                .ToList());
    }
}
