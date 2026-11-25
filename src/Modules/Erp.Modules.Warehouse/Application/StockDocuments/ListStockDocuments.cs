using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Warehouse.Application.Abstractions;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Warehouse.Application.StockDocuments;

/// <summary>
/// Newest first. <see cref="WarehouseId"/> matches source or target; <see cref="Search"/> matches the number.
/// Dates are inclusive.
/// </summary>
public sealed record ListStockDocuments(
    CompanyId CompanyId,
    StockDocumentType? Type,
    StockDocumentStatus? Status,
    Guid? WarehouseId,
    DateOnly? From,
    DateOnly? To,
    string? Search,
    int Page,
    int PageSize);

internal sealed class ListStockDocumentsValidator : AbstractValidator<ListStockDocuments>
{
    public ListStockDocumentsValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListStockDocumentsHandler(IWarehouseDbContext db, IValidator<ListStockDocuments> validator)
{
    public async Task<Result<PagedResponse<StockDocumentSummaryResponse>>> HandleAsync(ListStockDocuments query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var documents = db.StockDocuments.AsNoTracking().Where(d => d.CompanyId == query.CompanyId);
        if (query.Type is { } type)
        {
            documents = documents.Where(d => d.Type == type);
        }

        if (query.Status is { } status)
        {
            documents = documents.Where(d => d.Status == status);
        }

        if (query.WarehouseId is { } warehouseId)
        {
            documents = documents.Where(d => d.SourceStorehouseId == warehouseId || d.TargetStorehouseId == warehouseId);
        }

        if (query.From is { } from)
        {
            documents = documents.Where(d => d.DocumentDate >= from);
        }

        if (query.To is { } to)
        {
            documents = documents.Where(d => d.DocumentDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            documents = documents.Where(d => d.Number != null && d.Number.ToLower().Contains(search));
        }

        var totalCount = await documents.CountAsync(cancellationToken);
        var page = await documents
            .OrderByDescending(d => d.DocumentDate)
            .ThenByDescending(d => d.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(d => new
            {
                d.Id,
                d.Type,
                d.Status,
                d.Number,
                d.DocumentDate,
                d.SourceStorehouseId,
                d.TargetStorehouseId,
                d.ReversesDocumentId,
                d.ReversedByDocumentId,
                LineCount = d.Lines.Count,
                Value = d.Lines.Sum(line => line.Value),
            })
            .ToListAsync(cancellationToken);

        var items = page
            .Select(d => new StockDocumentSummaryResponse(
                d.Id,
                d.Type.ToKind(),
                d.Status.ToState(),
                d.Number,
                d.DocumentDate,
                d.SourceStorehouseId,
                d.TargetStorehouseId,
                d.ReversesDocumentId,
                d.ReversedByDocumentId,
                d.LineCount,
                d.Status == StockDocumentStatus.Confirmed ? d.Value ?? 0m : null))
            .ToList();
        return new PagedResponse<StockDocumentSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
