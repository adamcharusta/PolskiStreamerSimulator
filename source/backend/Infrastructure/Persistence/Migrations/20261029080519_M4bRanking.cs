using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolskiStreamerSimulator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4bRanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "season_id",
                schema: "game",
                table: "careers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seasons",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ruleset_id = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    content_digest = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seasons", x => x.id);
                    table.ForeignKey(
                        name: "FK_seasons_content_snapshots_content_digest",
                        column: x => x.content_digest,
                        principalSchema: "game",
                        principalTable: "content_snapshots",
                        principalColumn: "digest",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leaderboard_entries",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    season_id = table.Column<int>(type: "int", nullable: false),
                    career_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nickname = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    score = table.Column<long>(type: "bigint", nullable: false),
                    weeks_played = table.Column<int>(type: "int", nullable: false),
                    difficulty = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    ending = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ruleset_id = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    final_state_version = table.Column<long>(type: "bigint", nullable: false),
                    final_state_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    hidden_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaderboard_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_leaderboard_entries_seasons_season_id",
                        column: x => x.season_id,
                        principalSchema: "game",
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "season_extensions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    season_id = table.Column<int>(type: "int", nullable: false),
                    deadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    actor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_season_extensions", x => x.id);
                    table.ForeignKey(
                        name: "FK_season_extensions_seasons_season_id",
                        column: x => x.season_id,
                        principalSchema: "game",
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_careers_season_id",
                schema: "game",
                table: "careers",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_leaderboard_entries_order",
                schema: "game",
                table: "leaderboard_entries",
                columns: new[] { "season_id", "score", "weeks_played", "submitted_at", "career_id" },
                descending: new[] { false, true, false, false, false },
                filter: "[hidden_at] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_leaderboard_entries_career",
                schema: "game",
                table: "leaderboard_entries",
                column: "career_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_season_extensions_season",
                schema: "game",
                table: "season_extensions",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "IX_seasons_content_digest",
                schema: "game",
                table: "seasons",
                column: "content_digest");

            migrationBuilder.CreateIndex(
                name: "ux_seasons_open",
                schema: "game",
                table: "seasons",
                column: "closed_at",
                unique: true,
                filter: "[closed_at] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_careers_seasons_season_id",
                schema: "game",
                table: "careers",
                column: "season_id",
                principalSchema: "game",
                principalTable: "seasons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_careers_seasons_season_id",
                schema: "game",
                table: "careers");

            migrationBuilder.DropTable(
                name: "leaderboard_entries",
                schema: "game");

            migrationBuilder.DropTable(
                name: "season_extensions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "seasons",
                schema: "game");

            migrationBuilder.DropIndex(
                name: "IX_careers_season_id",
                schema: "game",
                table: "careers");

            migrationBuilder.DropColumn(
                name: "season_id",
                schema: "game",
                table: "careers");
        }
    }
}
