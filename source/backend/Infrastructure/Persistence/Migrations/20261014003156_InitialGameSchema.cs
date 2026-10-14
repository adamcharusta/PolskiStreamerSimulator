using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolskiStreamerSimulator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialGameSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "game");

            migrationBuilder.CreateTable(
                name: "content_snapshots",
                schema: "game",
                columns: table => new
                {
                    digest = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    balance_json = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    events_json = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_snapshots", x => x.digest);
                });

            migrationBuilder.CreateTable(
                name: "careers",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nickname = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ruleset_id = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    content_digest = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ranked = table.Column<bool>(type: "bit", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    state_version = table.Column<long>(type: "bigint", nullable: false),
                    state_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    state_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    last_action_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_careers", x => x.id);
                    table.ForeignKey(
                        name: "FK_careers_content_snapshots_content_digest",
                        column: x => x.content_digest,
                        principalSchema: "game",
                        principalTable: "content_snapshots",
                        principalColumn: "digest",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "career_actions",
                schema: "game",
                columns: table => new
                {
                    career_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sequence = table.Column<int>(type: "int", nullable: false),
                    command_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    step = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    request_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    response_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    report_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    state_version_after = table.Column<long>(type: "bigint", nullable: false),
                    state_hash_after = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_career_actions", x => new { x.career_id, x.sequence });
                    table.ForeignKey(
                        name: "FK_career_actions_careers_career_id",
                        column: x => x.career_id,
                        principalSchema: "game",
                        principalTable: "careers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "career_secrets",
                schema: "game",
                columns: table => new
                {
                    career_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    secret_hash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_career_secrets", x => x.career_id);
                    table.ForeignKey(
                        name: "FK_career_secrets_careers_career_id",
                        column: x => x.career_id,
                        principalSchema: "game",
                        principalTable: "careers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_career_actions_command",
                schema: "game",
                table: "career_actions",
                columns: new[] { "career_id", "command_id", "step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_careers_content_digest",
                schema: "game",
                table: "careers",
                column: "content_digest");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "career_actions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "career_secrets",
                schema: "game");

            migrationBuilder.DropTable(
                name: "careers",
                schema: "game");

            migrationBuilder.DropTable(
                name: "content_snapshots",
                schema: "game");
        }
    }
}
