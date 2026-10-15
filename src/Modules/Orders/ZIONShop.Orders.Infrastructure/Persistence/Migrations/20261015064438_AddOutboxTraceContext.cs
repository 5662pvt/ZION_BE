using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxTraceContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                schema: "orders",
                table: "outbox_messages",
                type: "character varying(55)",
                maxLength: 55,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceState",
                schema: "orders",
                table: "outbox_messages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TraceParent",
                schema: "orders",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "TraceState",
                schema: "orders",
                table: "outbox_messages");
        }
    }
}
