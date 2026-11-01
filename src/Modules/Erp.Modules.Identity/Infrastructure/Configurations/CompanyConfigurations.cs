using Erp.Modules.Identity.Domain;
using Erp.Modules.Identity.Domain.Companies;
using Erp.Modules.Identity.Domain.Memberships;
using Erp.Modules.Identity.Domain.Roles;
using Erp.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Identity.Infrastructure.Configurations;

internal sealed class CompanyReferenceConfiguration : IEntityTypeConfiguration<CompanyReference>
{
    public void Configure(EntityTypeBuilder<CompanyReference> builder)
    {
        builder.ToTable("companies");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(FieldLengths.CompanyName);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(FieldLengths.RoleName);
        builder.PrimitiveCollection(r => r.Permissions).ElementType().HasMaxLength(FieldLengths.Permission);
        builder.HasIndex(r => new { r.CompanyId, r.Name }).IsUnique();
        builder.HasIndex(r => r.CompanyId).IsUnique().HasFilter("is_administrator");
        builder.HasOne<CompanyReference>().WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.HasKey(m => m.Id);
        builder.PrimitiveCollection(m => m.RoleIds);
        builder.HasIndex(m => new { m.CompanyId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
        builder.HasOne<CompanyReference>().WithMany().HasForeignKey(m => m.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(m => m.DomainEvents);
    }
}
