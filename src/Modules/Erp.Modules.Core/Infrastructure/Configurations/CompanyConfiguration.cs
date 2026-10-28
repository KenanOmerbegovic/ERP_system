using Erp.Modules.Core.Domain;
using Erp.Modules.Core.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Core.Infrastructure.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(c => c.TaxId).HasMaxLength(FieldLengths.TaxId);
        builder.Property(c => c.VatNumber).HasMaxLength(FieldLengths.VatNumber);
        builder.Property(c => c.Address).HasMaxLength(FieldLengths.Address);
        builder.Property(c => c.PostalCode).HasMaxLength(FieldLengths.PostalCode);
        builder.Property(c => c.City).HasMaxLength(FieldLengths.City);
        builder.HasIndex(c => c.TaxId).IsUnique();
        builder.Ignore(c => c.IsVatRegistered);
        builder.Ignore(c => c.DomainEvents);
    }
}
