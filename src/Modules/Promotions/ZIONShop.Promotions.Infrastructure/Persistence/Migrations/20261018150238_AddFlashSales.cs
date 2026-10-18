using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Promotions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlashSaleItems",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlashSaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlashPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AllocatedQuantity = table.Column<int>(type: "integer", nullable: false),
                    SoldQuantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashSaleItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlashSaleRedemptions",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlashSaleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitFlashPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashSaleRedemptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlashSales",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashSales", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlashSaleItems_FlashSaleId",
                schema: "promotions",
                table: "FlashSaleItems",
                column: "FlashSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashSaleItems_FlashSaleId_ProductId",
                schema: "promotions",
                table: "FlashSaleItems",
                columns: new[] { "FlashSaleId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlashSaleItems_ProductId",
                schema: "promotions",
                table: "FlashSaleItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashSaleRedemptions_FlashSaleItemId_OrderId",
                schema: "promotions",
                table: "FlashSaleRedemptions",
                columns: new[] { "FlashSaleItemId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlashSaleRedemptions_Status_CreatedAt",
                schema: "promotions",
                table: "FlashSaleRedemptions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlashSales_IsActive",
                schema: "promotions",
                table: "FlashSales",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlashSaleItems",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "FlashSaleRedemptions",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "FlashSales",
                schema: "promotions");
        }
    }
}
