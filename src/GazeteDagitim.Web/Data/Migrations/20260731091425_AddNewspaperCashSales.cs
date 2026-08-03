using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GazeteDagitim.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNewspaperCashSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewspaperCashSales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DistributorId = table.Column<int>(type: "int", nullable: false),
                    DistributorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewspaperCashSales", x => x.Id);
                    table.CheckConstraint("CK_NewspaperCashSales_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_NewspaperCashSales_CancelledAt", "[CancelledAt] IS NULL OR [CancelledAt] >= [CreatedAt]");
                    table.CheckConstraint("CK_NewspaperCashSales_Quantity", "[Quantity] BETWEEN 1 AND 1000");
                    table.CheckConstraint("CK_NewspaperCashSales_UnitPrice", "[UnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_NewspaperCashSales_Distributors_DistributorId",
                        column: x => x.DistributorId,
                        principalTable: "Distributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewspaperCashSales_Date_Active_Distributor",
                table: "NewspaperCashSales",
                columns: new[] { "Date", "CancelledAt", "DistributorId" });

            migrationBuilder.CreateIndex(
                name: "IX_NewspaperCashSales_DistributorId",
                table: "NewspaperCashSales",
                column: "DistributorId");

            migrationBuilder.CreateIndex(
                name: "UX_NewspaperCashSales_IdempotencyKey",
                table: "NewspaperCashSales",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.Sql(
                """
                DECLARE @databaseName sysname = DB_NAME();
                DECLARE @statement nvarchar(max) =
                    N'ALTER DATABASE ' + QUOTENAME(@databaseName) +
                    N' SET AUTO_CLOSE OFF';
                EXEC sys.sp_executesql @statement;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewspaperCashSales");
        }
    }
}
