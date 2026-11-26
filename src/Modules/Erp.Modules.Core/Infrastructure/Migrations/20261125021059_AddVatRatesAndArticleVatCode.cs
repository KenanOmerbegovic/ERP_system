using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVatRatesAndArticleVatCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_vat_code",
                schema: "core",
                table: "articles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vat_rates",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vat_rates", x => x.id);
                    table.CheckConstraint("ck_vat_rates_rate", "rate >= 0 AND rate <= 1");
                    table.ForeignKey(
                        name: "fk_vat_rates_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "core",
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vat_rates_company_id_code_valid_from",
                schema: "core",
                table: "vat_rates",
                columns: new[] { "company_id", "code", "valid_from" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vat_rates",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "default_vat_code",
                schema: "core",
                table: "articles");
        }
    }
}
