using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Warehouse.Application.Storehouses;
using Erp.Modules.Warehouse.Contracts;
using Erp.Modules.Warehouse.Contracts.Warehouses;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Warehouse.Endpoints;

internal static class WarehouseEndpoints
{
    public static void MapWarehouseLocationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/warehouses").WithTags("Warehouses");

        group.MapGet("/", async (Guid companyId, ListWarehousesHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(new ListWarehouses(new CompanyId(companyId)), cancellationToken)))
            .WithName("ListWarehouses")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.WarehousesRead))
            .Produces<IReadOnlyList<WarehouseResponse>>();

        group.MapPost("/", async (Guid companyId, WarehouseRequest request, CreateWarehouseHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateWarehouse(new CompanyId(companyId), request.Code, request.Name, request.IsActive),
                    cancellationToken)))
            .WithName("CreateWarehouse")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.WarehousesWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{warehouseId:guid}", async (
                Guid companyId,
                Guid warehouseId,
                WarehouseRequest request,
                UpdateWarehouseHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdateWarehouse(new CompanyId(companyId), warehouseId, request.Code, request.Name, request.IsActive),
                    cancellationToken)))
            .WithName("UpdateWarehouse")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.WarehousesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
