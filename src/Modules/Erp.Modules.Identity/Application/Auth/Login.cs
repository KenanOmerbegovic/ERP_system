using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Domain.Users;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Erp.Modules.Identity.Application.Auth;

public sealed record Login(string Email, string Password);

internal sealed class LoginValidator : AbstractValidator<Login>
{
    public LoginValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(Domain.FieldLengths.Email);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(ValidationExtensions.PasswordMaxLength);
    }
}

public sealed class LoginHandler(
    IIdentityDbContext db,
    IValidator<Login> validator,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    IClock clock,
    IOptions<LockoutOptions> lockout)
{
    public async Task<Result<AuthTokens>> HandleAsync(Login command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var now = clock.UtcNow;
        var email = User.NormalizeEmail(command.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            passwordHasher.VerifyWithoutUser(command.Password);
            return IdentityErrors.InvalidCredentials;
        }

        if (user.IsLockedOut(now))
        {
            return IdentityErrors.LockedOut;
        }

        if (!passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            user.RecordFailedLogin(now, lockout.Value.MaxFailedAttempts, lockout.Value.Duration);
            await db.SaveChangesAsync(cancellationToken);
            return IdentityErrors.InvalidCredentials;
        }

        user.RecordSuccessfulLogin();
        var tokens = sessions.Start(user, now);
        await db.SaveChangesAsync(cancellationToken);
        return tokens;
    }
}
