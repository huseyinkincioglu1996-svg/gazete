using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GazeteDagitim.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentPeriodCollectionSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CollectionAmount",
                table: "PaymentPeriods",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CollectionDayOfMonth",
                table: "PaymentPeriods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CollectionTime",
                table: "PaymentPeriods",
                type: "time(0)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentPeriods_CollectionDay",
                table: "PaymentPeriods",
                sql: "[CollectionDayOfMonth] IS NULL OR [CollectionDayOfMonth] BETWEEN 1 AND 31");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentPeriods_CollectionSchedule",
                table: "PaymentPeriods",
                sql: "([CollectionDayOfMonth] IS NULL AND [CollectionTime] IS NULL AND [CollectionAmount] IS NULL) OR ([CollectionDayOfMonth] IS NOT NULL AND [CollectionTime] IS NOT NULL AND [CollectionAmount] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentPeriods_CollectionDay",
                table: "PaymentPeriods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentPeriods_CollectionSchedule",
                table: "PaymentPeriods");

            migrationBuilder.DropColumn(
                name: "CollectionAmount",
                table: "PaymentPeriods");

            migrationBuilder.DropColumn(
                name: "CollectionDayOfMonth",
                table: "PaymentPeriods");

            migrationBuilder.DropColumn(
                name: "CollectionTime",
                table: "PaymentPeriods");
        }
    }
}
