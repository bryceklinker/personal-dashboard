using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Dashboard.Migrations.Host.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueLastRefreshed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRefreshed",
                table: "FootballLeagueEntity",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRefreshed",
                table: "FootballLeagueEntity");
        }
    }
}
