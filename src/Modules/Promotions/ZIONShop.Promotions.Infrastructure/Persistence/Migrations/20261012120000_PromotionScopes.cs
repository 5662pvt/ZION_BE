using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Promotions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromotionScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Scope",
                schema: "promotions",
                table: "Promotions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Order");

            migrationBuilder.AddColumn<Guid[]>(
                name: "TargetProductIds",
                schema: "promotions",
                table: "Promotions",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid[]>(
                name: "TargetCategoryIds",
                schema: "promotions",
                table: "Promotions",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "promotions",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "TargetProductIds",
                schema: "promotions",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "TargetCategoryIds",
                schema: "promotions",
                table: "Promotions");
        }
    }
}
