using Erp.Modules.Purchasing.Domain;
using Erp.Modules.Purchasing.Domain.Invoices;
using Erp.Modules.Purchasing.Domain.Numbering;
using Erp.Modules.Purchasing.Domain.Orders;
using Erp.Modules.Purchasing.Domain.Receipts;
using Erp.Modules.Purchasing.Domain.References;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Purchasing.Infrastructure.Configurations;

// Table names are explicit: PurchasingLocks addresses them in SQL.
internal static class Tables
{
    public const string Suppliers = "suppliers";
    public const string Articles = "articles";
    public const string PurchaseOrders = "purchase_orders";
    public const string PurchaseOrderLines = "purchase_order_lines";
    public const string GoodsReceipts = "goods_receipts";
    public const string GoodsReceiptLines = "goods_receipt_lines";
    public const string SupplierInvoices = "supplier_invoices";
    public const string SupplierInvoiceLines = "supplier_invoice_lines";
    public const string SupplierInvoiceVatTotals = "supplier_invoice_vat_totals";
    public const string NumberSeries = "number_series";
    public const string NumberCounters = "number_counters";
}

internal static class Columns
{
    public static PropertyBuilder<decimal> IsQuantity(this PropertyBuilder<decimal> property) =>
        property.HasPrecision(FieldLengths.QuantityPrecision, FieldLengths.QuantityScale);

    public static PropertyBuilder<decimal> IsAmount(this PropertyBuilder<decimal> property) =>
        property.HasPrecision(FieldLengths.ValuePrecision, FieldLengths.ValueScale);

    public static PropertyBuilder<decimal?> IsAmount(this PropertyBuilder<decimal?> property) =>
        property.HasPrecision(FieldLengths.ValuePrecision, FieldLengths.ValueScale);

    public static PropertyBuilder<DocumentStatus> IsStatus(this PropertyBuilder<DocumentStatus> property) =>
        property.HasConversion<string>().HasMaxLength(20);
}

internal sealed class SupplierReferenceConfiguration : IEntityTypeConfiguration<SupplierReference>
{
    public void Configure(EntityTypeBuilder<SupplierReference> builder)
    {
        builder.ToTable(Tables.Suppliers);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Code).HasMaxLength(FieldLengths.Code);
        builder.Property(s => s.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(s => s.TaxId).HasMaxLength(FieldLengths.TaxId);
        builder.Property(s => s.VatNumber).HasMaxLength(FieldLengths.TaxId);
        builder.HasIndex(s => new { s.CompanyId, s.Code });
        builder.Ignore(s => s.CanBeOrderedFrom);
    }
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
        builder.Property(a => a.DefaultVatCode).HasMaxLength(FieldLengths.VatCode);
        builder.HasIndex(a => new { a.CompanyId, a.Code });
        builder.Ignore(a => a.CanBeOrdered);
    }
}

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable(Tables.PurchaseOrders);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).IsStatus();
        builder.Property(o => o.Number).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(o => o.Note).HasMaxLength(FieldLengths.Note);
        builder.HasOne<SupplierReference>().WithMany().HasForeignKey(o => o.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(o => o.ReversesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(o => new { o.CompanyId, o.Number }).IsUnique();
        builder.HasIndex(o => new { o.CompanyId, o.OrderDate });
        builder.HasIndex(o => o.ReversesOrderId).IsUnique();
        builder.Ignore(o => o.IsDraft);
        builder.Ignore(o => o.IsStorno);
        builder.Ignore(o => o.IsOpen);
        builder.Ignore(o => o.NetAmount);
        builder.Ignore(o => o.FullyReceived);
        builder.Ignore(o => o.CanBeReversed);
        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable(Tables.PurchaseOrderLines, table =>
        {
            table.HasCheckConstraint("ck_purchase_order_lines_received", "received_quantity >= 0 AND received_quantity <= quantity");
            table.HasCheckConstraint("ck_purchase_order_lines_invoiced", "invoiced_quantity >= 0 AND invoiced_quantity <= received_quantity");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Quantity).IsQuantity();
        builder.Property(l => l.UnitPrice).IsQuantity();
        builder.Property(l => l.ReceivedQuantity).IsQuantity();
        builder.Property(l => l.InvoicedQuantity).IsQuantity();
        builder.Property(l => l.VatCode).HasMaxLength(FieldLengths.VatCode);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.OrderId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.NetAmount);
        builder.Ignore(l => l.OpenQuantity);
    }
}

