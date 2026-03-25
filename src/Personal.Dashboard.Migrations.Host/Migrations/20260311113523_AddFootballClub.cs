using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Dashboard.Migrations.Host.Migrations
{
    /// <inheritdoc />
    public partial class AddFootballClub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FootballClubEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    LastRefreshed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballClubEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FootballClubAlias",
                columns: table => new
                {
                    Alias = table.Column<string>(type: "text", nullable: false),
                    AliasSource = table.Column<string>(type: "text", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballClubAlias", x => new { x.AliasSource, x.Alias, x.ClubId });
                    table.ForeignKey(
                        name: "FK_FootballClubAlias_FootballClubEntity_ClubId",
                        column: x => x.ClubId,
                        principalTable: "FootballClubEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FootballLeagueClub",
                columns: table => new
                {
                    ClubsId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaguesId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballLeagueClub", x => new { x.ClubsId, x.LeaguesId });
                    table.ForeignKey(
                        name: "FK_FootballLeagueClub_FootballClubEntity_ClubsId",
                        column: x => x.ClubsId,
                        principalTable: "FootballClubEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FootballLeagueClub_FootballLeagueEntity_LeaguesId",
                        column: x => x.LeaguesId,
                        principalTable: "FootballLeagueEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FootballClubAlias_ClubId",
                table: "FootballClubAlias",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_FootballLeagueClub_LeaguesId",
                table: "FootballLeagueClub",
                column: "LeaguesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FootballClubAlias");

            migrationBuilder.DropTable(
                name: "FootballLeagueClub");

            migrationBuilder.DropTable(
                name: "FootballClubEntity");
        }
    }
}
