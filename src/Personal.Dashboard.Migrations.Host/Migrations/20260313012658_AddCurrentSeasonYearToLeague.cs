using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Dashboard.Migrations.Host.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentSeasonYearToLeague : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentSeasonYear",
                table: "FootballLeagueEntity",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentSeasonYear",
                table: "FootballLeagueEntity");
        }
    }
}
