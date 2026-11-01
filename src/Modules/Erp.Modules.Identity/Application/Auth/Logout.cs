using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Application.Common;
using Erp.SharedKernel.Results;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Auth;

public sealed record Logout(string? RefreshToken);

/// <summary>Ends the session the refresh token belongs to. Unknown or missing tokens are not an error.</summary>
public sealed class LogoutHandler(IIdentityDbContext db, IClock clock)
{
    public async Task<Result> HandleAsync(Logout command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result.Success();
        }

        var hash = Secrets.Hash(command.RefreshToken);
        var token = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is not null)
        {
            await RefreshSessionHandler.RevokeFamilyAsync(db, token.FamilyId, clock.UtcNow, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
