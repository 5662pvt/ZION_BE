using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Promotions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FlashSaleItems_FlashSaleId_ProductId",
                schema: "promotions",
                table: "FlashSaleItems");

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                schema: "promotions",
                table: "FlashSaleItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE promotions.\"FlashSaleItems\" SET \"VariantId\" = \"ProductId\" WHERE \"VariantId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "VariantId",
                schema: "promotions",
                table: "FlashSaleItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlashSaleItems_VariantId",
                schema: "promotions",
                table: "FlashSaleItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashSaleItems_FlashSaleId_VariantId",
                schema: "promotions",
                table: "FlashSaleItems",
                columns: new[] { "FlashSaleId", "VariantId" },
                unique: true);

            migrationBuilder.DropPrimaryKey(
                name: "PK_promotion_gift_items",
                schema: "promotions",
                table: "promotion_gift_items");

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                schema: "promotions",
                table: "promotion_gift_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE promotions.promotion_gift_items SET \"VariantId\" = \"ProductId\" WHERE \"VariantId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "VariantId",
                schema: "promotions",
                table: "promotion_gift_items",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_promotion_gift_items",
                schema: "promotions",
                table: "promotion_gift_items",
                columns: new[] { "PromotionId", "VariantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_promotion_gift_items",
                schema: "promotions",
                table: "promotion_gift_items");

            migrationBuilder.DropColumn(
                name: "VariantId",
                schema: "promotions",
                table: "promotion_gift_items");

            migrationBuilder.AddPrimaryKey(
                name: "PK_promotion_gift_items",
                schema: "promotions",
                table: "promotion_gift_items",
                columns: new[] { "PromotionId", "ProductId" });

            migrationBuilder.DropIndex(
                name: "IX_FlashSaleItems_FlashSaleId_VariantId",
                schema: "promotions",
                table: "FlashSaleItems");

            migrationBuilder.DropIndex(
                name: "IX_FlashSaleItems_VariantId",
                schema: "promotions",
                table: "FlashSaleItems");

            migrationBuilder.DropColumn(
                name: "VariantId",
                schema: "promotions",
                table: "FlashSaleItems");

            migrationBuilder.CreateIndex(
                name: "IX_FlashSaleItems_FlashSaleId_ProductId",
                schema: "promotions",
                table: "FlashSaleItems",
                columns: new[] { "FlashSaleId", "ProductId" },
                unique: true);
        }
    }
}
