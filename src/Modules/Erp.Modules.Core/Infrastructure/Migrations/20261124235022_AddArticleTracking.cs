using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tracking_type",
                schema: "core",
                table: "articles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tracking_type",
                schema: "core",
                table: "articles");
        }
    }
}
