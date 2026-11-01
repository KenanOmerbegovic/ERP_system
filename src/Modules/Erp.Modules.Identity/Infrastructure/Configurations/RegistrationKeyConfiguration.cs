using Erp.Modules.Identity.Domain;
using Erp.Modules.Identity.Domain.Registration;
using Erp.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Identity.Infrastructure.Configurations;

internal sealed class RegistrationKeyConfiguration : IEntityTypeConfiguration<RegistrationKey>
{
    public void Configure(EntityTypeBuilder<RegistrationKey> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.KeyHash).HasMaxLength(FieldLengths.SecretHash);
        builder.HasIndex(k => k.KeyHash).IsUnique();
        builder.HasIndex(k => k.RedeemedByUserId);
        builder.HasOne<User>().WithMany().HasForeignKey(k => k.RedeemedByUserId).OnDelete(DeleteBehavior.Restrict);

        // PostgreSQL xmin: a key redeemed concurrently is redeemed only once.
        builder.Property<uint>("Version").IsRowVersion();
    }
}
