using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Deliveries;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Contracts.Deliveries;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Sales.Endpoints;

internal static class DeliveryNoteEndpoints
{
    public static void MapDeliveryNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/delivery-notes").WithTags("DeliveryNotes");

        group.MapGet("/", async (
                Guid companyId,
                Guid? salesOrderId,
                SalesDocumentState? status,
                string? search,
                int? page,
                int? pageSize,
                ListDeliveryNotesHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListDeliveryNotes(new CompanyId(companyId), salesOrderId, status?.ToStatus(), search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListDeliveryNotes")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.DeliveriesRead))
            .Produces<PagedResponse<DeliveryNoteSummaryResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{deliveryNoteId:guid}", async (Guid companyId, Guid deliveryNoteId, GetDeliveryNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetDeliveryNote(new CompanyId(companyId), deliveryNoteId), cancellationToken)))
            .WithName("GetDeliveryNote")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.DeliveriesRead))
            .Produces<DeliveryNoteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Guid companyId, DeliveryNoteRequest request, CreateDeliveryNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateDeliveryNote(new CompanyId(companyId), request.SalesOrderId, request.WarehouseId, request.DeliveryDate, request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("CreateDeliveryNote")
            .WithDescription("Saves a draft delivery note against a confirmed order; stock leaves when it is confirmed.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.DeliveriesWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{deliveryNoteId:guid}", async (
                Guid companyId, Guid deliveryNoteId, DeliveryNoteRequest request, UpdateDeliveryNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdateDeliveryNote(
                        new CompanyId(companyId), deliveryNoteId, request.SalesOrderId, request.WarehouseId, request.DeliveryDate, request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("UpdateDeliveryNote")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.DeliveriesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{deliveryNoteId:guid}", async (Guid companyId, Guid deliveryNoteId, DeleteDeliveryNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new DeleteDeliveryNote(new CompanyId(companyId), deliveryNoteId), cancellationToken)))
            .WithName("DeleteDeliveryNote")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.DeliveriesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{deliveryNoteId:guid}/confirm", async (Guid companyId, Guid deliveryNoteId, ConfirmDeliveryNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new ConfirmDeliveryNote(new CompanyId(companyId), deliveryNoteId), cancellationToken)))
            .WithName("ConfirmDeliveryNote")
            .WithDescription("Issues the goods from the warehouse and numbers the note. 409 Stock.Insufficient (with shortages) if there is not "
                + "enough stock: the note stays a draft (FR-WH-03). Confirming a note left in the Confirming state completes it; stock moves only once.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.DeliveriesConfirm))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<StockProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{deliveryNoteId:guid}/storno", async (Guid companyId, Guid deliveryNoteId, StornoDeliveryNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(new StornoDeliveryNote(new CompanyId(companyId), deliveryNoteId), cancellationToken)))
            .WithName("StornoDeliveryNote")
            .WithDescription("Brings the goods back into the warehouse with a storno note dated today; returns its id. "
                + "409 DeliveryNote.Invoiced if the note is invoiced.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.DeliveriesConfirm))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<StockProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static List<DeliveryNoteLineInput> ToLines(IReadOnlyList<DeliveryNoteLineRequest>? lines) =>
        (lines ?? []).Select(line => new DeliveryNoteLineInput(line.OrderLineId, line.Quantity, line.LotNumber, line.SerialNumbers ?? [])).ToList();
}
