using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Receipts;
using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Purchasing.Contracts.Receipts;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Purchasing.Endpoints;

internal static class GoodsReceiptEndpoints
{
    public static void MapGoodsReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/goods-receipts").WithTags("GoodsReceipts");

        group.MapGet("/", async (
                Guid companyId,
                Guid? purchaseOrderId,
                PurchaseDocumentState? status,
                string? search,
                int? page,
                int? pageSize,
                ListGoodsReceiptsHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListGoodsReceipts(new CompanyId(companyId), purchaseOrderId, status?.ToStatus(), search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListGoodsReceipts")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.ReceiptsRead))
            .Produces<PagedResponse<GoodsReceiptSummaryResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{receiptId:guid}", async (Guid companyId, Guid receiptId, GetGoodsReceiptHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetGoodsReceipt(new CompanyId(companyId), receiptId), cancellationToken)))
            .WithName("GetGoodsReceipt")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.ReceiptsRead))
            .Produces<GoodsReceiptResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Guid companyId, GoodsReceiptRequest request, CreateGoodsReceiptHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateGoodsReceipt(new CompanyId(companyId), request.PurchaseOrderId, request.WarehouseId, request.ReceiptDate, request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("CreateGoodsReceipt")
            .WithDescription("Saves a draft receipt against a confirmed order; stock moves when it is confirmed.")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.ReceiptsWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{receiptId:guid}", async (
                Guid companyId, Guid receiptId, GoodsReceiptRequest request, UpdateGoodsReceiptHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdateGoodsReceipt(
                        new CompanyId(companyId), receiptId, request.PurchaseOrderId, request.WarehouseId, request.ReceiptDate, request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("UpdateGoodsReceipt")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.ReceiptsWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{receiptId:guid}", async (Guid companyId, Guid receiptId, DeleteGoodsReceiptHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new DeleteGoodsReceipt(new CompanyId(companyId), receiptId), cancellationToken)))
            .WithName("DeleteGoodsReceipt")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.ReceiptsWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{receiptId:guid}/confirm", async (Guid companyId, Guid receiptId, ConfirmGoodsReceiptHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new ConfirmGoodsReceipt(new CompanyId(companyId), receiptId), cancellationToken)))
            .WithName("ConfirmGoodsReceipt")
            .WithDescription("Receives the goods into the warehouse at the order price and numbers the receipt. Confirming a receipt left "
                + "in the Confirming state completes it; stock moves only once. Warehouse refusals (e.g. 409 SerialNumber.AlreadyInStock, "
                + "400 per line) are passed on and the receipt is a draft again.")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.ReceiptsConfirm))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<StockProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{receiptId:guid}/storno", async (Guid companyId, Guid receiptId, StornoGoodsReceiptHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(new StornoGoodsReceipt(new CompanyId(companyId), receiptId), cancellationToken)))
            .WithName("StornoGoodsReceipt")
            .WithDescription("Takes the goods back out of the warehouse with a storno receipt dated today; returns its id. "
                + "409 Stock.Insufficient (with shortages) if they are gone, 409 GoodsReceipt.Invoiced if the receipt is invoiced.")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.ReceiptsConfirm))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<StockProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static List<GoodsReceiptLineInput> ToLines(IReadOnlyList<GoodsReceiptLineRequest>? lines) =>
        (lines ?? [])
            .Select(line => new GoodsReceiptLineInput(line.OrderLineId, line.Quantity, line.LotNumber, line.LotExpiryDate, line.SerialNumbers ?? []))
            .ToList();
}
