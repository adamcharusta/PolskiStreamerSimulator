using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolskiStreamerSimulator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4fTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telemetry_daily",
                schema: "game",
                columns: table => new
                {
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    @event = table.Column<string>(name: "event", type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    props_key = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_daily", x => new { x.day, x.@event, x.props_key });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemetry_daily",
                schema: "game");
        }
    }
}
