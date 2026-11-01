using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Identity.Application.Users;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Identity.Contracts.Users;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Identity.Endpoints;

internal static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/users").WithTags("Users");

        group.MapGet("/", async (Guid companyId, ListMembersHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(new ListMembers(new CompanyId(companyId)), cancellationToken)))
            .WithName("ListUsers")
            .RequireAuthorization(PermissionPolicy.For(IdentityPermissions.UsersRead))
            .Produces<IReadOnlyList<MemberResponse>>();

        group.MapPost("/", async (Guid companyId, CreateUserRequest request, CreateUserHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Created(await handler.HandleAsync(
                    new CreateUser(new CompanyId(companyId), request.Email, request.DisplayName, request.Password, request.RoleIds),
                    cancellationToken)))
            .WithName("CreateUser")
            .RequireAuthorization(PermissionPolicy.For(IdentityPermissions.UsersWrite))
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{userId:guid}", async (
                Guid companyId,
                Guid userId,
                UpdateMemberRequest request,
                UpdateMemberHandler handler,
                CancellationToken cancellationToken) =>
            HttpResults.NoContent(await handler.HandleAsync(
                new UpdateMember(new CompanyId(companyId), userId, request.RoleIds, request.IsActive),
                cancellationToken)))
            .WithName("UpdateUser")
            .RequireAuthorization(PermissionPolicy.For(IdentityPermissions.UsersWrite))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
