using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Promotions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromotionGifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinQuantity",
                schema: "promotions",
                table: "Promotions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "promotion_gift_items",
                schema: "promotions",
                columns: table => new
                {
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_gift_items", x => new { x.PromotionId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_promotion_gift_items_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "promotions",
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GiftRedemptions",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftRedemptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiftRedemptions_PromotionId_OrderId",
                schema: "promotions",
                table: "GiftRedemptions",
                columns: new[] { "PromotionId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftRedemptions_PromotionId_UserId_Status",
                schema: "promotions",
                table: "GiftRedemptions",
                columns: new[] { "PromotionId", "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotion_gift_items",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "GiftRedemptions",
                schema: "promotions");

            migrationBuilder.DropColumn(
                name: "MinQuantity",
                schema: "promotions",
                table: "Promotions");
        }
    }
}
