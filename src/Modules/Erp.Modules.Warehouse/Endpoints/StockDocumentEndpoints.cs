using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Warehouse.Application.Common;
using Erp.Modules.Warehouse.Application.StockDocuments;
using Erp.Modules.Warehouse.Contracts;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Warehouse.Endpoints;

internal static class StockDocumentEndpoints
{
    public static void MapStockDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/stock-documents").WithTags("StockDocuments");

        group.MapGet("/", async (
                Guid companyId,
                StockDocumentKind? type,
                StockDocumentState? status,
                Guid? warehouseId,
                DateOnly? from,
                DateOnly? to,
                string? search,
                int? page,
                int? pageSize,
                ListStockDocumentsHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListStockDocuments(
                        new CompanyId(companyId),
                        type is { } kind ? ToType(kind) : null,
                        status is { } state ? ToStatus(state) : null,
                        warehouseId,
                        from,
                        to,
                        search,
                        page ?? 1,
                        pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListStockDocuments")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockDocumentsRead))
            .Produces<PagedResponse<StockDocumentSummaryResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{documentId:guid}", async (Guid companyId, Guid documentId, GetStockDocumentHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetStockDocument(new CompanyId(companyId), documentId), cancellationToken)))
            .WithName("GetStockDocument")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockDocumentsRead))
            .Produces<StockDocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Guid companyId, StockDocumentRequest request, CreateStockDocumentHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateStockDocument(
                        new CompanyId(companyId),
                        ToType(request.Type),
                        request.DocumentDate,
                        request.SourceWarehouseId,
                        request.TargetWarehouseId,
                        request.Note,
                        ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("CreateStockDocument")
            .WithDescription("Saves a draft. Stock moves only when it is confirmed.")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockDocumentsWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{documentId:guid}", async (
                Guid companyId,
                Guid documentId,
                StockDocumentRequest request,
                UpdateStockDocumentHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdateStockDocument(
                        new CompanyId(companyId),
                        documentId,
                        ToType(request.Type),
                        request.DocumentDate,
                        request.SourceWarehouseId,
                        request.TargetWarehouseId,
                        request.Note,
                        ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("UpdateStockDocument")
            .WithDescription("Replaces a draft's content. A confirmed document cannot be changed (409 StockDocument.NotDraft).")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockDocumentsWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{documentId:guid}", async (Guid companyId, Guid documentId, DeleteStockDocumentHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new DeleteStockDocument(new CompanyId(companyId), documentId), cancellationToken)))
            .WithName("DeleteStockDocument")
            .WithDescription("Deletes a draft. A confirmed document is reversed with a storno instead.")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockDocumentsWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{documentId:guid}/confirm", async (Guid companyId, Guid documentId, ConfirmStockDocumentHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new ConfirmStockDocument(new CompanyId(companyId), documentId), cancellationToken)))
            .WithName("ConfirmStockDocument")
            .WithDescription("Moves the stock and numbers the document; it is immutable afterwards. "
                + "409 Stock.Insufficient (with shortages) if it would take more than is in stock; nothing is saved then. "
                + "422 NumberSeries.NotConfigured if no number series covers its type and date.")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockDocumentsConfirm))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<StockProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{documentId:guid}/storno", async (Guid companyId, Guid documentId, StornoStockDocumentHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(new StornoStockDocument(new CompanyId(companyId), documentId), cancellationToken)))
            .WithName("StornoStockDocument")
            .WithDescription("Reverses a confirmed document with a new, confirmed storno dated today; returns the storno's id. "
                + "409 Stock.Insufficient if the goods are no longer in stock.")
            .RequireAuthorization(PermissionPolicy.For(WarehousePermissions.StockDocumentsConfirm))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<StockProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static List<StockLineInput> ToLines(IReadOnlyList<StockDocumentLineRequest>? lines) =>
        (lines ?? [])
            .Select(line => new StockLineInput(
                line.ArticleId,
                line.Quantity,
                line.UnitCost,
                line.LotNumber,
                line.LotExpiryDate,
                line.SerialNumbers ?? []))
            .ToList();

    // Undefined values pass through unchanged so the validator reports them.
    internal static StockDocumentType ToType(StockDocumentKind kind) => kind switch
    {
        StockDocumentKind.Receipt => StockDocumentType.Receipt,
        StockDocumentKind.Issue => StockDocumentType.Issue,
        StockDocumentKind.Transfer => StockDocumentType.Transfer,
        _ => (StockDocumentType)(int)kind,
    };

    private static StockDocumentStatus ToStatus(StockDocumentState state) => state switch
    {
        StockDocumentState.Draft => StockDocumentStatus.Draft,
        StockDocumentState.Confirmed => StockDocumentStatus.Confirmed,
        _ => (StockDocumentStatus)(int)state,
    };
}
