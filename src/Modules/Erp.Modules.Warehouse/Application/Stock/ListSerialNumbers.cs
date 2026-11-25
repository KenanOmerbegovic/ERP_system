using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.Stock;

/// <summary>Serial numbers ever received (FR-WH-02), by article code and number. <see cref="Search"/> matches the number.</summary>
public sealed record ListSerialNumbers(
    CompanyId CompanyId,
    Guid? ArticleId,
    Guid? WarehouseId,
    bool InStockOnly,
    string? Search,
    int Page,
    int PageSize);

internal sealed class ListSerialNumbersValidator : AbstractValidator<ListSerialNumbers>
{
    public ListSerialNumbersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListSerialNumbersHandler(IWarehouseDbContext db, IValidator<ListSerialNumbers> validator)
{
    public async Task<Result<PagedResponse<SerialNumberResponse>>> HandleAsync(ListSerialNumbers query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        // A serial number row survives only a committed confirmation, which always received the piece at some point.
        var serials =
            from serial in db.SerialNumbers.AsNoTracking()
            join article in db.Articles.AsNoTracking() on serial.ArticleId equals article.Id
            where serial.CompanyId == query.CompanyId
            select new { serial, ArticleCode = article.Code };
        if (query.ArticleId is { } articleId)
        {
            serials = serials.Where(s => s.serial.ArticleId == articleId);
        }

        if (query.WarehouseId is { } warehouseId)
        {
            serials = serials.Where(s => s.serial.StorehouseId == warehouseId);
        }

        if (query.InStockOnly)
        {
            serials = serials.Where(s => s.serial.StorehouseId != null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            serials = serials.Where(s => s.serial.Number.ToLower().Contains(search));
        }

        var totalCount = await serials.CountAsync(cancellationToken);
        var page = await serials
            .OrderBy(s => s.ArticleCode)
            .ThenBy(s => s.serial.Number)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var items = page
            .Select(s => new SerialNumberResponse(s.serial.ArticleId, s.ArticleCode, s.serial.Number, s.serial.InStock, s.serial.StorehouseId))
            .ToList();
        return new PagedResponse<SerialNumberResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
