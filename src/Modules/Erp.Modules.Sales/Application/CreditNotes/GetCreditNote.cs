using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.CreditNotes;
using Erp.Modules.Sales.Contracts.Invoices;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.CreditNotes;

public sealed record GetCreditNote(CompanyId CompanyId, Guid CreditNoteId);

public sealed class GetCreditNoteHandler(ISalesDbContext db)
{
    public async Task<Result<CreditNoteResponse>> HandleAsync(GetCreditNote query, CancellationToken cancellationToken)
    {
        var creditNote = await db.CreditNotes.AsNoTracking().Include(c => c.Lines).Include(c => c.VatTotals).AsSplitQuery()
            .FirstOrDefaultAsync(c => c.CompanyId == query.CompanyId && c.Id == query.CreditNoteId, cancellationToken);
        if (creditNote is null)
        {
            return SalesErrors.CreditNoteNotFound;
        }

        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == creditNote.CustomerId, cancellationToken);
        var invoiceNumber = await db.SalesInvoices.AsNoTracking().Where(i => i.Id == creditNote.InvoiceId).Select(i => i.Number).FirstAsync(cancellationToken);
        var articleIds = creditNote.Lines.Select(l => l.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        return new CreditNoteResponse(
            creditNote.Id, creditNote.Status.ToState(), creditNote.Number, creditNote.InvoiceId, invoiceNumber, creditNote.SalesOrderId,
            creditNote.CustomerId, customer.Name, creditNote.CreditNoteDate, creditNote.WarehouseId, creditNote.Note, creditNote.StockDocumentId,
            creditNote.StockDocumentNumber, creditNote.ReversesCreditNoteId, creditNote.ReversedByCreditNoteId, creditNote.ConfirmedAt,
            creditNote.NetAmount, creditNote.VatAmount, creditNote.GrossAmount, creditNote.ReturnedCostValue,
            creditNote.VatTotals.OrderByDescending(t => t.Rate).ThenBy(t => t.VatCode, StringComparer.Ordinal)
                .Select(t => new SalesInvoiceVatTotalResponse(t.VatCode, t.Rate, t.Base, t.VatAmount))
                .ToList(),
            creditNote.Lines.OrderBy(l => l.LineNumber)
                .Select(l => new CreditNoteLineResponse(
                    l.Id, l.LineNumber, l.InvoiceLineId, l.ArticleId, articles[l.ArticleId].Code, articles[l.ArticleId].Name,
                    articles[l.ArticleId].IsService.ToArticleKind(), l.Kind.ToContract(), l.Quantity, l.UnitPrice, l.VatCode, l.VatRate, l.NetAmount,
                    l.VatAmount, l.LotNumber, l.SerialNumbers, l.ReturnedCostValue))
                .ToList());
    }
}
