using System.Security.Claims;

namespace Erp.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Authorization policy names other modules put on their endpoints. A permission policy
/// requires an active membership in the company named by the <c>companyId</c> route value,
/// with a role granting the permission.
/// </summary>
public static class PermissionPolicy
{
    public const string Prefix = "permission:";

    /// <summary>The route value that names the company a permission is checked in.</summary>
    public const string CompanyRouteValue = "companyId";

    public static string For(string permission) => Prefix + permission;
}

public static class IdentityPolicies
{
    /// <summary>The user redeemed a registration key that has not yet been used to create a company.</summary>
    public const string CompanyRegistration = "company-registration";
}

/// <summary>Permissions the Identity module checks.</summary>
public static class IdentityPermissions
{
    public const string UsersRead = "identity.users.read";
    public const string UsersWrite = "identity.users.write";
    public const string RolesRead = "identity.roles.read";
    public const string RolesWrite = "identity.roles.write";

    public static IReadOnlyList<string> All { get; } = [UsersRead, UsersWrite, RolesRead, RolesWrite];
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>The <c>sub</c> claim of the access token.</summary>
    public const string UserIdClaim = "sub";

    /// <summary>The authenticated user's id. Only call on endpoints that require authorization.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var value = principal.FindFirst(UserIdClaim)?.Value;
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("The principal has no user id; the endpoint must require authorization.");
    }
}
