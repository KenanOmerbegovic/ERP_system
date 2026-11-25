using Erp.Modules.Warehouse.Domain;
using Erp.Modules.Warehouse.Domain.Articles;
using Erp.Modules.Warehouse.Domain.Numbering;
using Erp.Modules.Warehouse.Domain.Stock;
using Erp.Modules.Warehouse.Domain.StockDocuments;
using Erp.Modules.Warehouse.Domain.Storehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Warehouse.Infrastructure.Configurations;

// Table names are explicit: StockLocks addresses them in SQL.
internal static class Tables
{
    public const string Articles = "articles";
    public const string Storehouses = "warehouses";
    public const string StockDocuments = "stock_documents";
    public const string StockDocumentLines = "stock_document_lines";
    public const string StockItems = "stock_items";
    public const string Lots = "lots";
    public const string LotBalances = "lot_balances";
    public const string SerialNumbers = "serial_numbers";
    public const string StockMovements = "stock_movements";
    public const string NumberSeries = "number_series";
    public const string NumberCounters = "number_counters";
}

internal static class Columns
{
    public static PropertyBuilder<decimal> IsQuantity(this PropertyBuilder<decimal> property) =>
        property.HasPrecision(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale);

    public static PropertyBuilder<decimal?> IsQuantity(this PropertyBuilder<decimal?> property) =>
        property.HasPrecision(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale);

    public static PropertyBuilder<decimal> IsValue(this PropertyBuilder<decimal> property) =>
        property.HasPrecision(FieldLengths.ValuePrecision, FieldLengths.ValueScale);

    public static PropertyBuilder<decimal?> IsValue(this PropertyBuilder<decimal?> property) =>
        property.HasPrecision(FieldLengths.ValuePrecision, FieldLengths.ValueScale);
}

internal sealed class ArticleReferenceConfiguration : IEntityTypeConfiguration<ArticleReference>
{
    public void Configure(EntityTypeBuilder<ArticleReference> builder)
    {
        builder.ToTable(Tables.Articles);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Code).HasMaxLength(FieldLengths.Code);
        builder.Property(a => a.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(a => a.Tracking).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => new { a.CompanyId, a.Code });
        builder.Ignore(a => a.IsStockable);
    }
}

internal sealed class StorehouseConfiguration : IEntityTypeConfiguration<Storehouse>
{
    public void Configure(EntityTypeBuilder<Storehouse> builder)
    {
        builder.ToTable(Tables.Storehouses);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).HasMaxLength(FieldLengths.Code);
        builder.Property(s => s.Name).HasMaxLength(FieldLengths.Name);
        builder.HasIndex(s => new { s.CompanyId, s.Code }).IsUnique();
        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class StockDocumentConfiguration : IEntityTypeConfiguration<StockDocument>
{
    public void Configure(EntityTypeBuilder<StockDocument> builder)
    {
        builder.ToTable(Tables.StockDocuments);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Number).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(d => d.Note).HasMaxLength(FieldLengths.Note);
        builder.Property(d => d.SourceStorehouseId).HasColumnName("source_warehouse_id");
        builder.Property(d => d.TargetStorehouseId).HasColumnName("target_warehouse_id");
        builder.HasOne<Storehouse>().WithMany().HasForeignKey(d => d.SourceStorehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Storehouse>().WithMany().HasForeignKey(d => d.TargetStorehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockDocument>().WithMany().HasForeignKey(d => d.ReversesDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(d => d.Lines).WithOne().HasForeignKey(line => line.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(d => new { d.CompanyId, d.Number }).IsUnique();
        builder.HasIndex(d => new { d.CompanyId, d.DocumentDate });
        builder.HasIndex(d => d.ReversesDocumentId).IsUnique();
        builder.Ignore(d => d.IsStorno);
        builder.Ignore(d => d.IsDraft);
        builder.Ignore(d => d.TotalValue);
        builder.Ignore(d => d.DomainEvents);
    }
}

internal sealed class StockDocumentLineConfiguration : IEntityTypeConfiguration<StockDocumentLine>
{
    public void Configure(EntityTypeBuilder<StockDocumentLine> builder)
    {
        builder.ToTable(Tables.StockDocumentLines, table =>
            table.HasCheckConstraint("ck_stock_document_lines_quantity_positive", "quantity > 0"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Quantity).IsQuantity();
        builder.Property(l => l.UnitCost).IsQuantity();
        builder.Property(l => l.Value).IsValue();
        builder.Property(l => l.PriceDifference).IsValue();
        builder.Property(l => l.LotNumber).HasMaxLength(FieldLengths.LotNumber);
        builder.PrimitiveCollection<List<string>>("_serialNumbers").HasColumnName("serial_numbers");
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.DocumentId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.SerialNumbers);
        builder.Ignore(l => l.ReceiptValue);
        builder.Ignore(l => l.OriginalValue);
    }
}

internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable(Tables.StockItems, table =>
        {
            table.HasCheckConstraint("ck_stock_items_quantity_not_negative", "quantity >= 0");
            table.HasCheckConstraint("ck_stock_items_value_not_negative", "value >= 0");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.StorehouseId).HasColumnName("warehouse_id");
        builder.Property(i => i.Quantity).IsQuantity();
        builder.Property(i => i.Value).IsValue();
        builder.HasOne<Storehouse>().WithMany().HasForeignKey(i => i.StorehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(i => i.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.CompanyId, i.StorehouseId, i.ArticleId }).IsUnique();
        builder.Ignore(i => i.AverageCost);
    }
}

internal sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable(Tables.Lots);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Number).HasMaxLength(FieldLengths.LotNumber);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.CompanyId, l.ArticleId, l.Number }).IsUnique();
    }
}

