using Erp.Modules.Core.Domain;
using Erp.Modules.Core.Domain.Companies;
using Erp.Modules.Core.Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Core.Infrastructure.Configurations;

internal sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasOne<Company>().WithMany().HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(u => u.Code).HasMaxLength(FieldLengths.UnitCode);
        builder.Property(u => u.Name).HasMaxLength(FieldLengths.UnitName);
        builder.HasIndex(u => new { u.CompanyId, u.Code }).IsUnique();
        builder.Ignore(u => u.DomainEvents);
    }
}
