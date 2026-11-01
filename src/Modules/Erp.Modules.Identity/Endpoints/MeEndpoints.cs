using System.Security.Claims;
using Erp.Modules.Identity.Application.Authorization;
using Erp.Modules.Identity.Application.Users;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.Modules.Identity.Contracts.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Identity.Endpoints;

internal static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (ClaimsPrincipal user, GetCurrentUserHandler handler, CancellationToken cancellationToken) =>
                HttpResults.Ok(await handler.HandleAsync(new GetCurrentUser(user.GetUserId()), cancellationToken)))
            .WithTags("Me")
            .WithName("GetCurrentUser")
            .RequireAuthorization()
            .Produces<CurrentUserResponse>();

        app.MapGet("/api/permissions", (PermissionCatalog catalog) => Results.Ok(catalog.All))
            .WithTags("Roles")
            .WithName("ListPermissions")
            .WithDescription("Every permission a role can grant.")
            .RequireAuthorization()
            .Produces<IReadOnlyList<string>>();
    }
}