internal sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable(Tables.GoodsReceipts);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).IsStatus();
        builder.Property(r => r.Number).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(r => r.StockDocumentNumber).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(r => r.Note).HasMaxLength(FieldLengths.Note);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(r => r.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierReference>().WithMany().HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoodsReceipt>().WithMany().HasForeignKey(r => r.ReversesReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(r => new { r.CompanyId, r.Number }).IsUnique();
        builder.HasIndex(r => new { r.PurchaseOrderId, r.Status });
        builder.HasIndex(r => r.ReversesReceiptId).IsUnique();
        builder.Ignore(r => r.IsDraft);
        builder.Ignore(r => r.IsStorno);
        builder.Ignore(r => r.TotalValue);
        builder.Ignore(r => r.CanBeReversed);
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable(Tables.GoodsReceiptLines, table =>
            table.HasCheckConstraint("ck_goods_receipt_lines_invoiced", "invoiced_quantity >= 0 AND invoiced_quantity <= quantity"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Quantity).IsQuantity();
        builder.Property(l => l.UnitCost).IsQuantity();
        builder.Property(l => l.Value).IsAmount();
        builder.Property(l => l.InvoicedQuantity).IsQuantity();
        builder.Property(l => l.LotNumber).HasMaxLength(FieldLengths.LotNumber);
        builder.PrimitiveCollection<List<string>>("_serialNumbers").HasColumnName("serial_numbers");
        builder.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(l => l.OrderLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.ReceiptId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.SerialNumbers);
        builder.Ignore(l => l.UninvoicedQuantity);
    }
}

internal sealed class SupplierInvoiceConfiguration : IEntityTypeConfiguration<SupplierInvoice>
{
    public void Configure(EntityTypeBuilder<SupplierInvoice> builder)
    {
        builder.ToTable(Tables.SupplierInvoices);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Status).IsStatus();
        builder.Property(i => i.Number).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(i => i.SupplierInvoiceNumber).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(i => i.Note).HasMaxLength(FieldLengths.Note);
        builder.Property(i => i.NetAmount).IsAmount();
        builder.Property(i => i.VatAmount).IsAmount();
        builder.Property(i => i.GrossAmount).IsAmount();
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(i => i.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierReference>().WithMany().HasForeignKey(i => i.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierInvoice>().WithMany().HasForeignKey(i => i.ReversesInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.VatTotals).WithOne().HasForeignKey(t => t.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.VatTotals).HasField("_vatTotals").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(i => new { i.CompanyId, i.Number }).IsUnique();
        builder.HasIndex(i => i.ReversesInvoiceId).IsUnique();

        // A supplier's invoice number is registered once; a reversed invoice frees it for the corrected one.
        builder.HasIndex(i => new { i.CompanyId, i.SupplierId, i.SupplierInvoiceNumber })
            .IsUnique()
            .HasFilter("status = 'Confirmed' AND reverses_invoice_id IS NULL AND reversed_by_invoice_id IS NULL");
        builder.Ignore(i => i.IsDraft);
        builder.Ignore(i => i.IsStorno);
        builder.Ignore(i => i.PriceDifference);
        builder.Ignore(i => i.CanBeReversed);
        builder.Ignore(i => i.DomainEvents);
    }
}

internal sealed class SupplierInvoiceLineConfiguration : IEntityTypeConfiguration<SupplierInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceLine> builder)
    {
        builder.ToTable(Tables.SupplierInvoiceLines);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Quantity).IsQuantity();
        builder.Property(l => l.UnitPrice).IsQuantity();
        builder.Property(l => l.ReceivedUnitCost).IsQuantity();
        builder.Property(l => l.VatCode).HasMaxLength(FieldLengths.VatCode);
        builder.Property(l => l.VatRate).HasPrecision(5, 4);
        builder.Property(l => l.VatAmount).IsAmount();
        builder.HasOne<GoodsReceiptLine>().WithMany().HasForeignKey(l => l.ReceiptLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.InvoiceId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.NetAmount);
        builder.Ignore(l => l.ReceivedValue);
        builder.Ignore(l => l.PriceDifference);
    }
}

internal sealed class SupplierInvoiceVatTotalConfiguration : IEntityTypeConfiguration<SupplierInvoiceVatTotal>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceVatTotal> builder)
    {
        builder.ToTable(Tables.SupplierInvoiceVatTotals);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.VatCode).HasMaxLength(FieldLengths.VatCode);
        builder.Property(t => t.Rate).HasPrecision(5, 4);
        builder.Property(t => t.Base).IsAmount();
        builder.Property(t => t.VatAmount).IsAmount();
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

/// <summary>The gapless counters behind <see cref="NumberSeries"/>; only PurchasingLocks reads and writes them, in SQL.</summary>
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
