using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Application.Numbering;
using Erp.Modules.Warehouse.Application.Stock;
using Erp.Modules.Warehouse.Contracts;
using Erp.Modules.Warehouse.Contracts.Numbering;
using Erp.Modules.Warehouse.Contracts.Stock;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Warehouse.Endpoints;

internal static class StockEndpoints
{
    public static void MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/stock").WithTags("Stock");

        group.MapGet("/", async (
                Guid companyId,
                Guid? warehouseId,
                Guid? articleId,
                string? search,
                bool? includeEmpty,
                int? page,
                int? pageSize,
                ListStockBalancesHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListStockBalances(
                        new CompanyId(companyId), warehouseId, articleId, search, includeEmpty ?? false, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListStockBalances")
            .WithDescription("Current stock per article and warehouse, with weighted-average cost, value (KM) and LOTs.")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockRead))
            .Produces<PagedResponse<StockBalanceResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/serial-numbers", async (
                Guid companyId,
                Guid? articleId,
                Guid? warehouseId,
                bool? inStockOnly,
                string? search,
                int? page,
                int? pageSize,
                ListSerialNumbersHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListSerialNumbers(
                        new CompanyId(companyId), articleId, warehouseId, inStockOnly ?? false, search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListSerialNumbers")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockRead))
            .Produces<PagedResponse<SerialNumberResponse>>()
            .ProducesValidationProblem();
    }

    public static void MapNumberSeriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/warehouse-number-series").WithTags("WarehouseNumberSeries");

        group.MapGet("/", async (Guid companyId, ListNumberSeriesHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(new ListNumberSeries(new CompanyId(companyId)), cancellationToken)))
            .WithName("ListWarehouseNumberSeries")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockDocumentsRead))
            .Produces<IReadOnlyList<NumberSeriesResponse>>();

        group.MapPost("/", async (Guid companyId, NumberSeriesRequest request, CreateNumberSeriesHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateNumberSeries(
                        new CompanyId(companyId), StockDocumentEndpoints.ToType(request.DocumentType), request.ValidFrom, request.Prefix, request.Padding),
                    cancellationToken)))
            .WithName("CreateWarehouseNumberSeries")
            .WithDescription("Series are not edited: a new format starts with a new series from a later date.")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.SettingsWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
