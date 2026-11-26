using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.CreditNotes;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Contracts.CreditNotes;
using Erp.Modules.Warehouse.Contracts.StockDocuments;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Sales.Endpoints;

internal static class CreditNoteEndpoints
{
    public static void MapCreditNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/credit-notes").WithTags("CreditNotes");

        group.MapGet("/", async (
                Guid companyId,
                Guid? invoiceId,
                SalesDocumentState? status,
                string? search,
                int? page,
                int? pageSize,
                ListCreditNotesHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListCreditNotes(new CompanyId(companyId), invoiceId, status?.ToStatus(), search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListCreditNotes")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.CreditNotesRead))
            .Produces<PagedResponse<CreditNoteSummaryResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{creditNoteId:guid}", async (Guid companyId, Guid creditNoteId, GetCreditNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetCreditNote(new CompanyId(companyId), creditNoteId), cancellationToken)))
            .WithName("GetCreditNote")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.CreditNotesRead))
            .Produces<CreditNoteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Guid companyId, CreditNoteRequest request, CreateCreditNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateCreditNote(new CompanyId(companyId), request.InvoiceId, request.CreditNoteDate, request.WarehouseId, request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("CreateCreditNote")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.CreditNotesWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{creditNoteId:guid}", async (
                Guid companyId, Guid creditNoteId, CreditNoteRequest request, UpdateCreditNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdateCreditNote(
                        new CompanyId(companyId), creditNoteId, request.InvoiceId, request.CreditNoteDate, request.WarehouseId, request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("UpdateCreditNote")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.CreditNotesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{creditNoteId:guid}", async (Guid companyId, Guid creditNoteId, DeleteCreditNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new DeleteCreditNote(new CompanyId(companyId), creditNoteId), cancellationToken)))
            .WithName("DeleteCreditNote")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.CreditNotesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{creditNoteId:guid}/confirm", async (Guid companyId, Guid creditNoteId, ConfirmCreditNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new ConfirmCreditNote(new CompanyId(companyId), creditNoteId), cancellationToken)))
            .WithName("ConfirmCreditNote")
            .WithDescription("Numbers the credit note and credits the invoice; returned goods come back into the warehouse at their delivery cost. "
                + "Warehouse refusals are passed on and the credit note is a draft again. Confirming one left in the Confirming state completes it.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.CreditNotesConfirm))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<StockProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{creditNoteId:guid}/storno", async (Guid companyId, Guid creditNoteId, StornoCreditNoteHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(new StornoCreditNote(new CompanyId(companyId), creditNoteId), cancellationToken)))
            .WithName("StornoCreditNote")
            .WithDescription("Reverses the credit note (returned goods leave the warehouse again); returns the storno's id. "
                + "409 Stock.Insufficient (with shortages) if the returned goods are gone.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.CreditNotesConfirm))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<StockProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static List<CreditNoteDraftLine> ToLines(IReadOnlyList<CreditNoteLineRequest>? lines) =>
        (lines ?? []).Select(line => new CreditNoteDraftLine(line.InvoiceLineId, line.Kind.ToKind(), line.Quantity, line.NetAmount, line.SerialNumbers ?? [])).ToList();
}
