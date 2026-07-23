using ColdVerdge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdVerdge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20260723190000_RemoveRetiredWeapons")]
public partial class RemoveRetiredWeapons : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM market_offers
            WHERE item_id IN ('kestrel_c46', 'breach_m12', 'longwatch_m762');

            DELETE FROM player_equipment_items
            WHERE item_id IN ('kestrel_c46', 'breach_m12', 'longwatch_m762');

            DELETE FROM player_item_instances
            WHERE item_id IN ('kestrel_c46', 'breach_m12', 'longwatch_m762');

            DELETE FROM player_inventory_items
            WHERE item_id IN ('kestrel_c46', 'breach_m12', 'longwatch_m762');

            DELETE FROM inventory_grants
            WHERE item_id IN ('kestrel_c46', 'breach_m12', 'longwatch_m762');

            DELETE FROM inventory_mutations
            WHERE item_id IN ('kestrel_c46', 'breach_m12', 'longwatch_m762');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Removed player-owned data cannot be reconstructed safely.
    }
}
