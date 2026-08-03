using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GazeteDagitim.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RequirePaidPaymentTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_PaidAt",
                table: "Payments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_PaidAt",
                table: "Payments",
                sql: "([Status] = 0 AND [PaidAt] IS NULL) OR ([Status] = 1 AND [PaidAt] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_PaidAt",
                table: "Payments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_PaidAt",
                table: "Payments",
                sql: "([Status] = 0 AND [PaidAt] IS NULL) OR [Status] = 1");
        }
    }
}
