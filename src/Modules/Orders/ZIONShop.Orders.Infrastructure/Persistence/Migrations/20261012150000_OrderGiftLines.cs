using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderGiftLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_gift",
                schema: "orders",
                table: "OrderLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "gift_promotion_id",
                schema: "orders",
                table: "OrderLines",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_gift",
                schema: "orders",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "gift_promotion_id",
                schema: "orders",
                table: "OrderLines");
        }
    }
}
