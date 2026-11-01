using Erp.Modules.Identity.Application.Authorization;
using Erp.Modules.Identity.Contracts.Authorization;
using Erp.SharedKernel.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Erp.Modules.Identity.Endpoints.Authorization;

internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

internal sealed record CompanyRegistrationRequirement : IAuthorizationRequirement;

/// <summary>Builds <see cref="PermissionPolicy"/> policies on demand, so modules don't register one per permission.</summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options, PermissionCatalog catalog)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        var permission = policyName[PermissionPolicy.Prefix.Length..];
        if (!catalog.Contains(permission))
        {
            throw new InvalidOperationException($"Permission '{permission}' is not registered; add it to the module's permission list.");
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}

/// <summary>Checks the permission in the company named by the route; per request, against the database.</summary>
internal sealed class PermissionHandler(MemberPermissions permissions) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext
            || context.User.Identity?.IsAuthenticated != true
            || !Guid.TryParse(httpContext.GetRouteValue(PermissionPolicy.CompanyRouteValue)?.ToString(), out var companyId))
        {
            return;
        }

        if (await permissions.HasPermissionAsync(context.User.GetUserId(), new CompanyId(companyId), requirement.Permission, httpContext.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}

internal sealed class CompanyRegistrationHandler(MemberPermissions permissions) : AuthorizationHandler<CompanyRegistrationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CompanyRegistrationRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        if (await permissions.CanRegisterCompanyAsync(context.User.GetUserId(), cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
