using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Common;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Auth;

public sealed record ChangePassword(Guid UserId, string CurrentPassword, string NewPassword);

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePassword>
{
    public ChangePasswordValidator()
    {
        RuleFor(command => command.CurrentPassword).NotEmpty().MaximumLength(ValidationExtensions.PasswordMaxLength);
        RuleFor(command => command.NewPassword).MustBeValidPassword();
    }
}

/// <summary>Changes the user's own password and signs out all their sessions.</summary>
public sealed class ChangePasswordHandler(
    IIdentityDbContext db,
    IValidator<ChangePassword> validator,
    IPasswordHasher passwordHasher,
    IClock clock)
{
    public async Task<Result> HandleAsync(ChangePassword command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return IdentityErrors.UserNotFound;
        }

        if (!passwordHasher.Verify(user.PasswordHash, command.CurrentPassword))
        {
            return ValidationExtensions.PropertyError(nameof(ChangePassword.CurrentPassword), IdentityErrors.Codes.WrongPassword);
        }

        user.ChangePasswordHash(passwordHasher.Hash(command.NewPassword));
        var now = clock.UtcNow;
        var sessions = await db.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
