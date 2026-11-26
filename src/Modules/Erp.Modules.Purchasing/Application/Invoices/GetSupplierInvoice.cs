using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Purchasing.Application.Abstractions;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Contracts.Invoices;
using Erp.Modules.Purchasing.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Invoices;

public sealed record GetSupplierInvoice(CompanyId CompanyId, Guid InvoiceId);

public sealed class GetSupplierInvoiceHandler(IPurchasingDbContext db)
{
    public async Task<Result<SupplierInvoiceResponse>> HandleAsync(GetSupplierInvoice query, CancellationToken cancellationToken)
    {
        var invoice = await db.SupplierInvoices.AsNoTracking().Include(i => i.Lines).Include(i => i.VatTotals)
            .FirstOrDefaultAsync(i => i.CompanyId == query.CompanyId && i.Id == query.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return PurchasingErrors.InvoiceNotFound;
        }

        var order = await db.PurchaseOrders.AsNoTracking().FirstAsync(o => o.Id == invoice.PurchaseOrderId, cancellationToken);
        var supplier = await db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == invoice.SupplierId, cancellationToken);
        var articleIds = invoice.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return new SupplierInvoiceResponse(
            invoice.Id, invoice.Status.ToState(), invoice.Number, invoice.PurchaseOrderId, order.Number, invoice.SupplierId, supplier.Name,
            invoice.SupplierInvoiceNumber, invoice.InvoiceDate, invoice.DueDate, invoice.Note, invoice.ReversesInvoiceId, invoice.ReversedByInvoiceId,
            invoice.ConfirmedAt, invoice.NetAmount, invoice.VatAmount, invoice.GrossAmount, invoice.PriceDifference,
            invoice.VatTotals.OrderByDescending(t => t.Rate).ThenBy(t => t.VatCode, StringComparer.Ordinal)
                .Select(t => new SupplierInvoiceVatTotalResponse(t.VatCode, t.Rate, t.Base, t.VatAmount)).ToList(),
            invoice.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new SupplierInvoiceLineResponse(
                    l.Id, l.LineNumber, l.ReceiptId, l.ReceiptLineId, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name,
                    l.Quantity, l.UnitPrice, l.ReceivedUnitCost, l.VatCode, l.NetAmount, l.VatAmount, l.ReceivedValue, l.PriceDifference))
                .ToList());
    }
}
