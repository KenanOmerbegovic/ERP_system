using Erp.Modules.Core.Domain;
using Erp.Modules.Core.Domain.Companies;
using Erp.Modules.Core.Domain.Vat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Core.Infrastructure.Configurations;

internal sealed class VatRateConfiguration : IEntityTypeConfiguration<VatRate>
{
    public void Configure(EntityTypeBuilder<VatRate> builder)
    {
        builder.ToTable("vat_rates", table => table.HasCheckConstraint("ck_vat_rates_rate", "rate >= 0 AND rate <= 1"));
        builder.HasKey(r => r.Id);
        builder.HasOne<Company>().WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(r => r.Code).HasMaxLength(FieldLengths.VatCode);
        builder.Property(r => r.Name).HasMaxLength(FieldLengths.UnitName);
        builder.Property(r => r.Rate).HasPrecision(5, 4);
        builder.HasIndex(r => new { r.CompanyId, r.Code, r.ValidFrom }).IsUnique();
        builder.Ignore(r => r.DomainEvents);
    }
}
