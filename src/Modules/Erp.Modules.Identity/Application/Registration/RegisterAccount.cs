using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Auth;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Domain.Users;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Registration;

/// <summary>
/// Step 1 of company registration: redeems the key, creates the future administrator's
/// account and signs them in. The company itself is created next, in Core; its
/// CompanyCreated event then makes this user the company's administrator.
/// </summary>
public sealed record RegisterAccount(string RegistrationKey, string Email, string DisplayName, string Password);

internal sealed class RegisterAccountValidator : AbstractValidator<RegisterAccount>
{
    public RegisterAccountValidator()
    {
        RuleFor(command => command.RegistrationKey).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email).MustBeValidEmail();
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(Domain.FieldLengths.DisplayName);
        RuleFor(command => command.Password).MustBeValidPassword();
    }
}

public sealed class RegisterAccountHandler(
    IIdentityDbContext db,
    IValidator<RegisterAccount> validator,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    IClock clock)
{
    public async Task<Result<AuthTokens>> HandleAsync(RegisterAccount command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var now = clock.UtcNow;
        var keyHash = Secrets.Hash(Secrets.NormalizeRegistrationKey(command.RegistrationKey));
        var key = await db.RegistrationKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);
        if (key is null || !key.CanBeRedeemed(now))
        {
            return IdentityErrors.InvalidRegistrationKey;
        }

        var email = User.NormalizeEmail(command.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return IdentityErrors.DuplicateEmail;
        }

        var user = User.Create(email, command.DisplayName, passwordHasher.Hash(command.Password), now);
        db.Users.Add(user);
        key.Redeem(user.Id, now);
        var tokens = sessions.Start(user, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The same key was redeemed concurrently.
            return IdentityErrors.InvalidRegistrationKey;
        }

        return tokens;
    }
}
