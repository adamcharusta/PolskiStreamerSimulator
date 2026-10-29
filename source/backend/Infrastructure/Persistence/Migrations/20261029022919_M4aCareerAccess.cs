using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolskiStreamerSimulator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4aCareerAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ended_at",
                schema: "game",
                table: "careers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                schema: "game",
                table: "careers",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Careers stored before M4-A: a finished career's last action is its ending (no action follows it).
            migrationBuilder.Sql(
                """
                UPDATE [game].[careers]
                SET [ended_at] = CASE WHEN [status] = 'Ended' THEN [last_action_at] END,
                    [expires_at] = DATEADD(month, CASE WHEN [status] = 'Ended' THEN 12 ELSE 6 END, [last_action_at]);
                """);

            migrationBuilder.AddColumn<byte[]>(
                name: "creation_hash",
                schema: "game",
                table: "career_secrets",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "creation_request_hash",
                schema: "game",
                table: "career_secrets",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "recovery_hash",
                schema: "game",
                table: "career_secrets",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rotated_at",
                schema: "game",
                table: "career_secrets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_careers_expires_at",
                schema: "game",
                table: "careers",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ux_career_secrets_creation",
                schema: "game",
                table: "career_secrets",
                column: "creation_hash",
                unique: true,
                filter: "[creation_hash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_careers_expires_at",
                schema: "game",
                table: "careers");

            migrationBuilder.DropIndex(
                name: "ux_career_secrets_creation",
                schema: "game",
                table: "career_secrets");

            migrationBuilder.DropColumn(
                name: "ended_at",
                schema: "game",
                table: "careers");

            migrationBuilder.DropColumn(
                name: "expires_at",
                schema: "game",
                table: "careers");

            migrationBuilder.DropColumn(
                name: "creation_hash",
                schema: "game",
                table: "career_secrets");

            migrationBuilder.DropColumn(
                name: "creation_request_hash",
                schema: "game",
                table: "career_secrets");

            migrationBuilder.DropColumn(
                name: "recovery_hash",
                schema: "game",
                table: "career_secrets");

            migrationBuilder.DropColumn(
                name: "rotated_at",
                schema: "game",
                table: "career_secrets");
        }
    }
}
