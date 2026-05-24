using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Auth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationAndOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                schema: "auth",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE auth.Users SET EmailConfirmed = 1");

            migrationBuilder.CreateTable(
                name: "AuthOtps",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthOtps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthOtps_Email_Purpose_UsedAt",
                schema: "auth",
                table: "AuthOtps",
                columns: new[] { "Email", "Purpose", "UsedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthOtps",
                schema: "auth");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                schema: "auth",
                table: "Users");
        }
    }
}
