using Erp.Modules.Sales.Domain;
using Erp.Modules.Sales.Domain.CreditNotes;
using Erp.Modules.Sales.Domain.Deliveries;
using Erp.Modules.Sales.Domain.Invoices;
using Erp.Modules.Sales.Domain.Numbering;
using Erp.Modules.Sales.Domain.Offers;
using Erp.Modules.Sales.Domain.Orders;
using Erp.Modules.Sales.Domain.References;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Modules.Sales.Infrastructure.Configurations;

// Table names are explicit: SalesLocks addresses them in SQL.
internal static class Tables
{
    public const string Customers = "customers";
    public const string Articles = "articles";
    public const string Offers = "offers";
    public const string OfferLines = "offer_lines";
    public const string SalesOrders = "sales_orders";
    public const string SalesOrderLines = "sales_order_lines";
    public const string DeliveryNotes = "delivery_notes";
    public const string DeliveryNoteLines = "delivery_note_lines";
    public const string SalesInvoices = "sales_invoices";
    public const string SalesInvoiceLines = "sales_invoice_lines";
    public const string SalesInvoiceVatTotals = "sales_invoice_vat_totals";
    public const string CreditNotes = "credit_notes";
    public const string CreditNoteLines = "credit_note_lines";
    public const string CreditNoteVatTotals = "credit_note_vat_totals";
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

    public static PropertyBuilder<decimal> IsRate(this PropertyBuilder<decimal> property) => property.HasPrecision(5, 4);

    public static PropertyBuilder<DocumentStatus> IsStatus(this PropertyBuilder<DocumentStatus> property) =>
        property.HasConversion<string>().HasMaxLength(20);
}

internal sealed class CustomerReferenceConfiguration : IEntityTypeConfiguration<CustomerReference>
{
    public void Configure(EntityTypeBuilder<CustomerReference> builder)
    {
        builder.ToTable(Tables.Customers);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Code).HasMaxLength(FieldLengths.Code);
        builder.Property(c => c.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(c => c.TaxId).HasMaxLength(FieldLengths.TaxId);
        builder.Property(c => c.VatNumber).HasMaxLength(FieldLengths.TaxId);
        builder.HasIndex(c => new { c.CompanyId, c.Code });
        builder.Ignore(c => c.CanBeSoldTo);
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
    }
}

internal sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable(Tables.Offers);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Number).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(o => o.Note).HasMaxLength(FieldLengths.Note);
        builder.HasOne<CustomerReference>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OfferId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(o => new { o.CompanyId, o.Number }).IsUnique();
        builder.HasIndex(o => new { o.CompanyId, o.OfferDate });
        builder.Ignore(o => o.IsDraft);
        builder.Ignore(o => o.NetAmount);
        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class OfferLineConfiguration : IEntityTypeConfiguration<OfferLine>
{
    public void Configure(EntityTypeBuilder<OfferLine> builder)
    {
        builder.ToTable(Tables.OfferLines);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Quantity).IsQuantity();
        builder.Property(l => l.UnitPrice).IsQuantity();
        builder.Property(l => l.VatCode).HasMaxLength(FieldLengths.VatCode);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.OfferId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.NetAmount);
    }
}

internal sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable(Tables.SalesOrders);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).IsStatus();
        builder.Property(o => o.Number).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(o => o.Note).HasMaxLength(FieldLengths.Note);
        builder.HasOne<CustomerReference>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Offer>().WithMany().HasForeignKey(o => o.OfferId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesOrder>().WithMany().HasForeignKey(o => o.ReversesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(o => new { o.CompanyId, o.Number }).IsUnique();
        builder.HasIndex(o => new { o.CompanyId, o.OrderDate });
        builder.HasIndex(o => o.ReversesOrderId).IsUnique();

        // One offer gives at most one order (its storno aside).
        builder.HasIndex(o => o.OfferId).IsUnique().HasFilter("offer_id IS NOT NULL AND reverses_order_id IS NULL");
        builder.Ignore(o => o.IsDraft);
        builder.Ignore(o => o.IsStorno);
        builder.Ignore(o => o.IsOpen);
        builder.Ignore(o => o.NetAmount);
        builder.Ignore(o => o.FullyDelivered);
        builder.Ignore(o => o.FullyInvoiced);
        builder.Ignore(o => o.CanBeReversed);
        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable(Tables.SalesOrderLines, table =>
        {
            table.HasCheckConstraint("ck_sales_order_lines_delivered", "delivered_quantity >= 0 AND delivered_quantity <= quantity AND (NOT is_service OR delivered_quantity = 0)");
            table.HasCheckConstraint(
                "ck_sales_order_lines_invoiced",
                "invoiced_quantity >= 0 AND invoiced_quantity <= CASE WHEN is_service THEN quantity ELSE delivered_quantity END");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Quantity).IsQuantity();
        builder.Property(l => l.UnitPrice).IsQuantity();
        builder.Property(l => l.DeliveredQuantity).IsQuantity();
        builder.Property(l => l.InvoicedQuantity).IsQuantity();
        builder.Property(l => l.VatCode).HasMaxLength(FieldLengths.VatCode);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.OrderId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.NetAmount);
        builder.Ignore(l => l.OpenQuantity);
        builder.Ignore(l => l.InvoiceableQuantity);
    }
}

