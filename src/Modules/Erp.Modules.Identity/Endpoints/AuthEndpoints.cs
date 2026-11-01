using System.Security.Claims;
using Erp.Modules.Identity.Application.Auth;
using Erp.Modules.Identity.Contracts.Auth;
using Erp.Modules.Identity.Contracts.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Identity.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RefreshTokenCookie.Path).WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, HttpResponse response, LoginHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new Login(request.Email, request.Password), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(RefreshTokenCookie.Respond(response, result.Value, request.TokenDelivery))
                    : HttpResults.Problem(result.Error);
            })
            .WithName("Login")
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimiting.Policy)
            .Produces<AuthResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", async (
                RefreshRequest? request,
                HttpRequest httpRequest,
                HttpResponse response,
                RefreshSessionHandler handler,
                CancellationToken cancellationToken) =>
            {
                var delivery = request?.TokenDelivery ?? RefreshTokenDelivery.Cookie;
                var token = delivery == RefreshTokenDelivery.Body ? request?.RefreshToken : RefreshTokenCookie.Read(httpRequest);
                var result = await handler.HandleAsync(new RefreshSession(token), cancellationToken);
                if (result.IsFailure)
                {
                    if (delivery == RefreshTokenDelivery.Cookie)
                    {
                        RefreshTokenCookie.Delete(response);
                    }

                    return HttpResults.Problem(result.Error);
                }

                return Results.Ok(RefreshTokenCookie.Respond(response, result.Value, delivery));
            })
            .WithName("RefreshSession")
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimiting.Policy)
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", async (
                LogoutRequest? request,
                HttpRequest httpRequest,
                HttpResponse response,
                LogoutHandler handler,
                CancellationToken cancellationToken) =>
            {
                var token = request?.RefreshToken ?? RefreshTokenCookie.Read(httpRequest);
                RefreshTokenCookie.Delete(response);
                return HttpResults.NoContent(await handler.HandleAsync(new Logout(token), cancellationToken));
            })
            .WithName("Logout")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/change-password", async (
                ChangePasswordRequest request,
                ClaimsPrincipal user,
                ChangePasswordHandler handler,
                CancellationToken cancellationToken) =>
            HttpResults.NoContent(await handler.HandleAsync(
                new ChangePassword(user.GetUserId(), request.CurrentPassword, request.NewPassword),
                cancellationToken)))
            .WithName("ChangePassword")
            .RequireAuthorization()
            .RequireRateLimiting(IdentityRateLimiting.Policy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
    }
}
