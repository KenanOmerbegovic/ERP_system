using Erp.Modules.Core.Application.UnitsOfMeasure;
using Erp.Modules.Core.Contracts;
using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Contracts.UnitsOfMeasure;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Core.Endpoints;

internal static class UnitOfMeasureEndpoints
{
    public static void MapUnitOfMeasureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/units-of-measure").WithTags("Units of measure");

        group.MapGet("/", async (Guid companyId, ListUnitsOfMeasureHandler handler, CancellationToken cancellationToken) =>
            HttpResults.Ok(await handler.HandleAsync(new ListUnitsOfMeasure(new CompanyId(companyId)), cancellationToken)))
            .WithName("ListUnitsOfMeasure")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.UnitsOfMeasureRead))
            .Produces<IReadOnlyList<UnitOfMeasureResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            Guid companyId,
            UnitOfMeasureRequest request,
            CreateUnitOfMeasureHandler handler,
            CancellationToken cancellationToken) =>
            HttpResults.Created(await handler.HandleAsync(
                new CreateUnitOfMeasure(new CompanyId(companyId), request.Code, request.Name),
                cancellationToken)))
            .WithName("CreateUnitOfMeasure")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.UnitsOfMeasureWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{unitOfMeasureId:guid}", async (
            Guid companyId,
            Guid unitOfMeasureId,
            UnitOfMeasureRequest request,
            UpdateUnitOfMeasureHandler handler,
            CancellationToken cancellationToken) =>
            HttpResults.NoContent(await handler.HandleAsync(
                new UpdateUnitOfMeasure(new CompanyId(companyId), unitOfMeasureId, request.Code, request.Name),
                cancellationToken)))
            .WithName("UpdateUnitOfMeasure")
            .RequireAuthorization(PermissionPolicy.For(CorePermissions.UnitsOfMeasureWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
