using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdVerdge.Infrastructure.Persistence.Migrations;

public partial class AddServerAuthoritativeInventory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "inventory_mutations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                player_id = table.Column<Guid>(type: "uuid", nullable: false),
                request_id = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                operation = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                item_id = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                quantity_delta = table.Column<int>(
                    type: "integer",
                    nullable: false),
                quantity_after = table.Column<int>(
                    type: "integer",
                    nullable: false),
                equipment_slot = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inventory_mutations", x => x.id);
                table.CheckConstraint(
                    "ck_inventory_mutations_quantity_after_non_negative",
                    "quantity_after >= 0");
                table.ForeignKey(
                    name: "FK_inventory_mutations_players_player_id",
                    column: x => x.player_id,
                    principalTable: "players",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "player_equipment_items",
            columns: table => new
            {
                player_id = table.Column<Guid>(type: "uuid", nullable: false),
                slot = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                item_id = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_player_equipment_items",
                    x => new { x.player_id, x.slot });
                table.ForeignKey(
                    name: "FK_player_equipment_items_players_player_id",
                    column: x => x.player_id,
                    principalTable: "players",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_inventory_mutations_player_id_request_id",
            table: "inventory_mutations",
            columns: new[] { "player_id", "request_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_player_equipment_items_player_id_item_id",
            table: "player_equipment_items",
            columns: new[] { "player_id", "item_id" },
            unique: true);

        migrationBuilder.Sql(
            @"
            WITH target_player AS
            (
                SELECT id
                FROM players
                WHERE id = '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid
                   OR normalized_user_name = 'MAXIM'
                ORDER BY
                    CASE
                        WHEN id = '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid
                            THEN 0
                        ELSE 1
                    END
                LIMIT 1
            ),
            starter_items(id, item_id, quantity) AS
            (
                VALUES
                    ('55f84820-16c9-4685-bcc9-ea547f1bcb01'::uuid, 'ak', 1),
                    ('55f84820-16c9-4685-bcc9-ea547f1bcb02'::uuid, 'raider_helmet', 1),
                    ('55f84820-16c9-4685-bcc9-ea547f1bcb03'::uuid, 'raider_vest', 1),
                    ('55f84820-16c9-4685-bcc9-ea547f1bcb04'::uuid, 'raider_pants', 1),
                    ('55f84820-16c9-4685-bcc9-ea547f1bcb05'::uuid, 'raider_boots', 1),
                    ('55f84820-16c9-4685-bcc9-ea547f1bcb06'::uuid, 'field_backpack', 1),
                    ('55f84820-16c9-4685-bcc9-ea547f1bcb07'::uuid, 'bandage', 30),
                    ('55f84820-16c9-4685-bcc9-ea547f1bcb08'::uuid, 'ammo_762x51', 3000)
            )
            INSERT INTO player_inventory_items
            (
                id,
                player_id,
                item_id,
                quantity,
                created_at_utc,
                updated_at_utc
            )
            SELECT
                starter_items.id,
                target_player.id,
                starter_items.item_id,
                starter_items.quantity,
                CURRENT_TIMESTAMP,
                CURRENT_TIMESTAMP
            FROM target_player
            CROSS JOIN starter_items
            ON CONFLICT (player_id, item_id)
            DO UPDATE SET
                quantity = GREATEST(
                    player_inventory_items.quantity,
                    EXCLUDED.quantity),
                updated_at_utc = CURRENT_TIMESTAMP;
            ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "inventory_mutations");
        migrationBuilder.DropTable(name: "player_equipment_items");
    }
}
