using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Common;
using Erp.Modules.Identity.Domain.Registration;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Erp.Modules.Identity.Application.Registration;

/// <summary>Issued by the operator (for now from the command line). Null lifetime uses the configured default.</summary>
public sealed record IssueRegistrationKey(TimeSpan? Lifetime = null);

/// <summary>The key in clear text, shown once; only its hash is stored.</summary>
public sealed record IssuedRegistrationKey(string Key, DateTimeOffset ExpiresAt);

internal sealed class IssueRegistrationKeyValidator : AbstractValidator<IssueRegistrationKey>
{
    public IssueRegistrationKeyValidator()
    {
        RuleFor(command => command.Lifetime)
            .GreaterThan(TimeSpan.Zero)
            .LessThanOrEqualTo(TimeSpan.FromDays(365))
            .When(command => command.Lifetime is not null);
    }
}

public sealed class IssueRegistrationKeyHandler(
    IIdentityDbContext db,
    IValidator<IssueRegistrationKey> validator,
    IClock clock,
    IOptions<RegistrationOptions> options)
{
    public async Task<Result<IssuedRegistrationKey>> HandleAsync(IssueRegistrationKey command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var key = Secrets.NewRegistrationKey();
        var stored = RegistrationKey.Issue(
            Secrets.Hash(Secrets.NormalizeRegistrationKey(key)),
            clock.UtcNow,
            command.Lifetime ?? options.Value.KeyLifetime);
        db.RegistrationKeys.Add(stored);
        await db.SaveChangesAsync(cancellationToken);
        return new IssuedRegistrationKey(key, stored.ExpiresAt);
    }
}
