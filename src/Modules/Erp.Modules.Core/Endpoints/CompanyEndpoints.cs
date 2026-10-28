using Erp.Modules.Core.Application.Companies;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Contracts.Companies;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Core.Endpoints;

internal static class CompanyEndpoints
{
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies").WithTags("Companies");

        group.MapGet("/", async (ListCompaniesHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new ListCompanies(), cancellationToken)));

        group.MapPost("/", async (CompanyRequest request, CreateCompanyHandler handler, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CreateCompany(request.Name, request.TaxId, request.VatNumber, request.Address, request.PostalCode, request.City),
                cancellationToken);
            return result.IsSuccess
                ? Results.Created((string?)null, new CreatedResponse(result.Value.Value))
                : HttpResults.Problem(result.Error);
        });

        group.MapPut("/{companyId:guid}", async (
            Guid companyId,
            CompanyRequest request,
            UpdateCompanyHandler handler,
            CancellationToken cancellationToken) =>
            HttpResults.NoContent(await handler.HandleAsync(
                new UpdateCompany(
                    new CompanyId(companyId),
                    request.Name,
                    request.TaxId,
                    request.VatNumber,
                    request.Address,
                    request.PostalCode,
                    request.City),
                cancellationToken)));
    }
}
