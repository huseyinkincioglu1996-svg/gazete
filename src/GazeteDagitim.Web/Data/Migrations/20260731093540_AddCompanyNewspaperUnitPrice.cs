using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GazeteDagitim.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyNewspaperUnitPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NewspaperUnitPrice",
                table: "CompanySettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompanySettings_NewspaperUnitPrice",
                table: "CompanySettings",
                sql: "[NewspaperUnitPrice] IS NULL OR [NewspaperUnitPrice] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CompanySettings_NewspaperUnitPrice",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "NewspaperUnitPrice",
                table: "CompanySettings");
        }
    }
}
