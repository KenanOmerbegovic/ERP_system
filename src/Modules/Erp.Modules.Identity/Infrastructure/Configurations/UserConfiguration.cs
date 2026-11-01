using Erp.Modules.Identity.Domain;
using Erp.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Identity.Infrastructure.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(FieldLengths.Email);
        builder.Property(u => u.DisplayName).HasMaxLength(FieldLengths.DisplayName);
        builder.Property(u => u.PasswordHash).HasMaxLength(FieldLengths.PasswordHash);
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
