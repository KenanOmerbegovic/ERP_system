using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Invoices;
using Erp.Modules.Sales.Application.Numbering;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Contracts.Invoices;
using Erp.Modules.Sales.Contracts.Numbering;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Sales.Endpoints;

internal static class SalesInvoiceEndpoints
{
    public static void MapSalesInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/sales-invoices").WithTags("SalesInvoices");

        group.MapGet("/", async (
                Guid companyId,
                Guid? salesOrderId,
                Guid? customerId,
                SalesDocumentState? status,
                bool? openOnly,
                string? search,
                int? page,
                int? pageSize,
                ListSalesInvoicesHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListSalesInvoices(
                        new CompanyId(companyId), salesOrderId, customerId, status?.ToStatus(), openOnly ?? false, search, page ?? 1,
                        pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListSalesInvoices")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.InvoicesRead))
            .Produces<PagedResponse<SalesInvoiceSummaryResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{invoiceId:guid}", async (Guid companyId, Guid invoiceId, GetSalesInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetSalesInvoice(new CompanyId(companyId), invoiceId), cancellationToken)))
            .WithName("GetSalesInvoice")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.InvoicesRead))
            .Produces<SalesInvoiceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/invoiceable-lines", async (Guid companyId, Guid salesOrderId, ListInvoiceableLinesHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new ListInvoiceableLines(new CompanyId(companyId), salesOrderId), cancellationToken)))
            .WithName("ListInvoiceableLines")
            .WithDescription("Confirmed delivery lines and service order lines of the order that still have quantity to invoice.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.InvoicesRead))
            .Produces<IReadOnlyList<InvoiceableLineResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Guid companyId, SalesInvoiceRequest request, CreateSalesInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateSalesInvoice(new CompanyId(companyId), request.SalesOrderId, request.InvoiceDate, request.DueDate, request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("CreateSalesInvoice")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.InvoicesWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{invoiceId:guid}", async (
                Guid companyId, Guid invoiceId, SalesInvoiceRequest request, UpdateSalesInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdateSalesInvoice(
                        new CompanyId(companyId), invoiceId, request.SalesOrderId, request.InvoiceDate, request.DueDate, request.Note, ToLines(request.Lines)),
                    cancellationToken)))
            .WithName("UpdateSalesInvoice")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.InvoicesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{invoiceId:guid}", async (Guid companyId, Guid invoiceId, DeleteSalesInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new DeleteSalesInvoice(new CompanyId(companyId), invoiceId), cancellationToken)))
            .WithName("DeleteSalesInvoice")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.InvoicesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{invoiceId:guid}/confirm", async (Guid companyId, Guid invoiceId, ConfirmSalesInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new ConfirmSalesInvoice(new CompanyId(companyId), invoiceId), cancellationToken)))
            .WithName("ConfirmSalesInvoice")
            .WithDescription("Issues the invoice: numbers it, recomputes its PDV and publishes it for Finance. It is immutable afterwards.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.InvoicesConfirm))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{invoiceId:guid}/storno", async (Guid companyId, Guid invoiceId, StornoSalesInvoiceHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(new StornoSalesInvoice(new CompanyId(companyId), invoiceId), cancellationToken)))
            .WithName("StornoSalesInvoice")
            .WithDescription("Reverses the whole invoice; returns the storno's id. 409 SalesInvoice.Credited once a credit note was made on it.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.InvoicesConfirm))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    public static void MapSalesNumberSeriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/sales-number-series").WithTags("SalesNumberSeries");

        group.MapGet("/", async (Guid companyId, ListSalesNumberSeriesHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(new ListSalesNumberSeries(new CompanyId(companyId)), cancellationToken)))
            .WithName("ListSalesNumberSeries")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OrdersRead))
            .Produces<IReadOnlyList<SalesNumberSeriesResponse>>();

        group.MapPost("/", async (Guid companyId, SalesNumberSeriesRequest request, CreateSalesNumberSeriesHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateSalesNumberSeries(new CompanyId(companyId), request.DocumentType.ToType(), request.ValidFrom, request.Prefix, request.Padding),
                    cancellationToken)))
            .WithName("CreateSalesNumberSeries")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.SettingsWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static List<SalesInvoiceDraftLine> ToLines(IReadOnlyList<SalesInvoiceLineRequest>? lines) =>
        (lines ?? []).Select(line => new SalesInvoiceDraftLine(line.DeliveryLineId, line.OrderLineId, line.Quantity)).ToList();
}