internal sealed class LotBalanceConfiguration : IEntityTypeConfiguration<LotBalance>
{
    public void Configure(EntityTypeBuilder<LotBalance> builder)
    {
        builder.ToTable(Tables.LotBalances, table =>
            table.HasCheckConstraint("ck_lot_balances_quantity_not_negative", "quantity >= 0"));
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.StorehouseId).HasColumnName("warehouse_id");
        builder.Property(b => b.Quantity).IsQuantity();
        builder.HasOne<Storehouse>().WithMany().HasForeignKey(b => b.StorehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Lot>().WithMany().HasForeignKey(b => b.LotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => new { b.CompanyId, b.StorehouseId, b.LotId }).IsUnique();
    }
}

internal sealed class SerialNumberConfiguration : IEntityTypeConfiguration<SerialNumber>
{
    public void Configure(EntityTypeBuilder<SerialNumber> builder)
    {
        builder.ToTable(Tables.SerialNumbers);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Number).HasMaxLength(FieldLengths.SerialNumber);
        builder.Property(s => s.StorehouseId).HasColumnName("warehouse_id");
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(s => s.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Storehouse>().WithMany().HasForeignKey(s => s.StorehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.CompanyId, s.ArticleId, s.Number }).IsUnique();
        builder.Ignore(s => s.InStock);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable(Tables.StockMovements);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.StorehouseId).HasColumnName("warehouse_id");
        builder.Property(m => m.Quantity).IsQuantity();
        builder.Property(m => m.Value).IsValue();
        builder.HasOne<StockDocument>().WithMany().HasForeignKey(m => m.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockDocumentLine>().WithMany().HasForeignKey(m => m.DocumentLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Storehouse>().WithMany().HasForeignKey(m => m.StorehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(m => m.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Lot>().WithMany().HasForeignKey(m => m.LotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.CompanyId, m.StorehouseId, m.ArticleId });
    }
}

internal sealed class NumberSeriesConfiguration : IEntityTypeConfiguration<NumberSeries>
{
    public void Configure(EntityTypeBuilder<NumberSeries> builder)
    {
        builder.ToTable(Tables.NumberSeries);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.DocumentType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Prefix).HasMaxLength(FieldLengths.NumberPrefix);
        builder.HasIndex(s => new { s.CompanyId, s.DocumentType, s.ValidFrom }).IsUnique();
    }
}

/// <summary>The gapless counters behind <see cref="NumberSeries"/>; only StockLocks reads and writes them, in SQL.</summary>
internal sealed class NumberCounter
{
    public Guid CompanyId { get; set; }

    public string DocumentType { get; set; } = null!;

    public int Year { get; set; }

    public int LastNumber { get; set; }
}

internal sealed class NumberCounterConfiguration : IEntityTypeConfiguration<NumberCounter>
{
    public void Configure(EntityTypeBuilder<NumberCounter> builder)
    {
        builder.ToTable(Tables.NumberCounters);
        builder.HasKey(c => new { c.CompanyId, c.DocumentType, c.Year });
        builder.Property(c => c.DocumentType).HasMaxLength(20);
    }
}
