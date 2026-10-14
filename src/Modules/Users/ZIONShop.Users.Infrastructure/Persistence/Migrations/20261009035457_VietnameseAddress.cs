using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZIONShop.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VietnameseAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new Vietnamese-address columns as nullable first so existing rows survive,
            // backfill them from the old columns (+ the owning profile's name/phone), then lock down to NOT NULL.
            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                schema: "users",
                table: "Addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "users",
                table: "Addresses",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Province",
                schema: "users",
                table: "Addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                schema: "users",
                table: "Addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                schema: "users",
                table: "Addresses",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE users.""Addresses"" a
                SET ""StreetAddress"" = trim(a.""Line1"" || coalesce(', ' || a.""Line2"", '')),
                    ""Province"" = a.""City"",
                    ""Ward"" = coalesce(a.""State"", ''),
                    ""RecipientName"" = coalesce(p.""FullName"", ''),
                    ""PhoneNumber"" = coalesce(p.""PhoneNumber"", '')
                FROM users.""UserProfiles"" p
                WHERE p.""Id"" = a.""UserProfileId"";
            ");

            migrationBuilder.AlterColumn<string>(
                name: "RecipientName",
                schema: "users",
                table: "Addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                schema: "users",
                table: "Addresses",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Province",
                schema: "users",
                table: "Addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Ward",
                schema: "users",
                table: "Addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StreetAddress",
                schema: "users",
                table: "Addresses",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "City",
                schema: "users",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "users",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Line1",
                schema: "users",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Line2",
                schema: "users",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "users",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "users",
                table: "Addresses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "users",
                table: "Addresses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "users",
                table: "Addresses",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Line1",
                schema: "users",
                table: "Addresses",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Line2",
                schema: "users",
                table: "Addresses",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "users",
                table: "Addresses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "users",
                table: "Addresses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE users.""Addresses""
                SET ""Line1"" = ""StreetAddress"",
                    ""City"" = ""Province"",
                    ""State"" = ""Ward"",
                    ""Country"" = 'VN',
                    ""PostalCode"" = '';
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Line1",
                schema: "users",
                table: "Addresses",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                schema: "users",
                table: "Addresses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                schema: "users",
                table: "Addresses",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PostalCode",
                schema: "users",
                table: "Addresses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "users",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Province",
                schema: "users",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                schema: "users",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                schema: "users",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Ward",
                schema: "users",
                table: "Addresses");
        }
    }
}
