using Erp.Modules.Core.Application.BusinessPartners;
using Erp.Modules.Core.Application.Common;
using Erp.Modules.Core.Contracts.BusinessPartners;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Core.Endpoints;

internal static class BusinessPartnerEndpoints
{
    public static void MapBusinessPartnerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/business-partners").WithTags("Business partners");

        group.MapGet("/", async (
            Guid companyId,
            string? search,
            int? page,
            int? pageSize,
            ListBusinessPartnersHandler handler,
            CancellationToken cancellationToken) =>
            HttpResults.Ok(await handler.HandleAsync(
                new ListBusinessPartners(new CompanyId(companyId), search, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                cancellationToken)));

        group.MapPost("/", async (
            Guid companyId,
            BusinessPartnerRequest request,
            CreateBusinessPartnerHandler handler,
            CancellationToken cancellationToken) =>
            HttpResults.Created(await handler.HandleAsync(
                new CreateBusinessPartner(
                    new CompanyId(companyId),
                    request.Code,
                    request.Name,
                    request.TaxId,
                    request.VatNumber,
                    request.Address,
                    request.PostalCode,
                    request.City,
                    request.CountryCode,
                    request.IsCustomer,
                    request.IsSupplier,
                    request.IsActive),
                cancellationToken)));

        group.MapPut("/{businessPartnerId:guid}", async (
            Guid companyId,
            Guid businessPartnerId,
            BusinessPartnerRequest request,
            UpdateBusinessPartnerHandler handler,
            CancellationToken cancellationToken) =>
            HttpResults.NoContent(await handler.HandleAsync(
                new UpdateBusinessPartner(
                    new CompanyId(companyId),
                    businessPartnerId,
                    request.Code,
                    request.Name,
                    request.TaxId,
                    request.VatNumber,
                    request.Address,
                    request.PostalCode,
                    request.City,
                    request.CountryCode,
                    request.IsCustomer,
                    request.IsSupplier,
                    request.IsActive),
                cancellationToken)));
    }
}
