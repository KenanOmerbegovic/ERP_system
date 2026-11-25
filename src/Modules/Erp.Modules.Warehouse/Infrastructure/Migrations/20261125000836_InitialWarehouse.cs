using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "warehouse");

            migrationBuilder.CreateTable(
                name: "articles",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_goods = table.Column<bool>(type: "boolean", nullable: false),
                    tracking = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_of_measure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_articles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "number_counters",
                schema: "warehouse",
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
                schema: "warehouse",
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
                schema: "warehouse",
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
                name: "warehouses",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lots",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lots", x => x.id);
                    table.ForeignKey(
                        name: "fk_lots_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "warehouse",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "serial_numbers",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_serial_numbers", x => x.id);
                    table.ForeignKey(
                        name: "fk_serial_numbers_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "warehouse",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_serial_numbers_storehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_documents",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    source_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reverses_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_documents_stock_documents_reverses_document_id",
                        column: x => x.reverses_document_id,
                        principalSchema: "warehouse",
                        principalTable: "stock_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_documents_storehouses_source_warehouse_id",
                        column: x => x.source_warehouse_id,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_documents_storehouses_target_warehouse_id",
                        column: x => x.target_warehouse_id,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_items", x => x.id);
                    table.CheckConstraint("ck_stock_items_quantity_not_negative", "quantity >= 0");
                    table.CheckConstraint("ck_stock_items_value_not_negative", "value >= 0");
                    table.ForeignKey(
                        name: "fk_stock_items_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "warehouse",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_items_storehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lot_balances",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lot_balances", x => x.id);
                    table.CheckConstraint("ck_lot_balances_quantity_not_negative", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_lot_balances_lots_lot_id",
                        column: x => x.lot_id,
                        principalSchema: "warehouse",
                        principalTable: "lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lot_balances_storehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_document_lines",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    lot_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lot_expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    serial_numbers = table.Column<List<string>>(type: "text[]", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_document_lines", x => x.id);
                    table.CheckConstraint("ck_stock_document_lines_quantity_positive", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_stock_document_lines_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "warehouse",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_document_lines_stock_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "warehouse",
                        principalTable: "stock_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_movements_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "warehouse",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_lots_lot_id",
                        column: x => x.lot_id,
                        principalSchema: "warehouse",
                        principalTable: "lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_stock_document_lines_document_line_id",
                        column: x => x.document_line_id,
                        principalSchema: "warehouse",
                        principalTable: "stock_document_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_stock_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "warehouse",
                        principalTable: "stock_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_storehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_articles_company_id_code",
                schema: "warehouse",
                table: "articles",
                columns: new[] { "company_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_lot_balances_company_id_warehouse_id_lot_id",
                schema: "warehouse",
                table: "lot_balances",
                columns: new[] { "company_id", "warehouse_id", "lot_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lot_balances_lot_id",
                schema: "warehouse",
                table: "lot_balances",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_lot_balances_warehouse_id",
                schema: "warehouse",
                table: "lot_balances",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_lots_article_id",
                schema: "warehouse",
                table: "lots",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_lots_company_id_article_id_number",
                schema: "warehouse",
                table: "lots",
                columns: new[] { "company_id", "article_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_number_series_company_id_document_type_valid_from",
                schema: "warehouse",
                table: "number_series",
                columns: new[] { "company_id", "document_type", "valid_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_at",
                schema: "warehouse",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_serial_numbers_article_id",
                schema: "warehouse",
                table: "serial_numbers",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_serial_numbers_company_id_article_id_number",
                schema: "warehouse",
                table: "serial_numbers",
                columns: new[] { "company_id", "article_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_serial_numbers_warehouse_id",
                schema: "warehouse",
                table: "serial_numbers",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_document_lines_article_id",
                schema: "warehouse",
                table: "stock_document_lines",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_document_lines_document_id_line_number",
                schema: "warehouse",
                table: "stock_document_lines",
                columns: new[] { "document_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_company_id_document_date",
                schema: "warehouse",
                table: "stock_documents",
                columns: new[] { "company_id", "document_date" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_company_id_number",
                schema: "warehouse",
                table: "stock_documents",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_reverses_document_id",
                schema: "warehouse",
                table: "stock_documents",
                column: "reverses_document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_source_warehouse_id",
                schema: "warehouse",
                table: "stock_documents",
                column: "source_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_documents_target_warehouse_id",
                schema: "warehouse",
                table: "stock_documents",
                column: "target_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_article_id",
                schema: "warehouse",
                table: "stock_items",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_company_id_warehouse_id_article_id",
                schema: "warehouse",
                table: "stock_items",
                columns: new[] { "company_id", "warehouse_id", "article_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_warehouse_id",
                schema: "warehouse",
                table: "stock_items",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_article_id",
                schema: "warehouse",
                table: "stock_movements",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_company_id_warehouse_id_article_id",
                schema: "warehouse",
                table: "stock_movements",
                columns: new[] { "company_id", "warehouse_id", "article_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_document_id",
                schema: "warehouse",
                table: "stock_movements",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_document_line_id",
                schema: "warehouse",
                table: "stock_movements",
                column: "document_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_lot_id",
                schema: "warehouse",
                table: "stock_movements",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_warehouse_id",
                schema: "warehouse",
                table: "stock_movements",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_company_id_code",
                schema: "warehouse",
                table: "warehouses",
                columns: new[] { "company_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lot_balances",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "number_counters",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "number_series",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "serial_numbers",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "stock_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "lots",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "stock_document_lines",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "articles",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "stock_documents",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "warehouse");
        }
    }
}
