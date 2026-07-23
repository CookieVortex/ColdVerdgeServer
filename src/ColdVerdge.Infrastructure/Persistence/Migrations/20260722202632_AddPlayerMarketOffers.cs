using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdVerdge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerMarketOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "market_offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    create_request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    item_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    price_copper = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    buyer_player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sold_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_offers", x => x.id);
                    table.CheckConstraint("ck_market_offers_price_positive", "price_copper > 0");
                    table.CheckConstraint("ck_market_offers_status_supported", "status IN ('active', 'sold', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_market_offers_players_seller_player_id",
                        column: x => x.seller_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_market_offers_item_id_status_price_copper_created_at_utc",
                table: "market_offers",
                columns: new[] { "item_id", "status", "price_copper", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_market_offers_seller_player_id_create_request_id",
                table: "market_offers",
                columns: new[] { "seller_player_id", "create_request_id" },
                unique: true);

            // These are regular server-side players and offers. They exercise the same
            // transactional purchase path as offers created by a live player and keep a
            // newly provisioned local database usable before a second client has sold items.
            migrationBuilder.Sql(
                """
                INSERT INTO players (id, user_name, normalized_user_name, created_at_utc)
                VALUES
                    ('10000000-0000-0000-0000-000000000001', 'IRONWOLF',  'IRONWOLF',  CURRENT_TIMESTAMP),
                    ('10000000-0000-0000-0000-000000000002', 'GHOST_7',   'GHOST_7',   CURRENT_TIMESTAMP),
                    ('10000000-0000-0000-0000-000000000003', 'RAZOR',     'RAZOR',     CURRENT_TIMESTAMP),
                    ('10000000-0000-0000-0000-000000000004', 'NOMAD',     'NOMAD',     CURRENT_TIMESTAMP),
                    ('10000000-0000-0000-0000-000000000005', 'BLACKRAVEN','BLACKRAVEN',CURRENT_TIMESTAMP),
                    ('10000000-0000-0000-0000-000000000006', 'WAYFARER',  'WAYFARER',  CURRENT_TIMESTAMP)
                ON CONFLICT DO NOTHING;

                INSERT INTO player_wallets (player_id, gold, copper)
                SELECT id, 0, 0 FROM players
                WHERE id IN (
                    '10000000-0000-0000-0000-000000000001',
                    '10000000-0000-0000-0000-000000000002',
                    '10000000-0000-0000-0000-000000000003',
                    '10000000-0000-0000-0000-000000000004',
                    '10000000-0000-0000-0000-000000000005',
                    '10000000-0000-0000-0000-000000000006')
                ON CONFLICT (player_id) DO NOTHING;

                INSERT INTO market_offers
                    (id, seller_player_id, create_request_id, item_id, price_copper, status, created_at_utc)
                VALUES
                    ('20000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','seed-frontier-1','frontier_a545',175,'active',CURRENT_TIMESTAMP - INTERVAL '2 hours'),
                    ('20000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002','seed-frontier-2','frontier_a545',180,'active',CURRENT_TIMESTAMP - INTERVAL '5 hours'),
                    ('20000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000003','seed-frontier-3','frontier_a545',204,'active',CURRENT_TIMESTAMP - INTERVAL '8 hours'),
                    ('20000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000004','seed-ranger-1','ranger_a556',180,'active',CURRENT_TIMESTAMP - INTERVAL '3 hours'),
                    ('20000000-0000-0000-0000-000000000005','10000000-0000-0000-0000-000000000005','seed-ranger-2','ranger_a556',190,'active',CURRENT_TIMESTAMP - INTERVAL '7 hours'),
                    ('20000000-0000-0000-0000-000000000006','10000000-0000-0000-0000-000000000006','seed-ranger-3','ranger_a556',210,'active',CURRENT_TIMESTAMP - INTERVAL '12 hours'),
                    ('20000000-0000-0000-0000-000000000007','10000000-0000-0000-0000-000000000001','seed-sentinel-1','sentinel_a556',210,'active',CURRENT_TIMESTAMP - INTERVAL '4 hours'),
                    ('20000000-0000-0000-0000-000000000008','10000000-0000-0000-0000-000000000003','seed-sentinel-2','sentinel_a556',220,'active',CURRENT_TIMESTAMP - INTERVAL '9 hours'),
                    ('20000000-0000-0000-0000-000000000009','10000000-0000-0000-0000-000000000005','seed-sentinel-3','sentinel_a556',240,'active',CURRENT_TIMESTAMP - INTERVAL '14 hours'),
                    ('20000000-0000-0000-0000-000000000010','10000000-0000-0000-0000-000000000002','seed-blackhorn-1','blackhorn_c46',85,'active',CURRENT_TIMESTAMP - INTERVAL '2 hours'),
                    ('20000000-0000-0000-0000-000000000011','10000000-0000-0000-0000-000000000004','seed-blackhorn-2','blackhorn_c46',92,'active',CURRENT_TIMESTAMP - INTERVAL '6 hours'),
                    ('20000000-0000-0000-0000-000000000012','10000000-0000-0000-0000-000000000006','seed-blackhorn-3','blackhorn_c46',105,'active',CURRENT_TIMESTAMP - INTERVAL '13 hours'),
                    ('20000000-0000-0000-0000-000000000013','10000000-0000-0000-0000-000000000001','seed-bulldog-1','bulldog_a762',240,'active',CURRENT_TIMESTAMP - INTERVAL '1 hour'),
                    ('20000000-0000-0000-0000-000000000014','10000000-0000-0000-0000-000000000004','seed-bulldog-2','bulldog_a762',255,'active',CURRENT_TIMESTAMP - INTERVAL '10 hours'),
                    ('20000000-0000-0000-0000-000000000015','10000000-0000-0000-0000-000000000005','seed-bulldog-3','bulldog_a762',275,'active',CURRENT_TIMESTAMP - INTERVAL '16 hours'),
                    ('20000000-0000-0000-0000-000000000016','10000000-0000-0000-0000-000000000002','seed-vanguard-1','vanguard_x762',280,'active',CURRENT_TIMESTAMP - INTERVAL '4 hours'),
                    ('20000000-0000-0000-0000-000000000017','10000000-0000-0000-0000-000000000003','seed-vanguard-2','vanguard_x762',295,'active',CURRENT_TIMESTAMP - INTERVAL '11 hours'),
                    ('20000000-0000-0000-0000-000000000018','10000000-0000-0000-0000-000000000006','seed-vanguard-3','vanguard_x762',315,'active',CURRENT_TIMESTAMP - INTERVAL '18 hours'),
                    ('20000000-0000-0000-0000-000000000019','10000000-0000-0000-0000-000000000001','seed-longeye-1','longeye_m762',360,'active',CURRENT_TIMESTAMP - INTERVAL '5 hours'),
                    ('20000000-0000-0000-0000-000000000020','10000000-0000-0000-0000-000000000005','seed-longeye-2','longeye_m762',380,'active',CURRENT_TIMESTAMP - INTERVAL '13 hours'),
                    ('20000000-0000-0000-0000-000000000021','10000000-0000-0000-0000-000000000006','seed-longeye-3','longeye_m762',410,'active',CURRENT_TIMESTAMP - INTERVAL '23 hours'),
                    ('20000000-0000-0000-0000-000000000022','10000000-0000-0000-0000-000000000002','seed-kestrel-1','kestrel_c46',85,'active',CURRENT_TIMESTAMP - INTERVAL '2 hours'),
                    ('20000000-0000-0000-0000-000000000023','10000000-0000-0000-0000-000000000003','seed-kestrel-2','kestrel_c46',90,'active',CURRENT_TIMESTAMP - INTERVAL '8 hours'),
                    ('20000000-0000-0000-0000-000000000024','10000000-0000-0000-0000-000000000004','seed-kestrel-3','kestrel_c46',100,'active',CURRENT_TIMESTAMP - INTERVAL '15 hours'),
                    ('20000000-0000-0000-0000-000000000025','10000000-0000-0000-0000-000000000001','seed-breach-1','breach_m12',130,'active',CURRENT_TIMESTAMP - INTERVAL '3 hours'),
                    ('20000000-0000-0000-0000-000000000026','10000000-0000-0000-0000-000000000004','seed-breach-2','breach_m12',140,'active',CURRENT_TIMESTAMP - INTERVAL '9 hours'),
                    ('20000000-0000-0000-0000-000000000027','10000000-0000-0000-0000-000000000006','seed-breach-3','breach_m12',155,'active',CURRENT_TIMESTAMP - INTERVAL '17 hours'),
                    ('20000000-0000-0000-0000-000000000028','10000000-0000-0000-0000-000000000002','seed-longwatch-1','longwatch_m762',280,'active',CURRENT_TIMESTAMP - INTERVAL '5 hours'),
                    ('20000000-0000-0000-0000-000000000029','10000000-0000-0000-0000-000000000003','seed-longwatch-2','longwatch_m762',300,'active',CURRENT_TIMESTAMP - INTERVAL '12 hours'),
                    ('20000000-0000-0000-0000-000000000030','10000000-0000-0000-0000-000000000005','seed-longwatch-3','longwatch_m762',320,'active',CURRENT_TIMESTAMP - INTERVAL '20 hours'),
                    ('20000000-0000-0000-0000-000000000031','10000000-0000-0000-0000-000000000001','seed-greylock-1','greylock_p9',35,'active',CURRENT_TIMESTAMP - INTERVAL '1 hour'),
                    ('20000000-0000-0000-0000-000000000032','10000000-0000-0000-0000-000000000003','seed-greylock-2','greylock_p9',42,'active',CURRENT_TIMESTAMP - INTERVAL '7 hours'),
                    ('20000000-0000-0000-0000-000000000033','10000000-0000-0000-0000-000000000005','seed-greylock-3','greylock_p9',50,'active',CURRENT_TIMESTAMP - INTERVAL '19 hours'),
                    ('20000000-0000-0000-0000-000000000034','10000000-0000-0000-0000-000000000002','seed-ak-1','ak',180,'active',CURRENT_TIMESTAMP - INTERVAL '4 hours'),
                    ('20000000-0000-0000-0000-000000000035','10000000-0000-0000-0000-000000000004','seed-ak-2','ak',195,'active',CURRENT_TIMESTAMP - INTERVAL '11 hours'),
                    ('20000000-0000-0000-0000-000000000036','10000000-0000-0000-0000-000000000006','seed-ak-3','ak',215,'active',CURRENT_TIMESTAMP - INTERVAL '22 hours')
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "market_offers");

            migrationBuilder.Sql(
                """
                DELETE FROM player_wallets WHERE player_id IN (
                    '10000000-0000-0000-0000-000000000001',
                    '10000000-0000-0000-0000-000000000002',
                    '10000000-0000-0000-0000-000000000003',
                    '10000000-0000-0000-0000-000000000004',
                    '10000000-0000-0000-0000-000000000005',
                    '10000000-0000-0000-0000-000000000006');
                DELETE FROM players WHERE id IN (
                    '10000000-0000-0000-0000-000000000001',
                    '10000000-0000-0000-0000-000000000002',
                    '10000000-0000-0000-0000-000000000003',
                    '10000000-0000-0000-0000-000000000004',
                    '10000000-0000-0000-0000-000000000005',
                    '10000000-0000-0000-0000-000000000006');
                """);
        }
    }
}
