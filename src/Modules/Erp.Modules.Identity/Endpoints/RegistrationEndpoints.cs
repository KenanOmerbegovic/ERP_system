using Erp.Modules.Identity.Application.Registration;
using Erp.Modules.Identity.Contracts.Auth;
using Erp.Modules.Identity.Contracts.Registration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Erp.Modules.Identity.Endpoints;

internal static class RegistrationEndpoints
{
    public static void MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/registration", async (
                RegisterAccountRequest request,
                HttpResponse response,
                RegisterAccountHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new RegisterAccount(request.RegistrationKey, request.Email, request.DisplayName, request.Password),
                    cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(RefreshTokenCookie.Respond(response, result.Value, request.TokenDelivery))
                    : HttpResults.Problem(result.Error);
            })
            .WithTags("Registration")
            .WithName("RegisterAccount")
            .WithDescription("Step 1 of company registration. Then create the company with POST /api/companies and poll GET /api/me until it appears.")
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimiting.Policy)
            .Produces<AuthResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}
