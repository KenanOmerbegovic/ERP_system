using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.Stock;

/// <summary>
/// Current stock per article and warehouse (FR-WH-04), with weighted-average cost, value and the quantity per LOT.
/// Ordered by article code, then warehouse code. Empty balances are left out unless <see cref="IncludeEmpty"/>.
/// <see cref="Search"/> matches article code or name.
/// </summary>
public sealed record ListStockBalances(
    CompanyId CompanyId,
    Guid? WarehouseId,
    Guid? ArticleId,
    string? Search,
    bool IncludeEmpty,
    int Page,
    int PageSize);

internal sealed class ListStockBalancesValidator : AbstractValidator<ListStockBalances>
{
    public ListStockBalancesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListStockBalancesHandler(IWarehouseDbContext db, IValidator<ListStockBalances> validator)
{
    /// <summary>Average costs are shown to 4 decimals, like unit costs.</summary>
    public const int AverageCostDecimals = 4;

    public async Task<Result<PagedResponse<StockBalanceResponse>>> HandleAsync(ListStockBalances query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var balances =
            from item in db.StockItems.AsNoTracking()
            join storehouse in db.Storehouses.AsNoTracking() on item.StorehouseId equals storehouse.Id
            join article in db.Articles.AsNoTracking() on item.ArticleId equals article.Id
            where item.CompanyId == query.CompanyId
            select new { item, StorehouseCode = storehouse.Code, ArticleCode = article.Code, ArticleName = article.Name, article.Tracking };
        if (!query.IncludeEmpty)
        {
            balances = balances.Where(b => b.item.Quantity != 0m);
        }

        if (query.WarehouseId is { } warehouseId)
        {
            balances = balances.Where(b => b.item.StorehouseId == warehouseId);
        }

        if (query.ArticleId is { } articleId)
        {
            balances = balances.Where(b => b.item.ArticleId == articleId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            balances = balances.Where(b => b.ArticleCode.ToLower().Contains(search) || b.ArticleName.ToLower().Contains(search));
        }

        var totalCount = await balances.CountAsync(cancellationToken);
        var page = await balances
            .OrderBy(b => b.ArticleCode)
            .ThenBy(b => b.StorehouseCode)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var storehouseIds = page.Select(b => b.item.StorehouseId).Distinct().ToList();
        var articleIds = page.Select(b => b.item.ArticleId).Distinct().ToList();
        var lots = await (
                from balance in db.LotBalances.AsNoTracking()
                join lot in db.Lots.AsNoTracking() on balance.LotId equals lot.Id
                where balance.CompanyId == query.CompanyId
                    && balance.Quantity != 0m
                    && storehouseIds.Contains(balance.StorehouseId)
                    && articleIds.Contains(balance.ArticleId)
                orderby lot.ExpiryDate, lot.Number
                select new { balance.StorehouseId, balance.ArticleId, lot.Number, lot.ExpiryDate, balance.Quantity })
            .ToListAsync(cancellationToken);
        var lotsByItem = lots.ToLookup(lot => (lot.StorehouseId, lot.ArticleId));

        var items = page
            .Select(b => new StockBalanceResponse(
                b.item.StorehouseId,
                b.StorehouseCode,
                b.item.ArticleId,
                b.ArticleCode,
                b.ArticleName,
                b.Tracking.ToKind(),
                b.item.Quantity,
                Math.Round(b.item.AverageCost, AverageCostDecimals, MidpointRounding.AwayFromZero),
                b.item.Value,
                lotsByItem[(b.item.StorehouseId, b.item.ArticleId)]
                    .Select(lot => new StockLotResponse(lot.Number, lot.ExpiryDate, lot.Quantity))
                    .ToList()))
            .ToList();
        return new PagedResponse<StockBalanceResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
