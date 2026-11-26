using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Orders;
using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Purchasing.Contracts.Orders;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Purchasing.Endpoints;

internal static class PurchaseOrderEndpoints
{
    public static void MapPurchaseOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/purchase-orders").WithTags("PurchaseOrders");

        group.MapGet("/", async (
                Guid companyId,
                Guid? supplierId,
                PurchaseDocumentState? status,
                bool? openOnly,
                string? search,
                int? page,
                int? pageSize,
                ListPurchaseOrdersHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListPurchaseOrders(
                        new CompanyId(companyId), supplierId, status?.ToStatus(), openOnly ?? false, search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListPurchaseOrders")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.OrdersRead))
            .Produces<PagedResponse<PurchaseOrderSummaryResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{orderId:guid}", async (Guid companyId, Guid orderId, GetPurchaseOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetPurchaseOrder(new CompanyId(companyId), orderId), cancellationToken)))
            .WithName("GetPurchaseOrder")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.OrdersRead))
            .Produces<PurchaseOrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Guid companyId, PurchaseOrderRequest request, CreatePurchaseOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreatePurchaseOrder(
                        new CompanyId(companyId), request.SupplierId, request.OrderDate, request.ExpectedDate, request.WarehouseId, request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("CreatePurchaseOrder")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.OrdersWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{orderId:guid}", async (
                Guid companyId, Guid orderId, PurchaseOrderRequest request, UpdatePurchaseOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdatePurchaseOrder(
                        new CompanyId(companyId), orderId, request.SupplierId, request.OrderDate, request.ExpectedDate, request.WarehouseId, request.Note,
                        ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("UpdatePurchaseOrder")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.OrdersWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{orderId:guid}", async (Guid companyId, Guid orderId, DeletePurchaseOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new DeletePurchaseOrder(new CompanyId(companyId), orderId), cancellationToken)))
            .WithName("DeletePurchaseOrder")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.OrdersWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{orderId:guid}/confirm", async (Guid companyId, Guid orderId, ConfirmPurchaseOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new ConfirmPurchaseOrder(new CompanyId(companyId), orderId), cancellationToken)))
            .WithName("ConfirmPurchaseOrder")
            .WithDescription("Numbers the order and makes it immutable. 422 NumberSeries.NotConfigured without a number series.")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.OrdersConfirm))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{orderId:guid}/storno", async (Guid companyId, Guid orderId, StornoPurchaseOrderHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(new StornoPurchaseOrder(new CompanyId(companyId), orderId), cancellationToken)))
            .WithName("StornoPurchaseOrder")
            .WithDescription("Reverses an order nothing was received or invoiced on (409 PurchaseOrder.HasReceipts otherwise); returns the storno's id.")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.OrdersConfirm))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static List<PurchaseOrderDraftLine> ToLines(IReadOnlyList<PurchaseOrderLineRequest>? lines) =>
        (lines ?? []).Select(line => new PurchaseOrderDraftLine(line.ArticleId, line.Quantity, line.UnitPrice, line.VatCode)).ToList();
}