internal sealed class DeliveryNoteConfiguration : IEntityTypeConfiguration<DeliveryNote>
{
    public void Configure(EntityTypeBuilder<DeliveryNote> builder)
    {
        builder.ToTable(Tables.DeliveryNotes);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Status).IsStatus();
        builder.Property(d => d.Number).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(d => d.StockDocumentNumber).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(d => d.Note).HasMaxLength(FieldLengths.Note);
        builder.HasOne<SalesOrder>().WithMany().HasForeignKey(d => d.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerReference>().WithMany().HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeliveryNote>().WithMany().HasForeignKey(d => d.ReversesDeliveryNoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(d => d.Lines).WithOne().HasForeignKey(l => l.DeliveryNoteId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(d => new { d.CompanyId, d.Number }).IsUnique();
        builder.HasIndex(d => new { d.SalesOrderId, d.Status });
        builder.HasIndex(d => d.ReversesDeliveryNoteId).IsUnique();
        builder.Ignore(d => d.IsDraft);
        builder.Ignore(d => d.IsStorno);
        builder.Ignore(d => d.CostValue);
        builder.Ignore(d => d.CanBeReversed);
        builder.Ignore(d => d.DomainEvents);
    }
}

internal sealed class DeliveryNoteLineConfiguration : IEntityTypeConfiguration<DeliveryNoteLine>
{
    public void Configure(EntityTypeBuilder<DeliveryNoteLine> builder)
    {
        builder.ToTable(Tables.DeliveryNoteLines, table =>
            table.HasCheckConstraint("ck_delivery_note_lines_invoiced", "invoiced_quantity >= 0 AND invoiced_quantity <= quantity"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Quantity).IsQuantity();
        builder.Property(l => l.CostValue).IsAmount();
        builder.Property(l => l.InvoicedQuantity).IsQuantity();
        builder.Property(l => l.LotNumber).HasMaxLength(FieldLengths.LotNumber);
        builder.PrimitiveCollection<List<string>>("_serialNumbers").HasColumnName("serial_numbers");
        builder.HasOne<SalesOrderLine>().WithMany().HasForeignKey(l => l.OrderLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.DeliveryNoteId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.SerialNumbers);
        builder.Ignore(l => l.UninvoicedQuantity);
        builder.Ignore(l => l.UnitCost);
    }
}

internal sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable(Tables.SalesInvoices);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Status).IsStatus();
        builder.Property(i => i.Number).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(i => i.Note).HasMaxLength(FieldLengths.Note);
        builder.Property(i => i.NetAmount).IsAmount();
        builder.Property(i => i.VatAmount).IsAmount();
        builder.Property(i => i.GrossAmount).IsAmount();
        builder.HasOne<SalesOrder>().WithMany().HasForeignKey(i => i.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerReference>().WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesInvoice>().WithMany().HasForeignKey(i => i.ReversesInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.VatTotals).WithOne().HasForeignKey(t => t.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(i => i.VatTotals).HasField("_vatTotals").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(i => new { i.CompanyId, i.Number }).IsUnique();
        builder.HasIndex(i => new { i.CompanyId, i.InvoiceDate });
        builder.HasIndex(i => i.SalesOrderId);
        builder.HasIndex(i => i.ReversesInvoiceId).IsUnique();
        builder.Ignore(i => i.IsDraft);
        builder.Ignore(i => i.IsStorno);
        builder.Ignore(i => i.IsOpen);
        builder.Ignore(i => i.CreditedAmount);
        builder.Ignore(i => i.CanBeReversed);
        builder.Ignore(i => i.DomainEvents);
    }
}

internal sealed class SalesInvoiceLineConfiguration : IEntityTypeConfiguration<SalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceLine> builder)
    {
        builder.ToTable(Tables.SalesInvoiceLines, table =>
        {
            table.HasCheckConstraint("ck_sales_invoice_lines_source", "is_service = (delivery_line_id IS NULL)");
            table.HasCheckConstraint("ck_sales_invoice_lines_returned", "returned_quantity >= 0 AND returned_quantity <= quantity");
            table.HasCheckConstraint("ck_sales_invoice_lines_credited", "credited_amount >= 0");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Quantity).IsQuantity();
        builder.Property(l => l.UnitPrice).IsQuantity();
        builder.Property(l => l.VatCode).HasMaxLength(FieldLengths.VatCode);
        builder.Property(l => l.VatRate).IsRate();
        builder.Property(l => l.VatAmount).IsAmount();
        builder.Property(l => l.CreditedAmount).IsAmount();
        builder.Property(l => l.ReturnedQuantity).IsQuantity();
        builder.HasOne<SalesOrderLine>().WithMany().HasForeignKey(l => l.OrderLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeliveryNoteLine>().WithMany().HasForeignKey(l => l.DeliveryLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.InvoiceId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.NetAmount);
        builder.Ignore(l => l.CreditableAmount);
        builder.Ignore(l => l.ReturnableQuantity);
    }
}

internal sealed class SalesInvoiceVatTotalConfiguration : IEntityTypeConfiguration<SalesInvoiceVatTotal>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceVatTotal> builder)
    {
        builder.ToTable(Tables.SalesInvoiceVatTotals);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.VatCode).HasMaxLength(FieldLengths.VatCode);
        builder.Property(t => t.Rate).IsRate();
        builder.Property(t => t.Base).IsAmount();
        builder.Property(t => t.VatAmount).IsAmount();
    }
}

