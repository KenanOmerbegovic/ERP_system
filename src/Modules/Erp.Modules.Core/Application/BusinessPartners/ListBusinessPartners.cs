using Erp.Modules.Core.Application.Abstractions;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.Modules.Core.Contracts.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Application.BusinessPartners;

/// <summary>Partners ordered by code; <see cref="Search"/> matches code or name, case-insensitively.</summary>
public sealed record ListBusinessPartners(CompanyId CompanyId, string? Search, int Page, int PageSize);

internal sealed class ListBusinessPartnersValidator : AbstractValidator<ListBusinessPartners>
{
    public ListBusinessPartnersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListBusinessPartnersHandler(ICoreDbContext db, IValidator<ListBusinessPartners> validator)
{
    public async Task<Result<PagedResponse<BusinessPartnerResponse>>> HandleAsync(
        ListBusinessPartners query,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (!await db.CompanyExistsAsync(query.CompanyId, cancellationToken))
        {
            return CoreErrors.CompanyNotFound;
        }

        var partners = db.BusinessPartners.AsNoTracking().Where(p => p.CompanyId == query.CompanyId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            partners = partners.Where(p => p.Code.ToLower().Contains(search) || p.Name.ToLower().Contains(search));
        }

        var totalCount = await partners.CountAsync(cancellationToken);
        var page = await partners
            .OrderBy(p => p.Code)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(p => new BusinessPartnerResponse(
                p.Id, p.Code, p.Name, p.TaxId, p.VatNumber, p.Address, p.PostalCode, p.City,
                p.CountryCode, p.IsCustomer, p.IsSupplier, p.IsActive))
            .ToList();
        return new PagedResponse<BusinessPartnerResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
