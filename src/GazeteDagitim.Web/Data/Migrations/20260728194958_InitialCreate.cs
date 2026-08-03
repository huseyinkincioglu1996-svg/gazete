using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GazeteDagitim.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashHandovers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashHandovers", x => x.Id);
                    table.CheckConstraint("CK_CashHandovers_DeliveredAt", "([Status] = 0 AND [DeliveredAt] IS NULL) OR ([Status] = 1 AND [DeliveredAt] IS NOT NULL)");
                    table.CheckConstraint("CK_CashHandovers_Status", "[Status] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_CashHandovers_Total", "[Total] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Distributors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProfileImageDataUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Zone = table.Column<int>(type: "int", nullable: false),
                    PaymentType = table.Column<int>(type: "int", nullable: false),
                    NewspaperPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 5m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distributors", x => x.Id);
                    table.CheckConstraint("CK_Distributors_NewspaperPrice", "[NewspaperPrice] >= 0");
                    table.CheckConstraint("CK_Distributors_PaymentType", "[PaymentType] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Distributors_ProfileImageLength", "[ProfileImageDataUrl] IS NULL OR LEN([ProfileImageDataUrl]) <= 2796227");
                    table.CheckConstraint("CK_Distributors_Zone", "[Zone] IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "PaymentPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Turkish_CI_AS"),
                    DayCount = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPeriods", x => x.Id);
                    table.CheckConstraint("CK_PaymentPeriods_DayCount", "[DayCount] BETWEEN 1 AND 365");
                });

            migrationBuilder.CreateTable(
                name: "CashHandoverItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashHandoverId = table.Column<int>(type: "int", nullable: false),
                    SubscriberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashHandoverItems", x => x.Id);
                    table.CheckConstraint("CK_CashHandoverItems_Amount", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_CashHandoverItems_CashHandovers_CashHandoverId",
                        column: x => x.CashHandoverId,
                        principalTable: "CashHandovers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SingletonKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "company"),
                    LogoDataUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FeaturedDistributorId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySettings", x => x.Id);
                    table.CheckConstraint("CK_CompanySettings_LogoLength", "[LogoDataUrl] IS NULL OR LEN([LogoDataUrl]) <= 2796227");
                    table.CheckConstraint("CK_CompanySettings_SingletonKey", "[SingletonKey] = N'company'");
                    table.ForeignKey(
                        name: "FK_CompanySettings_Distributors_FeaturedDistributorId",
                        column: x => x.FeaturedDistributorId,
                        principalTable: "Distributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DistributorId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    NewspaperCount = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                    table.CheckConstraint("CK_Deliveries_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Deliveries_Day", "[Day] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_Deliveries_NewspaperCount", "[NewspaperCount] >= 0");
                    table.CheckConstraint("CK_Deliveries_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Deliveries_Distributors_DistributorId",
                        column: x => x.DistributorId,
                        principalTable: "Distributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DistributorDistributionDays",
                columns: table => new
                {
                    DistributorId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributorDistributionDays", x => new { x.DistributorId, x.Day });
                    table.CheckConstraint("CK_DistributorDistributionDays_Day", "[Day] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_DistributorDistributionDays_Distributors_DistributorId",
                        column: x => x.DistributorId,
                        principalTable: "Distributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DistributorMonthlyPaymentDays",
                columns: table => new
                {
                    DistributorId = table.Column<int>(type: "int", nullable: false),
                    DayOfMonth = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributorMonthlyPaymentDays", x => new { x.DistributorId, x.DayOfMonth });
                    table.CheckConstraint("CK_DistributorMonthlyPaymentDays_DayOfMonth", "[DayOfMonth] BETWEEN 1 AND 31");
                    table.ForeignKey(
                        name: "FK_DistributorMonthlyPaymentDays_Distributors_DistributorId",
                        column: x => x.DistributorId,
                        principalTable: "Distributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DistributorWeeklyPaymentDays",
                columns: table => new
                {
                    DistributorId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributorWeeklyPaymentDays", x => new { x.DistributorId, x.Day });
                    table.CheckConstraint("CK_DistributorWeeklyPaymentDays_Day", "[Day] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_DistributorWeeklyPaymentDays_Distributors_DistributorId",
                        column: x => x.DistributorId,
                        principalTable: "Distributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DistributorId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PaymentType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Payments_PaidAt", "([Status] = 0 AND [PaidAt] IS NULL) OR [Status] = 1");
                    table.CheckConstraint("CK_Payments_Period", "[PeriodEnd] >= [PeriodStart]");
                    table.CheckConstraint("CK_Payments_Status", "[Status] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Payments_Type", "[PaymentType] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Payments_Distributors_DistributorId",
                        column: x => x.DistributorId,
                        principalTable: "Distributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subscribers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MonthlyFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PaymentPeriodId = table.Column<int>(type: "int", nullable: true),
                    DistributorId = table.Column<int>(type: "int", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscribers", x => x.Id);
                    table.CheckConstraint("CK_Subscribers_Latitude", "[Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_Subscribers_LocationPair", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL)");
                    table.CheckConstraint("CK_Subscribers_Longitude", "[Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_Subscribers_MonthlyFee", "[MonthlyFee] >= 0");
                    table.ForeignKey(
                        name: "FK_Subscribers_Distributors_DistributorId",
                        column: x => x.DistributorId,
                        principalTable: "Distributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Subscribers_PaymentPeriods_PaymentPeriodId",
                        column: x => x.PaymentPeriodId,
                        principalTable: "PaymentPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriberDailyDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubscriberId = table.Column<int>(type: "int", nullable: false),
                    DistributorId = table.Column<int>(type: "int", nullable: true),
                    DistributorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    NewspaperCount = table.Column<int>(type: "int", nullable: false),
                    IsDelivered = table.Column<bool>(type: "bit", nullable: false),
                    IsCollected = table.Column<bool>(type: "bit", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberDailyDeliveries", x => x.Id);
                    table.CheckConstraint("CK_SubscriberDailyDeliveries_Amount", "[Amount] >= 0 AND ([IsCollected] = 0 OR [Amount] > 0)");
                    table.CheckConstraint("CK_SubscriberDailyDeliveries_NewspaperCount", "[NewspaperCount] IN (1, 2)");
                    table.CheckConstraint("CK_SubscriberDailyDeliveries_PaymentMethod", "[PaymentMethod] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_SubscriberDailyDeliveries_Distributors_DistributorId",
                        column: x => x.DistributorId,
                        principalTable: "Distributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubscriberDailyDeliveries_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriberPublicationDays",
                columns: table => new
                {
                    SubscriberId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberPublicationDays", x => new { x.SubscriberId, x.Day });
                    table.CheckConstraint("CK_SubscriberPublicationDays_Day", "[Day] BETWEEN 0 AND 7");
                    table.ForeignKey(
                        name: "FK_SubscriberPublicationDays_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriberDailyDeliveryCoveredDates",
                columns: table => new
                {
                    SubscriberDailyDeliveryId = table.Column<int>(type: "int", nullable: false),
                    CoveredDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberDailyDeliveryCoveredDates", x => new { x.SubscriberDailyDeliveryId, x.CoveredDate });
                    table.ForeignKey(
                        name: "FK_SubscriberDailyDeliveryCoveredDates_SubscriberDailyDeliveries_SubscriberDailyDeliveryId",
                        column: x => x.SubscriberDailyDeliveryId,
                        principalTable: "SubscriberDailyDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashHandoverItems_CashHandoverId",
                table: "CashHandoverItems",
                column: "CashHandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_CashHandovers_Status_Date",
                table: "CashHandovers",
                columns: new[] { "Status", "Date" });

            migrationBuilder.CreateIndex(
                name: "UX_CashHandovers_Date",
                table: "CashHandovers",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySettings_FeaturedDistributorId",
                table: "CompanySettings",
                column: "FeaturedDistributorId");

            migrationBuilder.CreateIndex(
                name: "UX_CompanySettings_SingletonKey",
                table: "CompanySettings",
                column: "SingletonKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_Date_Status",
                table: "Deliveries",
                columns: new[] { "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Deliveries_Distributor_Date",
                table: "Deliveries",
                columns: new[] { "DistributorId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Distributors_Active_Name",
                table: "Distributors",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPeriods_Active_Name",
                table: "PaymentPeriods",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentPeriods_Name",
                table: "PaymentPeriods",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Date_Status",
                table: "Payments",
                columns: new[] { "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Distributor_Type_PeriodEnd",
                table: "Payments",
                columns: new[] { "DistributorId", "PaymentType", "PeriodEnd" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "UX_Payments_Distributor_Type_Period",
                table: "Payments",
                columns: new[] { "DistributorId", "PaymentType", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberDailyDeliveries_Date_Collected",
                table: "SubscriberDailyDeliveries",
                columns: new[] { "Date", "IsCollected" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberDailyDeliveries_Tracking",
                table: "SubscriberDailyDeliveries",
                columns: new[] { "DistributorId", "Date", "IsCollected", "PaymentMethod" },
                descending: new[] { false, true, false, false });

            migrationBuilder.CreateIndex(
                name: "UX_SubscriberDailyDeliveries_Subscriber_Date",
                table: "SubscriberDailyDeliveries",
                columns: new[] { "SubscriberId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_Active_Name",
                table: "Subscribers",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_DistributorId",
                table: "Subscribers",
                column: "DistributorId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_Name",
                table: "Subscribers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_PaymentPeriodId",
                table: "Subscribers",
                column: "PaymentPeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashHandoverItems");

            migrationBuilder.DropTable(
                name: "CompanySettings");

            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "DistributorDistributionDays");

            migrationBuilder.DropTable(
                name: "DistributorMonthlyPaymentDays");

            migrationBuilder.DropTable(
                name: "DistributorWeeklyPaymentDays");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "SubscriberDailyDeliveryCoveredDates");

            migrationBuilder.DropTable(
                name: "SubscriberPublicationDays");

            migrationBuilder.DropTable(
                name: "CashHandovers");

            migrationBuilder.DropTable(
                name: "SubscriberDailyDeliveries");

            migrationBuilder.DropTable(
                name: "Subscribers");

            migrationBuilder.DropTable(
                name: "Distributors");

            migrationBuilder.DropTable(
                name: "PaymentPeriods");
        }
    }
}