internal sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.ToTable(Tables.CreditNotes);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Status).IsStatus();
        builder.Property(c => c.Number).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(c => c.StockDocumentNumber).HasMaxLength(FieldLengths.DocumentNumber);
        builder.Property(c => c.Note).HasMaxLength(FieldLengths.Note);
        builder.Property(c => c.NetAmount).IsAmount();
        builder.Property(c => c.VatAmount).IsAmount();
        builder.Property(c => c.GrossAmount).IsAmount();
        builder.HasOne<SalesInvoice>().WithMany().HasForeignKey(c => c.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesOrder>().WithMany().HasForeignKey(c => c.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerReference>().WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditNote>().WithMany().HasForeignKey(c => c.ReversesCreditNoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.Lines).WithOne().HasForeignKey(l => l.CreditNoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.VatTotals).WithOne().HasForeignKey(t => t.CreditNoteId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.VatTotals).HasField("_vatTotals").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(c => new { c.CompanyId, c.Number }).IsUnique();
        builder.HasIndex(c => new { c.InvoiceId, c.Status });
        builder.HasIndex(c => c.ReversesCreditNoteId).IsUnique();
        builder.Ignore(c => c.IsDraft);
        builder.Ignore(c => c.IsStorno);
        builder.Ignore(c => c.HasReturns);
        builder.Ignore(c => c.ReturnedCostValue);
        builder.Ignore(c => c.CanBeReversed);
        builder.Ignore(c => c.ReturnLines);
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class CreditNoteLineConfiguration : IEntityTypeConfiguration<CreditNoteLine>
{
    public void Configure(EntityTypeBuilder<CreditNoteLine> builder)
    {
        builder.ToTable(Tables.CreditNoteLines, table =>
            table.HasCheckConstraint("ck_credit_note_lines_kind", "(kind = 'Return' AND quantity > 0) OR (kind = 'ValueReduction' AND quantity = 0)"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.Quantity).IsQuantity();
        builder.Property(l => l.UnitPrice).IsQuantity();
        builder.Property(l => l.NetAmount).IsAmount();
        builder.Property(l => l.VatCode).HasMaxLength(FieldLengths.VatCode);
        builder.Property(l => l.VatRate).IsRate();
        builder.Property(l => l.VatAmount).IsAmount();
        builder.Property(l => l.LotNumber).HasMaxLength(FieldLengths.LotNumber);
        builder.Property(l => l.ReturnUnitCost).IsQuantity();
        builder.Property(l => l.ReturnedCostValue).IsAmount();
        builder.PrimitiveCollection<List<string>>("_serialNumbers").HasColumnName("serial_numbers");
        builder.HasOne<SalesInvoiceLine>().WithMany().HasForeignKey(l => l.InvoiceLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ArticleReference>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.CreditNoteId, l.LineNumber }).IsUnique();
        builder.Ignore(l => l.SerialNumbers);
    }
}

internal sealed class CreditNoteVatTotalConfiguration : IEntityTypeConfiguration<CreditNoteVatTotal>
{
    public void Configure(EntityTypeBuilder<CreditNoteVatTotal> builder)
    {
        builder.ToTable(Tables.CreditNoteVatTotals);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.VatCode).HasMaxLength(FieldLengths.VatCode);
        builder.Property(t => t.Rate).IsRate();
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

/// <summary>The gapless counters behind <see cref="NumberSeries"/>; only SalesLocks reads and writes them, in SQL.</summary>
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
