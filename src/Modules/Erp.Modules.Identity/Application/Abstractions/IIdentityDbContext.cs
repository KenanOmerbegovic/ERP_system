using Erp.Modules.Identity.Domain.Companies;
using Erp.Modules.Identity.Domain.Memberships;
using Erp.Modules.Identity.Domain.Registration;
using Erp.Modules.Identity.Domain.Roles;
using Erp.Modules.Identity.Domain.Sessions;
using Erp.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Application.Abstractions;

/// <summary>The Identity module's data, implemented by Infrastructure.</summary>
public interface IIdentityDbContext
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<RegistrationKey> RegistrationKeys { get; }

    DbSet<CompanyReference> Companies { get; }

    DbSet<Role> Roles { get; }

    DbSet<Membership> Memberships { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
