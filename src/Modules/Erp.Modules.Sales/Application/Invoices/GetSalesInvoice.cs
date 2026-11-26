using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.Invoices;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Invoices;

public sealed record GetSalesInvoice(CompanyId CompanyId, Guid InvoiceId);

public sealed class GetSalesInvoiceHandler(ISalesDbContext db)
{
    public async Task<Result<SalesInvoiceResponse>> HandleAsync(GetSalesInvoice query, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices.AsNoTracking().Include(i => i.Lines).Include(i => i.VatTotals).AsSplitQuery()
            .FirstOrDefaultAsync(i => i.CompanyId == query.CompanyId && i.Id == query.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return SalesErrors.InvoiceNotFound;
        }

        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == invoice.CustomerId, cancellationToken);
        var orderNumber = await db.SalesOrders.AsNoTracking().Where(o => o.Id == invoice.SalesOrderId).Select(o => o.Number).FirstAsync(cancellationToken);
        var articleIds = invoice.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return new SalesInvoiceResponse(
            invoice.Id, invoice.Status.ToState(), invoice.Number, invoice.SalesOrderId, orderNumber, invoice.CustomerId, customer.Name, customer.TaxId,
            customer.VatNumber, invoice.InvoiceDate, invoice.DueDate, invoice.Note, invoice.ReversesInvoiceId, invoice.ReversedByInvoiceId,
            invoice.ConfirmedAt, invoice.NetAmount, invoice.VatAmount, invoice.GrossAmount, invoice.CreditedAmount,
            invoice.VatTotals.OrderByDescending(t => t.Rate).ThenBy(t => t.VatCode, StringComparer.Ordinal)
                .Select(t => new SalesInvoiceVatTotalResponse(t.VatCode, t.Rate, t.Base, t.VatAmount))
                .ToList(),
            invoice.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new SalesInvoiceLineResponse(
                    l.Id, l.LineNumber, l.OrderLineId, l.DeliveryNoteId, l.DeliveryLineId, l.ArticleId, articles[l.ArticleId].Code,
                    articles[l.ArticleId].Name, l.IsService.ToArticleKind(), l.Quantity, l.UnitPrice, l.VatCode, l.VatRate, l.NetAmount, l.VatAmount,
                    l.CreditedAmount, l.ReturnedQuantity))
                .ToList());
    }
}
