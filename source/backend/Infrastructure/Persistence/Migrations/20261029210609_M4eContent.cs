using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolskiStreamerSimulator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4eContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mechanics_hash",
                schema: "game",
                table: "seasons",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "content_drafts",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    balance_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    events_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    base_number = table.Column<int>(type: "int", nullable: false),
                    revision = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_by = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_drafts", x => x.id);
                    table.CheckConstraint("ck_content_drafts_single", "[id] = 1");
                });

            migrationBuilder.CreateTable(
                name: "content_publications",
                schema: "game",
                columns: table => new
                {
                    number = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    digest = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    mechanics_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    source_number = table.Column<int>(type: "int", nullable: true),
                    actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_publications", x => x.number);
                    table.ForeignKey(
                        name: "FK_content_publications_content_snapshots_digest",
                        column: x => x.digest,
                        principalSchema: "game",
                        principalTable: "content_snapshots",
                        principalColumn: "digest",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_publications_digest",
                schema: "game",
                table: "content_publications",
                columns: new[] { "digest", "kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_drafts",
                schema: "game");

            migrationBuilder.DropTable(
                name: "content_publications",
                schema: "game");

            migrationBuilder.DropColumn(
                name: "mechanics_hash",
                schema: "game",
                table: "seasons");
        }
    }
}
