using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Authorization;
using Erp.Modules.Identity.Domain;
using Erp.SharedKernel.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Roles;

internal static class RoleLookup
{
    /// <summary>All ids name roles of the company.</summary>
    public static async Task<bool> RolesExistAsync(this IIdentityDbContext db, CompanyId companyId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var distinct = roleIds.Distinct().ToList();
        var found = await db.Roles.CountAsync(role => role.CompanyId == companyId && distinct.Contains(role.Id), cancellationToken);
        return found == distinct.Count;
    }

    public static IRuleBuilderOptions<T, IReadOnlyList<string>> MustBeKnownPermissions<T>(this IRuleBuilder<T, IReadOnlyList<string>> rule, PermissionCatalog catalog) =>
        rule.NotNull()
            .Must(permissions => permissions.All(catalog.Contains))
            .WithErrorCode(IdentityErrors.Codes.UnknownPermission)
            .WithMessage("Unknown permission.");

    public static IRuleBuilderOptions<T, string> MustBeValidRoleName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(FieldLengths.RoleName);
}
