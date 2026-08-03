using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GazeteDagitim.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriberPaymentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeactivatedAt",
                table: "Subscribers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CollectedAt",
                table: "SubscriberDailyDeliveries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CollectionDayCount",
                table: "SubscriberDailyDeliveries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionPeriodName",
                table: "SubscriberDailyDeliveries",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SubscriberPaymentDeferrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubscriberId = table.Column<int>(type: "int", nullable: false),
                    OriginalDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeferredUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberPaymentDeferrals", x => x.Id);
                    table.CheckConstraint("CK_SubscriberPaymentDeferrals_Dates", "[OriginalDueDate] <= [PreviousDueDate] AND [DeferredUntil] > [PreviousDueDate]");
                    table.ForeignKey(
                        name: "FK_SubscriberPaymentDeferrals_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                UPDATE [Subscribers]
                SET [DeactivatedAt] = [UpdatedAt]
                WHERE [IsActive] = 0 AND [DeactivatedAt] IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscribers_ActivationState",
                table: "Subscribers",
                sql: "([IsActive] = 1 AND [DeactivatedAt] IS NULL) OR ([IsActive] = 0 AND [DeactivatedAt] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriberDailyDeliveries_CollectionSnapshot",
                table: "SubscriberDailyDeliveries",
                sql: "[IsCollected] = 1 OR ([CollectedAt] IS NULL AND [CollectionDayCount] IS NULL AND [CollectionPeriodName] = N'')");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberPaymentDeferrals_History",
                table: "SubscriberPaymentDeferrals",
                columns: new[] { "SubscriberId", "OriginalDueDate", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "UX_SubscriberPaymentDeferrals_Active",
                table: "SubscriberPaymentDeferrals",
                columns: new[] { "SubscriberId", "OriginalDueDate" },
                unique: true,
                filter: "[CancelledAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriberPaymentDeferrals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscribers_ActivationState",
                table: "Subscribers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriberDailyDeliveries_CollectionSnapshot",
                table: "SubscriberDailyDeliveries");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "CollectedAt",
                table: "SubscriberDailyDeliveries");

            migrationBuilder.DropColumn(
                name: "CollectionDayCount",
                table: "SubscriberDailyDeliveries");

            migrationBuilder.DropColumn(
                name: "CollectionPeriodName",
                table: "SubscriberDailyDeliveries");
        }
    }
}
