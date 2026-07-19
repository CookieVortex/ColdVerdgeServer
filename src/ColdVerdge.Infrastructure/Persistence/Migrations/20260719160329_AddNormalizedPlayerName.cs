using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdVerdge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedPlayerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_players_user_name",
                table: "players");

            migrationBuilder.AddColumn<string>(
                name: "normalized_user_name",
                table: "players",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_players_normalized_user_name",
                table: "players",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_players_normalized_user_name",
                table: "players");

            migrationBuilder.DropColumn(
                name: "normalized_user_name",
                table: "players");

            migrationBuilder.CreateIndex(
                name: "IX_players_user_name",
                table: "players",
                column: "user_name",
                unique: true);
        }
    }
}
