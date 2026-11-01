using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Application.Roles;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Users;

/// <summary>Changes a member's roles or deactivates them in this company. The company always keeps an active administrator.</summary>
public sealed record UpdateMember(CompanyId CompanyId, Guid UserId, IReadOnlyList<Guid> RoleIds, bool IsActive);

internal sealed class UpdateMemberValidator : AbstractValidator<UpdateMember>
{
    public UpdateMemberValidator()
    {
        RuleFor(command => command.RoleIds).NotNull();
    }
}

public sealed class UpdateMemberHandler(IIdentityDbContext db, IValidator<UpdateMember> validator)
{
    public async Task<Result> HandleAsync(UpdateMember command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var membership = await db.Memberships.FirstOrDefaultAsync(
            m => m.CompanyId == command.CompanyId && m.UserId == command.UserId,
            cancellationToken);
        if (membership is null)
        {
            return IdentityErrors.MemberNotFound;
        }

        if (!await db.RolesExistAsync(command.CompanyId, command.RoleIds, cancellationToken))
        {
            return ValidationExtensions.PropertyError(nameof(UpdateMember.RoleIds), IdentityErrors.Codes.UnknownRole);
        }

        var administratorRoleId = await db.Roles
            .Where(role => role.CompanyId == command.CompanyId && role.IsAdministrator)
            .Select(role => role.Id)
            .SingleAsync(cancellationToken);
        var wasAdministrator = membership.IsActive && membership.RoleIds.Contains(administratorRoleId);
        var staysAdministrator = command.IsActive && command.RoleIds.Contains(administratorRoleId);
        if (wasAdministrator && !staysAdministrator && !await HasOtherAdministratorAsync(command, administratorRoleId, cancellationToken))
        {
            return IdentityErrors.LastAdministrator;
        }

        membership.Update(command.RoleIds, command.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private Task<bool> HasOtherAdministratorAsync(UpdateMember command, Guid administratorRoleId, CancellationToken cancellationToken) =>
        db.Memberships.AnyAsync(
            m => m.CompanyId == command.CompanyId
                && m.UserId != command.UserId
                && m.IsActive
                && m.RoleIds.Contains(administratorRoleId),
            cancellationToken);
}
