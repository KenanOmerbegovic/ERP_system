using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Sales.Application.Common;
using Erp.Modules.Sales.Application.Offers;
using Erp.Modules.Sales.Contracts;
using Erp.Modules.Sales.Contracts.Offers;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Sales.Endpoints;

internal static class OfferEndpoints
{
    public static void MapOfferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/offers").WithTags("Offers");

        group.MapGet("/", async (
                Guid companyId,
                Guid? customerId,
                OfferState? status,
                string? search,
                int? page,
                int? pageSize,
                ListOffersHandler handler,
                CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(
                    new ListOffers(new CompanyId(companyId), customerId, status?.ToStatus(), search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                    cancellationToken)))
            .WithName("ListOffers")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OffersRead))
            .Produces<PagedResponse<OfferSummaryResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{offerId:guid}", async (Guid companyId, Guid offerId, GetOfferHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetOffer(new CompanyId(companyId), offerId), cancellationToken)))
            .WithName("GetOffer")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OffersRead))
            .Produces<OfferResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Guid companyId, OfferRequest request, CreateOfferHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateOffer(new CompanyId(companyId), request.CustomerId, request.OfferDate, request.ValidUntil, request.Note, SalesLines.From(request.Lines)),
                    cancellationToken)))
            .WithName("CreateOffer")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OffersWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{offerId:guid}", async (Guid companyId, Guid offerId, OfferRequest request, UpdateOfferHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(
                    new UpdateOffer(
                        new CompanyId(companyId), offerId, request.CustomerId, request.OfferDate, request.ValidUntil, request.Note, SalesLines.From(request.Lines)),
                    cancellationToken)))
            .WithName("UpdateOffer")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OffersWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{offerId:guid}", async (Guid companyId, Guid offerId, DeleteOfferHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new DeleteOffer(new CompanyId(companyId), offerId), cancellationToken)))
            .WithName("DeleteOffer")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OffersWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{offerId:guid}/send", async (Guid companyId, Guid offerId, SendOfferHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new SendOffer(new CompanyId(companyId), offerId), cancellationToken)))
            .WithName("SendOffer")
            .WithDescription("Numbers the offer and freezes it. 422 NumberSeries.NotConfigured without a number series.")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OffersSend))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{offerId:guid}/accept", async (
                Guid companyId, Guid offerId, AcceptOfferRequest request, AcceptOfferHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(new AcceptOffer(new CompanyId(companyId), offerId, request.OrderDate, request.WarehouseId), cancellationToken)))
            .WithName("AcceptOffer")
            .WithDescription("The customer accepted the offer: creates a draft sales order with its lines and returns the order's id (FR-SAL-02).")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OffersSend))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{offerId:guid}/reject", async (Guid companyId, Guid offerId, RejectOfferHandler handler, CancellationToken cancellationToken) =>
                HttpResults.NoContent(await handler.HandleAsync(new RejectOffer(new CompanyId(companyId), offerId), cancellationToken)))
            .WithName("RejectOffer")
            .RequireAuthorization(PermissionPolicy.For(SalesPermissions.OffersSend))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

internal static class SalesLines
{
    public static List<SalesDraftLine> From(IReadOnlyList<SalesLineRequest>? lines) =>
        (lines ?? []).Select(line => new SalesDraftLine(line.ArticleId, line.Quantity, line.UnitPrice, line.VatCode)).ToList();
}
