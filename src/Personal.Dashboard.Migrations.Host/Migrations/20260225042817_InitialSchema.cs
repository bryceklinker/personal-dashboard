using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Dashboard.Migrations.Host.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FootballLeagueEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballLeagueEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FootballLeagueAlias",
                columns: table => new
                {
                    Alias = table.Column<string>(type: "text", nullable: false),
                    AliasSource = table.Column<string>(type: "text", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballLeagueAlias", x => new { x.AliasSource, x.Alias, x.LeagueId });
                    table.ForeignKey(
                        name: "FK_FootballLeagueAlias_FootballLeagueEntity_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "FootballLeagueEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FootballLeagueAlias_LeagueId",
                table: "FootballLeagueAlias",
                column: "LeagueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FootballLeagueAlias");

            migrationBuilder.DropTable(
                name: "FootballLeagueEntity");
        }
    }
}
