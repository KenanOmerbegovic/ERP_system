using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Purchasing.Application.Common;
using Erp.Modules.Purchasing.Application.Invoices;
using Erp.Modules.Purchasing.Application.Numbering;
using Erp.Modules.Purchasing.Contracts;
using Erp.Modules.Purchasing.Contracts.Invoices;
using Erp.Modules.Purchasing.Contracts.Numbering;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Purchasing.Endpoints;

internal static class SupplierInvoiceEndpoints
{
    public static void MapSupplierInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/supplier-invoices").WithTags("SupplierInvoices");

        group.MapGet("/", async (
                Guid companyId,
                Guid? purchaseOrderId,
                PurchaseDocumentState? status,
                string? search,
                int? page,
                int? pageSize,
                ListSupplierInvoicesHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListSupplierInvoices(new CompanyId(companyId), purchaseOrderId, status?.ToStatus(), search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListSupplierInvoices")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.InvoicesRead))
            .Produces<PagedResponse<SupplierInvoiceSummaryResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{invoiceId:guid}", async (Guid companyId, Guid invoiceId, GetSupplierInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetSupplierInvoice(new CompanyId(companyId), invoiceId), cancellationToken)))
            .WithName("GetSupplierInvoice")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.InvoicesRead))
            .Produces<SupplierInvoiceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/invoiceable-lines", async (Guid companyId, Guid purchaseOrderId, ListInvoiceableReceiptLinesHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new ListInvoiceableReceiptLines(new CompanyId(companyId), purchaseOrderId), cancellationToken)))
            .WithName("ListInvoiceableReceiptLines")
            .WithDescription("The order's confirmed receipt lines that still have quantity to invoice.")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.InvoicesRead))
            .Produces<IReadOnlyList<InvoiceableReceiptLineResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Guid companyId, SupplierInvoiceRequest request, CreateSupplierInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateSupplierInvoice(
                        new CompanyId(companyId), request.PurchaseOrderId, request.SupplierInvoiceNumber, request.InvoiceDate, request.DueDate, request.Note,
                        ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("CreateSupplierInvoice")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.InvoicesWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{invoiceId:guid}", async (
                Guid companyId, Guid invoiceId, SupplierInvoiceRequest request, UpdateSupplierInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdateSupplierInvoice(
                        new CompanyId(companyId), invoiceId, request.PurchaseOrderId, request.SupplierInvoiceNumber, request.InvoiceDate, request.DueDate,
                        request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("UpdateSupplierInvoice")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.InvoicesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{invoiceId:guid}", async (Guid companyId, Guid invoiceId, DeleteSupplierInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new DeleteSupplierInvoice(new CompanyId(companyId), invoiceId), cancellationToken)))
            .WithName("DeleteSupplierInvoice")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.InvoicesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{invoiceId:guid}/confirm", async (Guid companyId, Guid invoiceId, ConfirmSupplierInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new ConfirmSupplierInvoice(new CompanyId(companyId), invoiceId), cancellationToken)))
            .WithName("ConfirmSupplierInvoice")
            .WithDescription("Registers the invoice: numbers it, recomputes its PDV and publishes it (with price differences) for Finance.")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.InvoicesConfirm))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{invoiceId:guid}/storno", async (Guid companyId, Guid invoiceId, StornoSupplierInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(new StornoSupplierInvoice(new CompanyId(companyId), invoiceId), cancellationToken)))
            .WithName("StornoSupplierInvoice")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.InvoicesConfirm))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    public static void MapPurchasingNumberSeriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/purchasing-number-series").WithTags("PurchasingNumberSeries");

        group.MapGet("/", async (Guid companyId, ListPurchasingNumberSeriesHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(new ListPurchasingNumberSeries(new CompanyId(companyId)), cancellationToken)))
            .WithName("ListPurchasingNumberSeries")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.OrdersRead))
            .Produces<IReadOnlyList<PurchasingNumberSeriesResponse>>();

        group.MapPost("/", async (Guid companyId, PurchasingNumberSeriesRequest request, CreatePurchasingNumberSeriesHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreatePurchasingNumberSeries(new CompanyId(companyId), request.DocumentType.ToType(), request.ValidFrom, request.Prefix, request.Padding),
                    cancellationToken)))
            .WithName("CreatePurchasingNumberSeries")
            .RequireAuthorization(PermissionPolicy.For(PurchasingPermissions.SettingsWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static List<SupplierInvoiceDraftLine> ToLines(IReadOnlyList<SupplierInvoiceLineRequest>? lines) =>
        (lines ?? []).Select(line => new SupplierInvoiceDraftLine(line.ReceiptLineId, line.Quantity, line.UnitPrice, line.VatCode)).ToList();
}
