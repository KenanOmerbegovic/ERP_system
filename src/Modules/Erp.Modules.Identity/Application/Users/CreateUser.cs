using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Application.Roles;
using Erp.Modules.Identity.Domain.Memberships;
using Erp.Modules.Identity.Domain.Users;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Users;

/// <summary>
/// A company administrator creates a login for a colleague with an initial password.
/// Emails are unique across all companies; adding an existing account to another
/// company is not supported yet (docs/open-decisions.md).
/// </summary>
public sealed record CreateUser(CompanyId CompanyId, string Email, string DisplayName, string Password, IReadOnlyList<Guid> RoleIds);

internal sealed class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(command => command.Email).MustBeValidEmail();
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(Domain.FieldLengths.DisplayName);
        RuleFor(command => command.Password).MustBeValidPassword();
        RuleFor(command => command.RoleIds).NotNull();
    }
}

public sealed class CreateUserHandler(
    IIdentityDbContext db,
    IValidator<CreateUser> validator,
    IPasswordHasher passwordHasher,
    IClock clock)
{
    public async Task<Result<Guid>> HandleAsync(CreateUser command, CancellationToken cancellationToken)
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

        if (!await db.RolesExistAsync(command.CompanyId, command.RoleIds, cancellationToken))
        {
            return ValidationExtensions.PropertyError(nameof(CreateUser.RoleIds), IdentityErrors.Codes.UnknownRole);
        }

        var email = User.NormalizeEmail(command.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return IdentityErrors.DuplicateEmail;
        }

        var user = User.Create(email, command.DisplayName, passwordHasher.Hash(command.Password), clock.UtcNow);
        db.Users.Add(user);
        db.Memberships.Add(Membership.Create(command.CompanyId, user.Id, command.RoleIds));
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
