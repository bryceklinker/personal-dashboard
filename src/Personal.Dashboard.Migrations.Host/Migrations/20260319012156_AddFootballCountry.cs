using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal.Dashboard.Migrations.Host.Migrations
{
    /// <inheritdoc />
    public partial class AddFootballCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CountryId",
                table: "FootballLeagueEntity",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FootballCountryEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
                    Flag = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballCountryEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FootballCountryAlias",
                columns: table => new
                {
                    Alias = table.Column<string>(type: "text", nullable: false),
                    AliasSource = table.Column<string>(type: "text", nullable: false),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FootballCountryAlias", x => new { x.AliasSource, x.Alias, x.CountryId });
                    table.ForeignKey(
                        name: "FK_FootballCountryAlias_FootballCountryEntity_CountryId",
                        column: x => x.CountryId,
                        principalTable: "FootballCountryEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FootballLeagueEntity_CountryId",
                table: "FootballLeagueEntity",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_FootballCountryAlias_CountryId",
                table: "FootballCountryAlias",
                column: "CountryId");

            migrationBuilder.AddForeignKey(
                name: "FK_FootballLeagueEntity_FootballCountryEntity_CountryId",
                table: "FootballLeagueEntity",
                column: "CountryId",
                principalTable: "FootballCountryEntity",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FootballLeagueEntity_FootballCountryEntity_CountryId",
                table: "FootballLeagueEntity");

            migrationBuilder.DropTable(
                name: "FootballCountryAlias");

            migrationBuilder.DropTable(
                name: "FootballCountryEntity");

            migrationBuilder.DropIndex(
                name: "IX_FootballLeagueEntity_CountryId",
                table: "FootballLeagueEntity");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "FootballLeagueEntity");
        }
    }
}
