using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Common;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Auth;

public sealed record RefreshSession(string? RefreshToken);

/// <summary>Rotates the refresh token. A revoked token being presented again revokes its whole family.</summary>
public sealed class RefreshSessionHandler(IIdentityDbContext db, SessionIssuer sessions, IClock clock)
{
    public async Task<Result<AuthTokens>> HandleAsync(RefreshSession command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        var now = clock.UtcNow;
        var hash = Secrets.Hash(command.RefreshToken);
        var current = await db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (current is null)
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        if (current.IsRevoked)
        {
            await RevokeFamilyAsync(db, current.FamilyId, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return IdentityErrors.InvalidRefreshToken;
        }

        if (!current.IsActive(now))
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        var user = await db.Users.FirstAsync(u => u.Id == current.UserId, cancellationToken);
        var tokens = sessions.Rotate(user, current, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent refresh rotated the same token first.
            return IdentityErrors.InvalidRefreshToken;
        }

        return tokens;
    }

    internal static async Task RevokeFamilyAsync(IIdentityDbContext db, Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var family = await db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in family)
        {
            token.Revoke(now);
        }
    }
}
