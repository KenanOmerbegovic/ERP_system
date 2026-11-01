using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Application.Roles;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Identity.Contracts.Roles;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Identity.Endpoints;

internal static class RoleEndpoints
{
    public static void MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/roles").WithTags("Roles");

        group.MapGet("/", async (Guid companyId, ListRolesHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(new ListRoles(new CompanyId(companyId)), cancellationToken)))
            .WithName("ListRoles")
            .RequireAuthorization(PermissionPolicy.For(IdentityPermissions.RolesRead))
            .Produces<IReadOnlyList<RoleResponse>>();

        group.MapPost("/", async (Guid companyId, RoleRequest request, CreateRoleHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateRole(new CompanyId(companyId), request.Name, request.Permissions),
                    cancellationToken)))
            .WithName("CreateRole")
            .RequireAuthorization(PermissionPolicy.For(IdentityPermissions.RolesWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{roleId:guid}", async (
                Guid companyId,
                Guid roleId,
                RoleRequest request,
                UpdateRoleHandler handler,
                CancellationToken cancellationToken) =>
            HttpResults.NoContent(await handler.HandleAsync(
                new UpdateRole(new CompanyId(companyId), roleId, request.Name, request.Permissions),
                cancellationToken)))
            .WithName("UpdateRole")
            .RequireAuthorization(PermissionPolicy.For(IdentityPermissions.RolesWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
