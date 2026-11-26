using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Purchasing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPurchasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "purchasing");

            migrationBuilder.CreateTable(
                name: "articles",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_goods = table.Column<bool>(type: "boolean", nullable: false),
                    tracking = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_articles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "number_counters",
                schema: "purchasing",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_number_counters", x => new { x.company_id, x.document_type, x.year });
                });

            migrationBuilder.CreateTable(
                name: "number_series",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    padding = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_number_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    vat_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_supplier = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_date = table.Column<DateOnly>(type: "date", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reverses_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_orders_purchase_orders_reverses_order_id",
                        column: x => x.reverses_order_id,
                        principalSchema: "purchasing",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_orders_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "purchasing",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stock_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reverses_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_goods_receipts_goods_receipts_reverses_receipt_id",
                        column: x => x.reverses_receipt_id,
                        principalSchema: "purchasing",
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_receipts_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalSchema: "purchasing",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_receipts_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "purchasing",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    received_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    invoiced_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_order_lines", x => x.id);
                    table.CheckConstraint("ck_purchase_order_lines_invoiced", "invoiced_quantity >= 0 AND invoiced_quantity <= received_quantity");
                    table.CheckConstraint("ck_purchase_order_lines_received", "received_quantity >= 0 AND received_quantity <= quantity");
                    table.ForeignKey(
                        name: "fk_purchase_order_lines_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "purchasing",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_order_lines_purchase_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "purchasing",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoices",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reverses_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    net_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_invoices_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalSchema: "purchasing",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_invoices_supplier_invoices_reverses_invoice_id",
                        column: x => x.reverses_invoice_id,
                        principalSchema: "purchasing",
                        principalTable: "supplier_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_invoices_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "purchasing",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    invoiced_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    lot_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lot_expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    serial_numbers = table.Column<List<string>>(type: "text[]", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_receipt_lines", x => x.id);
                    table.CheckConstraint("ck_goods_receipt_lines_invoiced", "invoiced_quantity >= 0 AND invoiced_quantity <= quantity");
                    table.ForeignKey(
                        name: "fk_goods_receipt_lines_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "purchasing",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goods_receipt_lines_goods_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalSchema: "purchasing",
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_goods_receipt_lines_purchase_order_line_order_line_id",
                        column: x => x.order_line_id,
                        principalSchema: "purchasing",
                        principalTable: "purchase_order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoice_vat_totals",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    @base = table.Column<decimal>(name: "base", type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_invoice_vat_totals", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_invoice_vat_totals_supplier_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "purchasing",
                        principalTable: "supplier_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoice_lines",
                schema: "purchasing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    received_unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_invoice_lines_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "purchasing",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_invoice_lines_goods_receipt_lines_receipt_line_id",
                        column: x => x.receipt_line_id,
                        principalSchema: "purchasing",
                        principalTable: "goods_receipt_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_invoice_lines_supplier_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "purchasing",
                        principalTable: "supplier_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_articles_company_id_code",
                schema: "purchasing",
                table: "articles",
                columns: new[] { "company_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_lines_article_id",
                schema: "purchasing",
                table: "goods_receipt_lines",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_lines_order_line_id",
                schema: "purchasing",
                table: "goods_receipt_lines",
                column: "order_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_lines_receipt_id_line_number",
                schema: "purchasing",
                table: "goods_receipt_lines",
                columns: new[] { "receipt_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_company_id_number",
                schema: "purchasing",
                table: "goods_receipts",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_purchase_order_id_status",
                schema: "purchasing",
                table: "goods_receipts",
                columns: new[] { "purchase_order_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_reverses_receipt_id",
                schema: "purchasing",
                table: "goods_receipts",
                column: "reverses_receipt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_supplier_id",
                schema: "purchasing",
                table: "goods_receipts",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_number_series_company_id_document_type_valid_from",
                schema: "purchasing",
                table: "number_series",
                columns: new[] { "company_id", "document_type", "valid_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_at",
                schema: "purchasing",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_article_id",
                schema: "purchasing",
                table: "purchase_order_lines",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_order_id_line_number",
                schema: "purchasing",
                table: "purchase_order_lines",
                columns: new[] { "order_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_company_id_number",
                schema: "purchasing",
                table: "purchase_orders",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_company_id_order_date",
                schema: "purchasing",
                table: "purchase_orders",
                columns: new[] { "company_id", "order_date" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_reverses_order_id",
                schema: "purchasing",
                table: "purchase_orders",
                column: "reverses_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_supplier_id",
                schema: "purchasing",
                table: "purchase_orders",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoice_lines_article_id",
                schema: "purchasing",
                table: "supplier_invoice_lines",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoice_lines_invoice_id_line_number",
                schema: "purchasing",
                table: "supplier_invoice_lines",
                columns: new[] { "invoice_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoice_lines_receipt_line_id",
                schema: "purchasing",
                table: "supplier_invoice_lines",
                column: "receipt_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoice_vat_totals_invoice_id",
                schema: "purchasing",
                table: "supplier_invoice_vat_totals",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoices_company_id_number",
                schema: "purchasing",
                table: "supplier_invoices",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoices_company_id_supplier_id_supplier_invoice_n",
                schema: "purchasing",
                table: "supplier_invoices",
                columns: new[] { "company_id", "supplier_id", "supplier_invoice_number" },
                unique: true,
                filter: "status = 'Confirmed' AND reverses_invoice_id IS NULL AND reversed_by_invoice_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoices_purchase_order_id",
                schema: "purchasing",
                table: "supplier_invoices",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoices_reverses_invoice_id",
                schema: "purchasing",
                table: "supplier_invoices",
                column: "reverses_invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoices_supplier_id",
                schema: "purchasing",
                table: "supplier_invoices",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_company_id_code",
                schema: "purchasing",
                table: "suppliers",
                columns: new[] { "company_id", "code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "number_counters",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "number_series",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "supplier_invoice_lines",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "supplier_invoice_vat_totals",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "goods_receipt_lines",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "supplier_invoices",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "goods_receipts",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "purchase_order_lines",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "articles",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "purchasing");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "purchasing");
        }
    }
}
