using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GazeteDagitim.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyPaymentPeriodFrequency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentPeriods_DailyDayCount",
                table: "PaymentPeriods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentPeriods_Frequency",
                table: "PaymentPeriods");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentPeriods_Frequency",
                table: "PaymentPeriods",
                sql: "[Frequency] IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentPeriods_FrequencyDayCount",
                table: "PaymentPeriods",
                sql: "([Frequency] <> 1 OR [DayCount] = 1) AND ([Frequency] <> 2 OR [DayCount] = 7)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentPeriods_Frequency",
                table: "PaymentPeriods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentPeriods_FrequencyDayCount",
                table: "PaymentPeriods");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentPeriods_DailyDayCount",
                table: "PaymentPeriods",
                sql: "[Frequency] <> 1 OR [DayCount] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentPeriods_Frequency",
                table: "PaymentPeriods",
                sql: "[Frequency] IN (0, 1)");
        }
    }
}
