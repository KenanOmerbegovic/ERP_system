using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Deliveries;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Deliveries;

public sealed record GetDeliveryNote(CompanyId CompanyId, Guid DeliveryNoteId);

public sealed class GetDeliveryNoteHandler(ISalesDbContext db)
{
    public async Task<Result<DeliveryNoteResponse>> HandleAsync(GetDeliveryNote query, CancellationToken cancellationToken)
    {
        var note = await db.DeliveryNotes.AsNoTracking().Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.CompanyId == query.CompanyId && d.Id == query.DeliveryNoteId, cancellationToken);
        if (note is null)
        {
            return SalesErrors.DeliveryNoteNotFound;
        }

        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == note.CustomerId, cancellationToken);
        var orderNumber = await db.SalesOrders.AsNoTracking().Where(o => o.Id == note.SalesOrderId).Select(o => o.Number).FirstAsync(cancellationToken);
        var articleIds = note.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return new DeliveryNoteResponse(
            note.Id, note.Status.ToState(), note.Number, note.SalesOrderId, orderNumber, note.CustomerId, customer.Name, note.WarehouseId,
            note.DeliveryDate, note.Note, note.StockDocumentId, note.StockDocumentNumber, note.ReversesDeliveryNoteId, note.ReversedByDeliveryNoteId,
            note.ConfirmedAt, note.CostValue,
            note.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new DeliveryNoteLineResponse(
                    l.Id, l.LineNumber, l.OrderLineId, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name, l.Quantity, l.CostValue,
                    l.InvoicedQuantity, l.LotNumber, l.SerialNumbers))
                .ToList());
    }
}
