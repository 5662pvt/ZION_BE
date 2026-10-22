using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PointsDiscount",
                schema: "orders",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PointsUsed",
                schema: "orders",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsDiscount",
                schema: "orders",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PointsUsed",
                schema: "orders",
                table: "Orders");
        }
    }
}
