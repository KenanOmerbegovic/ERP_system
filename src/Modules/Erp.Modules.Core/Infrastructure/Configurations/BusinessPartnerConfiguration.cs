using Erp.Modules.Core.Domain;
using Erp.Modules.Core.Domain.BusinessPartners;
using Erp.Modules.Core.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Core.Infrastructure.Configurations;

internal sealed class BusinessPartnerConfiguration : IEntityTypeConfiguration<BusinessPartner>
{
    public void Configure(EntityTypeBuilder<BusinessPartner> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(p => p.Code).HasMaxLength(FieldLengths.Code);
        builder.Property(p => p.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(p => p.TaxId).HasMaxLength(FieldLengths.TaxId);
        builder.Property(p => p.VatNumber).HasMaxLength(FieldLengths.VatNumber);
        builder.Property(p => p.Address).HasMaxLength(FieldLengths.Address);
        builder.Property(p => p.PostalCode).HasMaxLength(FieldLengths.PostalCode);
        builder.Property(p => p.City).HasMaxLength(FieldLengths.City);
        builder.Property(p => p.CountryCode).HasMaxLength(FieldLengths.CountryCode).IsFixedLength();
        builder.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
        builder.Ignore(p => p.DomainEvents);
    }
}
