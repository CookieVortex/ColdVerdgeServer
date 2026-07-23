using ColdVerdge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdVerdge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260723120000_CharacterModelV2")]
public sealed class CharacterModelV2 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "equipment_slot",
            table: "player_inventory_items",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "player_progress",
            columns: table => new
            {
                player_id = table.Column<Guid>(type: "uuid", nullable: false),
                level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                current_experience = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                free_attribute_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                strength = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                endurance = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                agility = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                perception = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                intelligence = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                profession_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: ""),
                profession_experience = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                pistols = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                submachine_guns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                assault_rifles = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                shotguns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                sniper_rifles = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                machine_guns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                throwables = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                medicine = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_player_progress", x => x.player_id);
                table.ForeignKey(
                    name: "fk_player_progress_players_player_id",
                    column: x => x.player_id,
                    principalTable: "players",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO player_progress (player_id, updated_at_utc)
            SELECT id, NOW()
            FROM players
            ON CONFLICT (player_id) DO NOTHING;
            """);

        // Compatibility with pre-v2 databases that stored attributes on players.
        // Intuition is copied to Perception; Survival is intentionally discarded.
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'players' AND column_name = 'intuition'
                ) THEN
                    EXECUTE 'UPDATE player_progress p
                             SET perception = GREATEST(0, legacy.intuition)
                             FROM players legacy
                             WHERE legacy.id = p.player_id';
                    ALTER TABLE players DROP COLUMN intuition;
                END IF;

                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'players' AND column_name = 'survival'
                ) THEN
                    ALTER TABLE players DROP COLUMN survival;
                END IF;
            END $$;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_player_inventory_items_player_id_equipment_slot",
            table: "player_inventory_items",
            columns: new[] { "player_id", "equipment_slot" },
            unique: true,
            filter: "equipment_slot IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "player_progress");
        migrationBuilder.DropIndex(
            name: "ix_player_inventory_items_player_id_equipment_slot",
            table: "player_inventory_items");
        migrationBuilder.DropColumn(
            name: "equipment_slot",
            table: "player_inventory_items");
    }
}
