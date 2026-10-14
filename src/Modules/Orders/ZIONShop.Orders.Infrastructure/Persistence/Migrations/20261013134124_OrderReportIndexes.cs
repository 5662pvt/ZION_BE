using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_Status_At",
                schema: "orders",
                table: "OrderStatusHistories",
                columns: new[] { "Status", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedDate",
                schema: "orders",
                table: "Orders",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                schema: "orders",
                table: "Orders",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderStatusHistories_Status_At",
                schema: "orders",
                table: "OrderStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CreatedDate",
                schema: "orders",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status",
                schema: "orders",
                table: "Orders");
        }
    }
}
