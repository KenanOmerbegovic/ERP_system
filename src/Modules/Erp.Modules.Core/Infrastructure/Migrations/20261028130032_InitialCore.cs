using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "companies",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vat_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "business_partners",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    vat_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    is_customer = table.Column<bool>(type: "boolean", nullable: false),
                    is_supplier = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_partners", x => x.id);
                    table.ForeignKey(
                        name: "fk_business_partners_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "core",
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_units_of_measure", x => x.id);
                    table.ForeignKey(
                        name: "fk_units_of_measure_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "core",
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "articles",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_of_measure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_articles", x => x.id);
                    table.ForeignKey(
                        name: "fk_articles_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "core",
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_articles_units_of_measure_unit_of_measure_id",
                        column: x => x.unit_of_measure_id,
                        principalSchema: "core",
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_articles_company_id_code",
                schema: "core",
                table: "articles",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_articles_unit_of_measure_id",
                schema: "core",
                table: "articles",
                column: "unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_partners_company_id_code",
                schema: "core",
                table: "business_partners",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_companies_tax_id",
                schema: "core",
                table: "companies",
                column: "tax_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_units_of_measure_company_id_code",
                schema: "core",
                table: "units_of_measure",
                columns: new[] { "company_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "articles",
                schema: "core");

            migrationBuilder.DropTable(
                name: "business_partners",
                schema: "core");

            migrationBuilder.DropTable(
                name: "units_of_measure",
                schema: "core");

            migrationBuilder.DropTable(
                name: "companies",
                schema: "core");
        }
    }
}
