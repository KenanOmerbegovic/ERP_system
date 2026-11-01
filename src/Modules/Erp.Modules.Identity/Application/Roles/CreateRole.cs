using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Authorization;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Domain.Roles;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Roles;

public sealed record CreateRole(CompanyId CompanyId, string Name, IReadOnlyList<string> Permissions);

internal sealed class CreateRoleValidator : AbstractValidator<CreateRole>
{
    public CreateRoleValidator(PermissionCatalog catalog)
    {
        RuleFor(command => command.Name).MustBeValidRoleName();
        RuleFor(command => command.Permissions).MustBeKnownPermissions(catalog);
    }
}

public sealed class CreateRoleHandler(IIdentityDbContext db, IValidator<CreateRole> validator)
{
    public async Task<Result<Guid>> HandleAsync(CreateRole command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        if (!await db.Companies.AnyAsync(company => company.Id == command.CompanyId, cancellationToken))
        {
            return IdentityErrors.CompanyNotFound;
        }

        var name = command.Name.Trim();
        if (await db.Roles.AnyAsync(role => role.CompanyId == command.CompanyId && role.Name == name, cancellationToken))
        {
            return IdentityErrors.DuplicateRoleName;
        }

        var role = Role.Create(command.CompanyId, name, command.Permissions);
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        return role.Id;
    }
}
