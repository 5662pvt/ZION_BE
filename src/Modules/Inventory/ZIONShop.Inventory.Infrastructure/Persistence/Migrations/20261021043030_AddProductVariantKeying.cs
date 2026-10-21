using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariantKeying : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockReservations_OrderId_ProductId",
                schema: "inventory",
                table: "StockReservations");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductId_CreatedAt",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_ProductId",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                schema: "inventory",
                table: "StockReservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                schema: "inventory",
                table: "StockMovements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                schema: "inventory",
                table: "InventoryItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "VariantLabel",
                schema: "inventory",
                table: "InventoryItems",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            // Backfill: every pre-existing product (and every product created without variants) has exactly
            // one DEFAULT variant whose Id == ProductId, so VariantId := ProductId is correct here with no
            // lookup needed. Must run before the unique indexes below are created, since the default value
            // EF used for the NOT NULL columns above (an all-zero GUID) would collide across every row.
            migrationBuilder.Sql("UPDATE \"inventory\".\"StockReservations\" SET \"VariantId\" = \"ProductId\";");
            migrationBuilder.Sql("UPDATE \"inventory\".\"StockMovements\" SET \"VariantId\" = \"ProductId\";");
            migrationBuilder.Sql("UPDATE \"inventory\".\"InventoryItems\" SET \"VariantId\" = \"ProductId\";");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_OrderId_VariantId",
                schema: "inventory",
                table: "StockReservations",
                columns: new[] { "OrderId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_VariantId_CreatedAt",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "VariantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ProductId",
                schema: "inventory",
                table: "InventoryItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_VariantId",
                schema: "inventory",
                table: "InventoryItems",
                column: "VariantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOT SAFELY REVERSIBLE once any product has more than one variant: this recreates the old
            // unique index on (OrderId/ProductId)/ProductId alone, which will fail (or silently collapse
            // rows) as soon as several VariantId rows share the same ProductId. Only run Down while every
            // product still has exactly one (default) variant.
            migrationBuilder.DropIndex(
                name: "IX_StockReservations_OrderId_VariantId",
                schema: "inventory",
                table: "StockReservations");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_VariantId_CreatedAt",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_ProductId",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_VariantId",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "VariantId",
                schema: "inventory",
                table: "StockReservations");

            migrationBuilder.DropColumn(
                name: "VariantId",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "VariantId",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "VariantLabel",
                schema: "inventory",
                table: "InventoryItems");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_OrderId_ProductId",
                schema: "inventory",
                table: "StockReservations",
                columns: new[] { "OrderId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId_CreatedAt",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ProductId",
                schema: "inventory",
                table: "InventoryItems",
                column: "ProductId",
                unique: true);
        }
    }
}
