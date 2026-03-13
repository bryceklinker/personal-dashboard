using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Dashboard.Migrations.Host.Migrations
{
    /// <inheritdoc />
    public partial class AddFootballLeagueSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentSeasonYear",
                table: "FootballLeagueEntity");

            migrationBuilder.CreateTable(
                name: "FootballLeagueSeason",
                columns: table => new
                {
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballLeagueSeason", x => new { x.LeagueId, x.Year });
                    table.ForeignKey(
                        name: "FK_FootballLeagueSeason_FootballLeagueEntity_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "FootballLeagueEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FootballLeagueSeason");

            migrationBuilder.AddColumn<int>(
                name: "CurrentSeasonYear",
                table: "FootballLeagueEntity",
                type: "integer",
                nullable: true);
        }
    }
}
