using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZIONShop.Products.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductOptions",
                schema: "products",
                columns: table => new
                {
                    Position = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Values = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptions", x => new { x.ProductId, x.Position });
                    table.ForeignKey(
                        name: "FK_ProductOptions_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "products",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                schema: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Option1Value = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Option2Value = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "products",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                schema: "products",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Sku",
                schema: "products",
                table: "ProductVariants",
                column: "Sku",
                unique: true);

            // Backfill: one variant per existing product, Id = ProductId (KEY TRICK — keeps every
            // downstream row already keyed by ProductId a valid VariantId). Must run while the old
            // Products.Sku/Price/Currency columns still exist.
            migrationBuilder.Sql(@"
                INSERT INTO products.""ProductVariants"" (""Id"", ""ProductId"", ""Sku"", ""Price"", ""Currency"", ""Option1Value"", ""Option2Value"", ""ImageId"", ""IsActive"", ""DisplayOrder"")
                SELECT ""Id"", ""Id"", ""Sku"", ""Price"", ""Currency"", NULL, NULL, NULL, TRUE, 0
                FROM products.""Products"";
            ");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                schema: "products",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "products",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sku",
                schema: "products",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "Price",
                schema: "products",
                table: "Products",
                newName: "MinPrice");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxPrice",
                schema: "products",
                table: "Products",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Every existing product has exactly one variant at this point, so MaxPrice == MinPrice.
            migrationBuilder.Sql(@"UPDATE products.""Products"" SET ""MaxPrice"" = ""MinPrice"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MinPrice",
                schema: "products",
                table: "Products",
                newName: "Price");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "products",
                table: "Products",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                schema: "products",
                table: "Products",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Restore Sku/Price/Currency from each product's default variant (Id == ProductId), while the
            // ProductVariants table still exists. Products whose default variant was replaced by the
            // variants endpoint (no row with Id == ProductId left) keep the empty defaults above.
            migrationBuilder.Sql(@"
                UPDATE products.""Products"" p
                SET ""Sku"" = v.""Sku"", ""Price"" = v.""Price"", ""Currency"" = v.""Currency""
                FROM products.""ProductVariants"" v
                WHERE v.""Id"" = p.""Id"";
            ");

            migrationBuilder.DropColumn(
                name: "MaxPrice",
                schema: "products",
                table: "Products");

            migrationBuilder.DropTable(
                name: "ProductOptions",
                schema: "products");

            migrationBuilder.DropTable(
                name: "ProductVariants",
                schema: "products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                schema: "products",
                table: "Products",
                column: "Sku",
                unique: true);
        }
    }
}
