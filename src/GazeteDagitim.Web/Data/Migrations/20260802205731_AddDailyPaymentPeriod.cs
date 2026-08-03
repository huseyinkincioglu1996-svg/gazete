using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GazeteDagitim.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyPaymentPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PaymentPeriodStartedOn",
                table: "Subscribers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Frequency",
                table: "PaymentPeriods",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentPeriods_DailyDayCount",
                table: "PaymentPeriods",
                sql: "[Frequency] <> 1 OR [DayCount] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentPeriods_Frequency",
                table: "PaymentPeriods",
                sql: "[Frequency] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentPeriods_DailyDayCount",
                table: "PaymentPeriods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentPeriods_Frequency",
                table: "PaymentPeriods");

            migrationBuilder.DropColumn(
                name: "PaymentPeriodStartedOn",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "PaymentPeriods");
        }
    }
}
