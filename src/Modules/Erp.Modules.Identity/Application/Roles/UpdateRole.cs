using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Authorization;
using Erp.Modules.Identity.Application.Common;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Roles;

public sealed record UpdateRole(CompanyId CompanyId, Guid RoleId, string Name, IReadOnlyList<string> Permissions);

internal sealed class UpdateRoleValidator : AbstractValidator<UpdateRole>
{
    public UpdateRoleValidator(PermissionCatalog catalog)
    {
        RuleFor(command => command.Name).MustBeValidRoleName();
        RuleFor(command => command.Permissions).MustBeKnownPermissions(catalog);
    }
}

public sealed class UpdateRoleHandler(IIdentityDbContext db, IValidator<UpdateRole> validator)
{
    public async Task<Result> HandleAsync(UpdateRole command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var role = await db.Roles.FirstOrDefaultAsync(r => r.CompanyId == command.CompanyId && r.Id == command.RoleId, cancellationToken);
        if (role is null)
        {
            return IdentityErrors.RoleNotFound;
        }

        if (role.IsAdministrator)
        {
            return IdentityErrors.AdministratorRoleIsReadOnly;
        }

        var name = command.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.CompanyId == command.CompanyId && r.Name == name && r.Id != command.RoleId, cancellationToken))
        {
            return IdentityErrors.DuplicateRoleName;
        }

        role.Update(name, command.Permissions);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
