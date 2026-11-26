using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "articles",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_service = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "customers",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    vat_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_customer = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "number_counters",
                schema: "sales",
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
                schema: "sales",
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
                schema: "sales",
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
                name: "offers",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_date = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offers", x => x.id);
                    table.ForeignKey(
                        name: "fk_offers_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "sales",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "offer_lines",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_service = table.Column<bool>(type: "boolean", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offer_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_offer_lines_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "sales",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_offer_lines_offers_offer_id",
                        column: x => x.offer_id,
                        principalSchema: "sales",
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_orders",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reverses_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "sales",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_orders_offers_offer_id",
                        column: x => x.offer_id,
                        principalSchema: "sales",
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_orders_sales_orders_reverses_order_id",
                        column: x => x.reverses_order_id,
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_notes",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stock_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reverses_delivery_note_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_delivery_note_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_notes_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "sales",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_notes_delivery_notes_reverses_delivery_note_id",
                        column: x => x.reverses_delivery_note_id,
                        principalSchema: "sales",
                        principalTable: "delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_notes_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoices",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_sales_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_invoices_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "sales",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_invoices_sales_invoices_reverses_invoice_id",
                        column: x => x.reverses_invoice_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_invoices_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_lines",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_service = table.Column<bool>(type: "boolean", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    delivered_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    invoiced_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_order_lines", x => x.id);
                    table.CheckConstraint("ck_sales_order_lines_delivered", "delivered_quantity >= 0 AND delivered_quantity <= quantity AND (NOT is_service OR delivered_quantity = 0)");
                    table.CheckConstraint("ck_sales_order_lines_invoiced", "invoiced_quantity >= 0 AND invoiced_quantity <= CASE WHEN is_service THEN quantity ELSE delivered_quantity END");
                    table.ForeignKey(
                        name: "fk_sales_order_lines_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "sales",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_order_lines_sales_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_notes",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_note_date = table.Column<DateOnly>(type: "date", nullable: false),
                    vat_date = table.Column<DateOnly>(type: "date", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stock_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reverses_credit_note_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_credit_note_id = table.Column<Guid>(type: "uuid", nullable: true),
                    net_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_credit_notes_credit_notes_reverses_credit_note_id",
                        column: x => x.reverses_credit_note_id,
                        principalSchema: "sales",
                        principalTable: "credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_credit_notes_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "sales",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_credit_notes_sales_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_credit_notes_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalSchema: "sales",
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_vat_totals",
                schema: "sales",
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
                    table.PrimaryKey("pk_sales_invoice_vat_totals", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_invoice_vat_totals_sales_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_note_lines",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cost_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    invoiced_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    lot_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    serial_numbers = table.Column<List<string>>(type: "text[]", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_note_lines", x => x.id);
                    table.CheckConstraint("ck_delivery_note_lines_invoiced", "invoiced_quantity >= 0 AND invoiced_quantity <= quantity");
                    table.ForeignKey(
                        name: "fk_delivery_note_lines_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "sales",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_note_lines_delivery_notes_delivery_note_id",
                        column: x => x.delivery_note_id,
                        principalSchema: "sales",
                        principalTable: "delivery_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_note_lines_sales_order_line_order_line_id",
                        column: x => x.order_line_id,
                        principalSchema: "sales",
                        principalTable: "sales_order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_note_vat_totals",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    @base = table.Column<decimal>(name: "base", type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_note_vat_totals", x => x.id);
                    table.ForeignKey(
                        name: "fk_credit_note_vat_totals_credit_notes_credit_note_id",
                        column: x => x.credit_note_id,
                        principalSchema: "sales",
                        principalTable: "credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_lines",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_note_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_service = table.Column<bool>(type: "boolean", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    credited_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    returned_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_invoice_lines", x => x.id);
                    table.CheckConstraint("ck_sales_invoice_lines_credited", "credited_amount >= 0");
                    table.CheckConstraint("ck_sales_invoice_lines_returned", "returned_quantity >= 0 AND returned_quantity <= quantity");
                    table.CheckConstraint("ck_sales_invoice_lines_source", "is_service = (delivery_line_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_sales_invoice_lines_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "sales",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_invoice_lines_delivery_note_lines_delivery_line_id",
                        column: x => x.delivery_line_id,
                        principalSchema: "sales",
                        principalTable: "delivery_note_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_invoice_lines_sales_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sales_invoice_lines_sales_order_line_order_line_id",
                        column: x => x.order_line_id,
                        principalSchema: "sales",
                        principalTable: "sales_order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_note_lines",
                schema: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    invoice_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    lot_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    return_unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    returned_cost_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    serial_numbers = table.Column<List<string>>(type: "text[]", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_note_lines", x => x.id);
                    table.CheckConstraint("ck_credit_note_lines_kind", "(kind = 'Return' AND quantity > 0) OR (kind = 'ValueReduction' AND quantity = 0)");
                    table.ForeignKey(
                        name: "fk_credit_note_lines_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "sales",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_credit_note_lines_credit_notes_credit_note_id",
                        column: x => x.credit_note_id,
                        principalSchema: "sales",
                        principalTable: "credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_credit_note_lines_sales_invoice_line_invoice_line_id",
                        column: x => x.invoice_line_id,
                        principalSchema: "sales",
                        principalTable: "sales_invoice_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_articles_company_id_code",
                schema: "sales",
                table: "articles",
                columns: new[] { "company_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_lines_article_id",
                schema: "sales",
                table: "credit_note_lines",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_lines_credit_note_id_line_number",
                schema: "sales",
                table: "credit_note_lines",
                columns: new[] { "credit_note_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_lines_invoice_line_id",
                schema: "sales",
                table: "credit_note_lines",
                column: "invoice_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_note_vat_totals_credit_note_id",
                schema: "sales",
                table: "credit_note_vat_totals",
                column: "credit_note_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_notes_company_id_number",
                schema: "sales",
                table: "credit_notes",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credit_notes_customer_id",
                schema: "sales",
                table: "credit_notes",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_notes_invoice_id_status",
                schema: "sales",
                table: "credit_notes",
                columns: new[] { "invoice_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_credit_notes_reverses_credit_note_id",
                schema: "sales",
                table: "credit_notes",
                column: "reverses_credit_note_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credit_notes_sales_order_id",
                schema: "sales",
                table: "credit_notes",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_company_id_code",
                schema: "sales",
                table: "customers",
                columns: new[] { "company_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_note_lines_article_id",
                schema: "sales",
                table: "delivery_note_lines",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_note_lines_delivery_note_id_line_number",
                schema: "sales",
                table: "delivery_note_lines",
                columns: new[] { "delivery_note_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_note_lines_order_line_id",
                schema: "sales",
                table: "delivery_note_lines",
                column: "order_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_notes_company_id_number",
                schema: "sales",
                table: "delivery_notes",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_notes_customer_id",
                schema: "sales",
                table: "delivery_notes",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_notes_reverses_delivery_note_id",
                schema: "sales",
                table: "delivery_notes",
                column: "reverses_delivery_note_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_notes_sales_order_id_status",
                schema: "sales",
                table: "delivery_notes",
                columns: new[] { "sales_order_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_number_series_company_id_document_type_valid_from",
                schema: "sales",
                table: "number_series",
                columns: new[] { "company_id", "document_type", "valid_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_offer_lines_article_id",
                schema: "sales",
                table: "offer_lines",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_lines_offer_id_line_number",
                schema: "sales",
                table: "offer_lines",
                columns: new[] { "offer_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_offers_company_id_number",
                schema: "sales",
                table: "offers",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_offers_company_id_offer_date",
                schema: "sales",
                table: "offers",
                columns: new[] { "company_id", "offer_date" });

            migrationBuilder.CreateIndex(
                name: "ix_offers_customer_id",
                schema: "sales",
                table: "offers",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_at",
                schema: "sales",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_article_id",
                schema: "sales",
                table: "sales_invoice_lines",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_delivery_line_id",
                schema: "sales",
                table: "sales_invoice_lines",
                column: "delivery_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_invoice_id_line_number",
                schema: "sales",
                table: "sales_invoice_lines",
                columns: new[] { "invoice_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_order_line_id",
                schema: "sales",
                table: "sales_invoice_lines",
                column: "order_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_vat_totals_invoice_id",
                schema: "sales",
                table: "sales_invoice_vat_totals",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_company_id_invoice_date",
                schema: "sales",
                table: "sales_invoices",
                columns: new[] { "company_id", "invoice_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_company_id_number",
                schema: "sales",
                table: "sales_invoices",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_customer_id",
                schema: "sales",
                table: "sales_invoices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_reverses_invoice_id",
                schema: "sales",
                table: "sales_invoices",
                column: "reverses_invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_sales_order_id",
                schema: "sales",
                table: "sales_invoices",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_lines_article_id",
                schema: "sales",
                table: "sales_order_lines",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_lines_order_id_line_number",
                schema: "sales",
                table: "sales_order_lines",
                columns: new[] { "order_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_company_id_number",
                schema: "sales",
                table: "sales_orders",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_company_id_order_date",
                schema: "sales",
                table: "sales_orders",
                columns: new[] { "company_id", "order_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_customer_id",
                schema: "sales",
                table: "sales_orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_offer_id",
                schema: "sales",
                table: "sales_orders",
                column: "offer_id",
                unique: true,
                filter: "offer_id IS NOT NULL AND reverses_order_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_reverses_order_id",
                schema: "sales",
                table: "sales_orders",
                column: "reverses_order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_note_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "credit_note_vat_totals",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "number_counters",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "number_series",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "offer_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_invoice_vat_totals",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_invoice_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "credit_notes",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "delivery_note_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_invoices",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "delivery_notes",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_order_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "articles",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "sales_orders",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "offers",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "sales");
        }
    }
}
