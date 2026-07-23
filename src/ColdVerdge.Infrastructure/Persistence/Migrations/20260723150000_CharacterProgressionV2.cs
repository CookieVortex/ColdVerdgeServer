using ColdVerdge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdVerdge.Infrastructure.Persistence.Migrations;

 [DbContext(typeof(GameDbContext))]
 [Migration("20260723150000_CharacterProgressionV2")]
public partial class CharacterProgressionV2 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve every earned point: the removed Survival value becomes spendable.
        // Older experimental databases that still contain "intuition" are normalized too.
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'players' AND column_name = 'intuition'
                ) THEN
                    UPDATE players
                    SET perception = GREATEST(perception, intuition);
                    ALTER TABLE players DROP COLUMN intuition;
                END IF;

                ALTER TABLE players DROP CONSTRAINT IF EXISTS ck_players_attributes_non_negative;

                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'players' AND column_name = 'survival'
                ) THEN
                    UPDATE players
                    SET free_attribute_points = free_attribute_points + GREATEST(survival, 0);
                    ALTER TABLE players DROP COLUMN survival;
                END IF;
            END $$;

            ALTER TABLE players
                ADD COLUMN IF NOT EXISTS profession_play_seconds bigint NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS profession_last_heartbeat_at_utc timestamp with time zone NULL;

            ALTER TABLE players DROP CONSTRAINT IF EXISTS ck_players_profession_play_seconds_range;
            ALTER TABLE players
                ADD CONSTRAINT ck_players_attributes_non_negative
                    CHECK (strength >= 0 AND endurance >= 0 AND agility >= 0 AND perception >= 0 AND intelligence >= 0),
                ADD CONSTRAINT ck_players_profession_play_seconds_range
                    CHECK (profession_play_seconds BETWEEN 0 AND 360000);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE players
                DROP CONSTRAINT IF EXISTS ck_players_profession_play_seconds_range,
                DROP CONSTRAINT IF EXISTS ck_players_attributes_non_negative,
                DROP COLUMN IF EXISTS profession_last_heartbeat_at_utc,
                DROP COLUMN IF EXISTS profession_play_seconds;

            ALTER TABLE players ADD COLUMN survival integer NOT NULL DEFAULT 0;
            ALTER TABLE players
                ADD CONSTRAINT ck_players_attributes_non_negative
                    CHECK (strength >= 0 AND endurance >= 0 AND agility >= 0 AND perception >= 0 AND intelligence >= 0 AND survival >= 0);
            """);
    }
}
