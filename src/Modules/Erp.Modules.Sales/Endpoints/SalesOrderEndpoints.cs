using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Orders;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Contracts.Orders;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Sales.Endpoints;

internal static class SalesOrderEndpoints
{
    public static void MapSalesOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/sales-orders").WithTags("SalesOrders");

        group.MapGet("/", async (
                Guid companyId,
                Guid? customerId,
                SalesDocumentState? status,
                bool? openOnly,
                string? search,
                int? page,
                int? pageSize,
                ListSalesOrdersHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListSalesOrders(
                        new CompanyId(companyId), customerId, status?.ToStatus(), openOnly ?? false, search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListSalesOrders")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OrdersRead))
            .Produces<PagedResponse<SalesOrderSummaryResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{orderId:guid}", async (Guid companyId, Guid orderId, GetSalesOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetSalesOrder(new CompanyId(companyId), orderId), cancellationToken)))
            .WithName("GetSalesOrder")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OrdersRead))
            .Produces<SalesOrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Guid companyId, SalesOrderRequest request, CreateSalesOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateSalesOrder(new CompanyId(companyId), request.CustomerId, request.OrderDate, request.WarehouseId, request.Note, SalesLines.From(request.Lines)),
                    cancellationToken)))
            .WithName("CreateSalesOrder")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OrdersWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{orderId:guid}", async (
                Guid companyId, Guid orderId, SalesOrderRequest request, UpdateSalesOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdateSalesOrder(
                        new CompanyId(companyId), orderId, request.CustomerId, request.OrderDate, request.WarehouseId, request.Note, SalesLines.From(request.Lines)),
                    cancellationToken)))
            .WithName("UpdateSalesOrder")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OrdersWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{orderId:guid}", async (Guid companyId, Guid orderId, DeleteSalesOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new DeleteSalesOrder(new CompanyId(companyId), orderId), cancellationToken)))
            .WithName("DeleteSalesOrder")
            .WithDescription("Deletes a draft order; for an order made from an offer, the offer is open (sent) again.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OrdersWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{orderId:guid}/confirm", async (Guid companyId, Guid orderId, ConfirmSalesOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new ConfirmSalesOrder(new CompanyId(companyId), orderId), cancellationToken)))
            .WithName("ConfirmSalesOrder")
            .WithDescription("Numbers the order and makes it immutable. 422 NumberSeries.NotConfigured without a number series.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OrdersConfirm))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{orderId:guid}/storno", async (Guid companyId, Guid orderId, StornoSalesOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(new StornoSalesOrder(new CompanyId(companyId), orderId), cancellationToken)))
            .WithName("StornoSalesOrder")
            .WithDescription("Reverses an order nothing was delivered or invoiced on; returns the storno's id. 409 SalesOrder.HasDeliveries otherwise.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OrdersConfirm))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
