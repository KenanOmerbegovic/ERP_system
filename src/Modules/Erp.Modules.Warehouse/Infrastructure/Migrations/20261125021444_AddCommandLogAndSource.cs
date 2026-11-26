using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandLogAndSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_document_id",
                schema: "warehouse",
                table: "stock_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_module",
                schema: "warehouse",
                table: "stock_documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "command_log",
                schema: "warehouse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_command_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_command_log_stock_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "warehouse",
                        principalTable: "stock_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_command_log_company_id_source_module_source_document_id_act",
                schema: "warehouse",
                table: "command_log",
                columns: new[] { "company_id", "source_module", "source_document_id", "action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_command_log_document_id",
                schema: "warehouse",
                table: "command_log",
                column: "document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "command_log",
                schema: "warehouse");

            migrationBuilder.DropColumn(
                name: "source_document_id",
                schema: "warehouse",
                table: "stock_documents");

            migrationBuilder.DropColumn(
                name: "source_module",
                schema: "warehouse",
                table: "stock_documents");
        }
    }
}
