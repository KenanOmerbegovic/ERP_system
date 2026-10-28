using Erp.Modules.Core.Domain;
using Erp.Modules.Core.Domain.Articles;
using Erp.Modules.Core.Domain.Companies;
using Erp.Modules.Core.Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Core.Infrastructure.Configurations;

internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(a => a.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.Code).HasMaxLength(FieldLengths.Code);
        builder.Property(a => a.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => new { a.CompanyId, a.Code }).IsUnique();
        builder.Ignore(a => a.DomainEvents);
    }
}
