using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolskiStreamerSimulator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4cSaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "restored_from_sequence",
                schema: "game",
                table: "career_actions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "unranked_save_slots",
                schema: "game",
                columns: table => new
                {
                    career_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    slot = table.Column<int>(type: "int", nullable: false),
                    state_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    log_sequence = table.Column<int>(type: "int", nullable: false),
                    saved_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unranked_save_slots", x => new { x.career_id, x.slot });
                    table.CheckConstraint("ck_unranked_save_slots_slot", "[slot] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_unranked_save_slots_careers_career_id",
                        column: x => x.career_id,
                        principalSchema: "game",
                        principalTable: "careers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unranked_save_slots",
                schema: "game");

            migrationBuilder.DropColumn(
                name: "restored_from_sequence",
                schema: "game",
                table: "career_actions");
        }
    }
}
