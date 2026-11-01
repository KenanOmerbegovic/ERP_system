using System.Security.Claims;
using Erp.Modules.Core.Application.Companies;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Contracts.Companies;
using Erp.Modules.Identity.Contracts.Authorization;
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

        group.MapPost("/", async (CompanyRequest request, ClaimsPrincipal user, CreateCompanyHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CreateCompany(user.GetUserId(), request.Name, request.TaxId, request.VatNumber, request.Address, request.PostalCode, request.City),
                    cancellationToken);
                return result.IsSuccess
                    ? Results.Created((string?)null, new CreatedResponse(result.Value.Value))
                    : HttpResults.Problem(result.Error);
            })
            .WithName("CreateCompany")
            .WithDescription("Step 2 of company registration: needs a session from POST /api/registration whose key is not used yet. "
                + "The caller becomes the company's administrator moments later; poll GET /api/me.")
            .RequireAuthorization(IdentityPolicies.CompanyRegistration)
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{companyId:guid}", async (Guid companyId, GetCompanyHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetCompany(new CompanyId(companyId)), cancellationToken)))
            .WithName("GetCompany")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.CompaniesRead))
            .Produces<CompanyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

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
                cancellationToken)))
            .WithName("UpdateCompany")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.CompaniesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
