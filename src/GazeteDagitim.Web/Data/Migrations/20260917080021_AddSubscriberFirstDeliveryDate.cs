using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GazeteDagitim.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriberFirstDeliveryDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FirstDeliveryDate",
                table: "Subscribers",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [Subscribers]
                SET [FirstDeliveryDate] = COALESCE(
                    [PaymentPeriodStartedOn],
                    CONVERT(date, SWITCHOFFSET([CreatedAt], '+03:00')))
                WHERE [FirstDeliveryDate] IS NULL;
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "FirstDeliveryDate",
                table: "Subscribers",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstDeliveryDate",
                table: "Subscribers");
        }
    }
}
