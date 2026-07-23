using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdVerdge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerProfession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "profession_id",
                table: "players",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "ck_players_profession_supported",
                table: "players",
                sql: "profession_id IN ('', 'miner', 'mercenary', 'engineer', 'scout', 'mayor')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_players_profession_supported",
                table: "players");

            migrationBuilder.DropColumn(
                name: "profession_id",
                table: "players");
        }
    }
}
