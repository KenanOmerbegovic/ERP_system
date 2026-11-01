using Erp.Modules.Identity.Application.Abstractions;
using Erp.Modules.Identity.Domain.Companies;
using Erp.Modules.Identity.Domain.Memberships;
using Erp.Modules.Identity.Domain.Registration;
using Erp.Modules.Identity.Domain.Roles;
using Erp.Modules.Identity.Domain.Sessions;
using Erp.Modules.Identity.Domain.Users;
using Erp.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Erp.Modules.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options), IIdentityDbContext
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<RegistrationKey> RegistrationKeys => Set<RegistrationKey>();

    public DbSet<CompanyReference> Companies => Set<CompanyReference>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<CompanyId>().HaveConversion<CompanyIdConverter>();
    }

    private sealed class CompanyIdConverter() : ValueConverter<CompanyId, Guid>(id => id.Value, value => new CompanyId(value));
}
