using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdVerdge.Infrastructure.Persistence.Migrations
{
    public partial class AddServerAuthoritativeProgress : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "agility",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "assault_rifles_experience",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "current_experience",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "endurance",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "experience_to_next_level",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "free_attribute_points",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "intelligence",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "level",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "machine_guns_experience",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "medicine_experience",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "perception",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "pistols_experience",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "progress_updated_at_utc",
                table: "players",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "shotguns_experience",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "sniper_rifles_experience",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "strength",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "submachine_guns_experience",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "survival",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "throwables_experience",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "progress_mutations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    payload = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_progress_mutations", x => x.id);
                    table.ForeignKey(
                        name: "FK_progress_mutations_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_players_attributes_non_negative",
                table: "players",
                sql: "strength >= 0 AND endurance >= 0 AND agility >= 0 AND perception >= 0 AND intelligence >= 0 AND survival >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_players_current_experience_non_negative",
                table: "players",
                sql: "current_experience >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_players_experience_to_next_level_positive",
                table: "players",
                sql: "experience_to_next_level >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_players_free_attribute_points_non_negative",
                table: "players",
                sql: "free_attribute_points >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_players_level_positive",
                table: "players",
                sql: "level >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_players_skill_experience_range",
                table: "players",
                sql: "pistols_experience BETWEEN 0 AND 15000 AND submachine_guns_experience BETWEEN 0 AND 15000 AND assault_rifles_experience BETWEEN 0 AND 15000 AND shotguns_experience BETWEEN 0 AND 15000 AND sniper_rifles_experience BETWEEN 0 AND 15000 AND machine_guns_experience BETWEEN 0 AND 15000 AND throwables_experience BETWEEN 0 AND 15000 AND medicine_experience BETWEEN 0 AND 15000");

            migrationBuilder.CreateIndex(
                name: "IX_progress_mutations_player_id_request_id",
                table: "progress_mutations",
                columns: new[] { "player_id", "request_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "progress_mutations");

            migrationBuilder.DropCheckConstraint(name: "ck_players_attributes_non_negative", table: "players");
            migrationBuilder.DropCheckConstraint(name: "ck_players_current_experience_non_negative", table: "players");
            migrationBuilder.DropCheckConstraint(name: "ck_players_experience_to_next_level_positive", table: "players");
            migrationBuilder.DropCheckConstraint(name: "ck_players_free_attribute_points_non_negative", table: "players");
            migrationBuilder.DropCheckConstraint(name: "ck_players_level_positive", table: "players");
            migrationBuilder.DropCheckConstraint(name: "ck_players_skill_experience_range", table: "players");

            string[] columns =
            {
                "agility",
                "assault_rifles_experience",
                "current_experience",
                "endurance",
                "experience_to_next_level",
                "free_attribute_points",
                "intelligence",
                "level",
                "machine_guns_experience",
                "medicine_experience",
                "perception",
                "pistols_experience",
                "progress_updated_at_utc",
                "shotguns_experience",
                "sniper_rifles_experience",
                "strength",
                "submachine_guns_experience",
                "survival",
                "throwables_experience"
            };

            foreach (string column in columns)
                migrationBuilder.DropColumn(name: column, table: "players");
        }
    }
}
