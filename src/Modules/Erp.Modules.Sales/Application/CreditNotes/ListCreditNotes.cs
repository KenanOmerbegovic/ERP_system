using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Sales.Application.Abstractions;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Contracts.CreditNotes;
using Erp.Modules.Sales.Domain;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.CreditNotes;

/// <summary>Newest first. <see cref="Search"/> matches the credit note's or the invoice's number, or the customer's name.</summary>
public sealed record ListCreditNotes(CompanyId CompanyId, Guid? InvoiceId, DocumentStatus? Status, string? Search, int Page, int PageSize);

internal sealed class ListCreditNotesValidator : AbstractValidator<ListCreditNotes>
{
    public ListCreditNotesValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListCreditNotesHandler(ISalesDbContext db, IValidator<ListCreditNotes> validator)
{
    public async Task<Result<PagedResponse<CreditNoteSummaryResponse>>> HandleAsync(ListCreditNotes query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var creditNotes =
            from creditNote in db.CreditNotes.AsNoTracking()
            join invoice in db.SalesInvoices.AsNoTracking() on creditNote.InvoiceId equals invoice.Id
            join customer in db.Customers.AsNoTracking() on creditNote.CustomerId equals customer.Id
            where creditNote.CompanyId == query.CompanyId
            select new { creditNote, InvoiceNumber = invoice.Number, CustomerName = customer.Name };
        if (query.InvoiceId is { } invoiceId)
        {
            creditNotes = creditNotes.Where(c => c.creditNote.InvoiceId == invoiceId);
        }

        if (query.Status is { } status)
        {
            creditNotes = creditNotes.Where(c => c.creditNote.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            creditNotes = creditNotes.Where(c =>
                (c.creditNote.Number != null && c.creditNote.Number.ToLower().Contains(search))
                || (c.InvoiceNumber != null && c.InvoiceNumber.ToLower().Contains(search))
                || c.CustomerName.ToLower().Contains(search));
        }

        var totalCount = await creditNotes.CountAsync(cancellationToken);
        var page = await creditNotes
            .OrderByDescending(c => c.creditNote.CreditNoteDate)
            .ThenByDescending(c => c.creditNote.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var items = page
            .Select(c => new CreditNoteSummaryResponse(
                c.creditNote.Id, c.creditNote.Status.ToState(), c.creditNote.Number, c.creditNote.InvoiceId, c.InvoiceNumber, c.CustomerName,
                c.creditNote.CreditNoteDate, c.creditNote.ReversesCreditNoteId, c.creditNote.ReversedByCreditNoteId, c.creditNote.GrossAmount))
            .ToList();
        return new PagedResponse<CreditNoteSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
