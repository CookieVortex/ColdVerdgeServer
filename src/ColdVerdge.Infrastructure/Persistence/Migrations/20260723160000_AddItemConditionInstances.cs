using ColdVerdge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdVerdge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260723160000_AddItemConditionInstances")]
public partial class AddItemConditionInstances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE player_item_instances
            (
                id uuid NOT NULL,
                player_id uuid NOT NULL,
                item_id character varying(64) NOT NULL,
                condition_percent integer NOT NULL DEFAULT 100,
                created_at_utc timestamp with time zone NOT NULL,
                updated_at_utc timestamp with time zone NOT NULL,
                CONSTRAINT pk_player_item_instances PRIMARY KEY (id),
                CONSTRAINT fk_player_item_instances_players
                    FOREIGN KEY (player_id) REFERENCES players (id) ON DELETE CASCADE,
                CONSTRAINT ck_player_item_instances_condition_range
                    CHECK (condition_percent BETWEEN 0 AND 100)
            );

            CREATE INDEX ix_player_item_instances_player_item
                ON player_item_instances (player_id, item_id);

            ALTER TABLE player_equipment_items
                ADD COLUMN item_instance_id uuid NULL;
            ALTER TABLE player_equipment_items
                ADD CONSTRAINT fk_player_equipment_items_item_instance
                    FOREIGN KEY (item_instance_id) REFERENCES player_item_instances (id) ON DELETE RESTRICT;
            CREATE UNIQUE INDEX ux_player_equipment_items_instance
                ON player_equipment_items (item_instance_id)
                WHERE item_instance_id IS NOT NULL;

            ALTER TABLE market_offers
                ADD COLUMN item_instance_id uuid NULL,
                ADD COLUMN condition_percent integer NOT NULL DEFAULT 100;
            ALTER TABLE market_offers
                ADD CONSTRAINT fk_market_offers_item_instance
                    FOREIGN KEY (item_instance_id) REFERENCES player_item_instances (id) ON DELETE RESTRICT,
                ADD CONSTRAINT ck_market_offers_condition_range
                    CHECK (condition_percent BETWEEN 0 AND 100);
            CREATE UNIQUE INDEX ux_market_offers_active_instance
                ON market_offers (item_instance_id)
                WHERE item_instance_id IS NOT NULL AND status = 'active';

            INSERT INTO player_item_instances
                (id, player_id, item_id, condition_percent, created_at_utc, updated_at_utc)
            SELECT
                gen_random_uuid(),
                inventory.player_id,
                inventory.item_id,
                100,
                inventory.created_at_utc,
                inventory.updated_at_utc
            FROM player_inventory_items AS inventory
            CROSS JOIN LATERAL generate_series(1, inventory.quantity)
            WHERE inventory.item_id IN
            (
                'greylock_p9', 'frontier_a545', 'blackhorn_c46', 'sentinel_a556', 'ranger_a556',
                'bulldog_a762', 'vanguard_x762', 'longeye_m762', 'ironclad_mg762',
                'ak', 'raider_helmet', 'raider_vest', 'raider_pants',
                'raider_boots', 'field_backpack'
            );

            UPDATE player_equipment_items AS equipment
            SET item_instance_id =
            (
                SELECT instance.id
                FROM player_item_instances AS instance
                WHERE instance.player_id = equipment.player_id
                  AND instance.item_id = equipment.item_id
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM player_equipment_items AS used
                      WHERE used.item_instance_id = instance.id
                  )
                ORDER BY instance.created_at_utc, instance.id
                LIMIT 1
            );

            INSERT INTO player_item_instances
                (id, player_id, item_id, condition_percent, created_at_utc, updated_at_utc)
            SELECT
                offer.id,
                offer.seller_player_id,
                offer.item_id,
                100,
                offer.created_at_utc,
                offer.created_at_utc
            FROM market_offers AS offer
            WHERE offer.status = 'active'
            ON CONFLICT (id) DO NOTHING;

            UPDATE market_offers AS offer
            SET item_instance_id = offer.id,
                condition_percent = 100
            WHERE offer.status = 'active'
              AND offer.item_instance_id IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS ux_market_offers_active_instance;
            ALTER TABLE market_offers
                DROP CONSTRAINT IF EXISTS fk_market_offers_item_instance,
                DROP CONSTRAINT IF EXISTS ck_market_offers_condition_range,
                DROP COLUMN IF EXISTS item_instance_id,
                DROP COLUMN IF EXISTS condition_percent;

            DROP INDEX IF EXISTS ux_player_equipment_items_instance;
            ALTER TABLE player_equipment_items
                DROP CONSTRAINT IF EXISTS fk_player_equipment_items_item_instance,
                DROP COLUMN IF EXISTS item_instance_id;

            DROP TABLE IF EXISTS player_item_instances;
            """);
    }
}
