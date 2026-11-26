using Erp.Modules.Core.Application.Vat;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Contracts.Vat;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Core.Endpoints;

internal static class VatRateEndpoints
{
    public static void MapVatRateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/vat-rates").WithTags("VatRates");

        group.MapGet("/", async (Guid companyId, ListVatRatesHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(new ListVatRates(new CompanyId(companyId)), cancellationToken)))
            .WithName("ListVatRates")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.VatRatesRead))
            .Produces<IReadOnlyList<VatRateResponse>>();

        group.MapPost("/", async (Guid companyId, VatRateRequest request, CreateVatRateHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateVatRate(new CompanyId(companyId), request.Code, request.Name, request.Rate, request.ValidFrom),
                    cancellationToken)))
            .WithName("CreateVatRate")
            .WithDescription("Rates are not edited: a rate change is a new rate of the same code from a later date.")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.VatRatesWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
