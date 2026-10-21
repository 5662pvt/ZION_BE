using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderLines_OrderId_ProductId",
                schema: "orders",
                table: "OrderLines");

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                schema: "orders",
                table: "OrderLines",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                schema: "orders",
                table: "OrderLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantLabel",
                schema: "orders",
                table: "OrderLines",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"orders\".\"OrderLines\" SET \"VariantId\" = \"ProductId\" WHERE \"VariantId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "VariantId",
                schema: "orders",
                table: "OrderLines",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId_VariantId",
                schema: "orders",
                table: "OrderLines",
                columns: new[] { "OrderId", "VariantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderLines_OrderId_VariantId",
                schema: "orders",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "Sku",
                schema: "orders",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "VariantId",
                schema: "orders",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "VariantLabel",
                schema: "orders",
                table: "OrderLines");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId_ProductId",
                schema: "orders",
                table: "OrderLines",
                columns: new[] { "OrderId", "ProductId" });
        }
    }
}
