using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Cart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartItemVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                schema: "cart",
                table: "CartItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantLabel",
                schema: "cart",
                table: "CartItems",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                schema: "cart",
                table: "CartItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Backfill: every existing cart line was keyed by ProductId before variants existed, and every
            // pre-existing product has a default variant whose Id == ProductId (see Products module W0),
            // so VariantId := ProductId is a correct stock/price key for every row already in this table.
            migrationBuilder.Sql(@"UPDATE cart.""CartItems"" SET ""VariantId"" = ""ProductId"";");

            migrationBuilder.AlterColumn<Guid>(
                name: "VariantId",
                schema: "cart",
                table: "CartItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductId",
                schema: "cart",
                table: "CartItems");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_VariantId",
                schema: "cart",
                table: "CartItems",
                columns: new[] { "CartId", "VariantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_VariantId",
                schema: "cart",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "VariantLabel",
                schema: "cart",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "Sku",
                schema: "cart",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "VariantId",
                schema: "cart",
                table: "CartItems");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId",
                schema: "cart",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId" });
        }
    }
}
