using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Chat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChatConcurrencyAndKeysetIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No AddColumn for "xmin": it is PostgreSQL's built-in system column on every table already --
            // ALTER TABLE ... ADD COLUMN xmin fails with "column name conflicts with a system column name".
            // Declaring it IsRowVersion() in ConversationConfiguration only tells EF to read/use the
            // existing system column as the optimistic-concurrency token; no DDL is needed for it.
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ConversationId_CreatedAt",
                schema: "chat",
                table: "ChatMessages");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_CreatedAt_Id",
                schema: "chat",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ConversationId_CreatedAt_Id",
                schema: "chat",
                table: "ChatMessages");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_CreatedAt",
                schema: "chat",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "CreatedAt" },
                descending: new[] { false, true });
        }
    }
}
