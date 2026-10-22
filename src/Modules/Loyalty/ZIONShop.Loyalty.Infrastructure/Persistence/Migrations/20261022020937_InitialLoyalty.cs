using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Loyalty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "loyalty");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "loyalty",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumer = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.MessageId, x.Consumer });
                });

            migrationBuilder.CreateTable(
                name: "OrderPointsRevocations",
                schema: "loyalty",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPointsRevocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "loyalty",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TraceParent = table.Column<string>(type: "character varying(55)", maxLength: 55, nullable: true),
                    TraceState = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointAccounts",
                schema: "loyalty",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointAccounts", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "PointLots",
                schema: "loyalty",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Original = table.Column<int>(type: "integer", nullable: false),
                    Remaining = table.Column<int>(type: "integer", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointLots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointReservations",
                schema: "loyalty",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointTransactions",
                schema: "loyalty",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointReservationAllocations",
                schema: "loyalty",
                columns: table => new
                {
                    PointReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointReservationAllocations", x => new { x.PointReservationId, x.LotId });
                    table.ForeignKey(
                        name: "FK_PointReservationAllocations_PointReservations_PointReservat~",
                        column: x => x.PointReservationId,
                        principalSchema: "loyalty",
                        principalTable: "PointReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_ProcessedOn",
                schema: "loyalty",
                table: "inbox_messages",
                column: "ProcessedOn");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPointsRevocations_OrderId",
                schema: "loyalty",
                table: "OrderPointsRevocations",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOn",
                schema: "loyalty",
                table: "outbox_messages",
                column: "ProcessedOn");

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_ExpiresAt",
                schema: "loyalty",
                table: "PointLots",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_UserId_Remaining_ExpiresAt",
                schema: "loyalty",
                table: "PointLots",
                columns: new[] { "UserId", "Remaining", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_UserId_Source_SourceRef",
                schema: "loyalty",
                table: "PointLots",
                columns: new[] { "UserId", "Source", "SourceRef" },
                unique: true,
                filter: "\"Source\" IN ('Order', 'Review')");

            migrationBuilder.CreateIndex(
                name: "IX_PointReservations_OrderId",
                schema: "loyalty",
                table: "PointReservations",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_OrderId",
                schema: "loyalty",
                table: "PointTransactions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_UserId_CreatedAt",
                schema: "loyalty",
                table: "PointTransactions",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "OrderPointsRevocations",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "PointAccounts",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "PointLots",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "PointReservationAllocations",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "PointTransactions",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "PointReservations",
                schema: "loyalty");
        }
    }
}
