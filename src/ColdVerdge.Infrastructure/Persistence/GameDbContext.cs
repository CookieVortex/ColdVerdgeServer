using ColdVerdge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ColdVerdge.Infrastructure.Persistence;

public sealed class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerWallet> PlayerWallets => Set<PlayerWallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<PlayerInventoryItem> PlayerInventoryItems => Set<PlayerInventoryItem>();
    public DbSet<PlayerItemInstance> PlayerItemInstances => Set<PlayerItemInstance>();
    public DbSet<PlayerEquipmentItem> PlayerEquipmentItems => Set<PlayerEquipmentItem>();
    public DbSet<InventoryGrant> InventoryGrants => Set<InventoryGrant>();
    public DbSet<InventoryMutation> InventoryMutations => Set<InventoryMutation>();
    public DbSet<ProgressMutation> ProgressMutations => Set<ProgressMutation>();
    public DbSet<MarketOffer> MarketOffers => Set<MarketOffer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurePlayer(modelBuilder);
        ConfigurePlayerWallet(modelBuilder);
        ConfigureWalletTransaction(modelBuilder);
        ConfigurePlayerInventoryItem(modelBuilder);
        ConfigurePlayerItemInstance(modelBuilder);
        ConfigurePlayerEquipmentItem(modelBuilder);
        ConfigureInventoryGrant(modelBuilder);
        ConfigureInventoryMutation(modelBuilder);
        ConfigureProgressMutation(modelBuilder);
        ConfigureMarketOffer(modelBuilder);
    }

    private static void ConfigurePlayer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Player>();

        entity.HasKey(player => player.Id);

        entity.Property(player => player.Id).HasColumnName("id");
        entity.Property(player => player.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(32)
            .IsRequired();
        entity.Property(player => player.NormalizedUserName)
            .HasColumnName("normalized_user_name")
            .HasMaxLength(32)
            .IsRequired();
        entity.Property(player => player.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
        entity.Property(player => player.Level)
            .HasColumnName("level")
            .HasDefaultValue(1)
            .IsRequired();
        entity.Property(player => player.CurrentExperience)
            .HasColumnName("current_experience")
            .HasDefaultValue(0)
            .IsRequired();
        entity.Property(player => player.ExperienceToNextLevel)
            .HasColumnName("experience_to_next_level")
            .HasDefaultValue(100)
            .IsRequired();
        entity.Property(player => player.FreeAttributePoints)
            .HasColumnName("free_attribute_points")
            .HasDefaultValue(5)
            .IsRequired();
        entity.Property(player => player.Strength).HasColumnName("strength").HasDefaultValue(10).IsRequired();
        entity.Property(player => player.Endurance).HasColumnName("endurance").HasDefaultValue(10).IsRequired();
        entity.Property(player => player.Agility).HasColumnName("agility").HasDefaultValue(10).IsRequired();
        entity.Property(player => player.Perception).HasColumnName("perception").HasDefaultValue(10).IsRequired();
        entity.Property(player => player.Intelligence).HasColumnName("intelligence").HasDefaultValue(10).IsRequired();
        entity.Property(player => player.PistolsExperience).HasColumnName("pistols_experience").HasDefaultValue(0).IsRequired();
        entity.Property(player => player.SubmachineGunsExperience).HasColumnName("submachine_guns_experience").HasDefaultValue(0).IsRequired();
        entity.Property(player => player.AssaultRiflesExperience).HasColumnName("assault_rifles_experience").HasDefaultValue(0).IsRequired();
        entity.Property(player => player.ShotgunsExperience).HasColumnName("shotguns_experience").HasDefaultValue(0).IsRequired();
        entity.Property(player => player.SniperRiflesExperience).HasColumnName("sniper_rifles_experience").HasDefaultValue(0).IsRequired();
        entity.Property(player => player.MachineGunsExperience).HasColumnName("machine_guns_experience").HasDefaultValue(0).IsRequired();
        entity.Property(player => player.ThrowablesExperience).HasColumnName("throwables_experience").HasDefaultValue(0).IsRequired();
        entity.Property(player => player.MedicineExperience).HasColumnName("medicine_experience").HasDefaultValue(0).IsRequired();
        entity.Property(player => player.ProfessionId)
            .HasColumnName("profession_id")
            .HasMaxLength(32)
            .HasDefaultValue(string.Empty)
            .IsRequired();
        entity.Property(player => player.ProfessionPlaySeconds)
            .HasColumnName("profession_play_seconds")
            .HasDefaultValue(0L)
            .IsRequired();
        entity.Property(player => player.ProfessionLastHeartbeatAtUtc)
            .HasColumnName("profession_last_heartbeat_at_utc");
        entity.Property(player => player.ProgressUpdatedAtUtc)
            .HasColumnName("progress_updated_at_utc")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        entity.ToTable(
            "players",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_players_level_positive", "level >= 1");
                tableBuilder.HasCheckConstraint("ck_players_current_experience_non_negative", "current_experience >= 0");
                tableBuilder.HasCheckConstraint("ck_players_experience_to_next_level_positive", "experience_to_next_level >= 1");
                tableBuilder.HasCheckConstraint("ck_players_free_attribute_points_non_negative", "free_attribute_points >= 0");
                tableBuilder.HasCheckConstraint("ck_players_attributes_non_negative", "strength >= 0 AND endurance >= 0 AND agility >= 0 AND perception >= 0 AND intelligence >= 0");
                tableBuilder.HasCheckConstraint("ck_players_skill_experience_range", "pistols_experience BETWEEN 0 AND 15000 AND submachine_guns_experience BETWEEN 0 AND 15000 AND assault_rifles_experience BETWEEN 0 AND 15000 AND shotguns_experience BETWEEN 0 AND 15000 AND sniper_rifles_experience BETWEEN 0 AND 15000 AND machine_guns_experience BETWEEN 0 AND 15000 AND throwables_experience BETWEEN 0 AND 15000 AND medicine_experience BETWEEN 0 AND 15000");
                tableBuilder.HasCheckConstraint("ck_players_profession_supported", "profession_id IN ('', 'miner', 'mercenary', 'engineer', 'scout', 'mayor')");
                tableBuilder.HasCheckConstraint("ck_players_profession_play_seconds_range", "profession_play_seconds BETWEEN 0 AND 360000");
            });

        entity.HasIndex(player => player.NormalizedUserName).IsUnique();

        entity.HasOne(player => player.Wallet)
            .WithOne(wallet => wallet.Player)
            .HasForeignKey<PlayerWallet>(wallet => wallet.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePlayerWallet(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PlayerWallet>();

        entity.ToTable(
            "player_wallets",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_player_wallets_gold_non_negative",
                    "gold >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_player_wallets_copper_non_negative",
                    "copper >= 0");
            });

        entity.HasKey(wallet => wallet.PlayerId);
        entity.Property(wallet => wallet.PlayerId).HasColumnName("player_id");
        entity.Property(wallet => wallet.Gold)
            .HasColumnName("gold")
            .HasDefaultValue(0L)
            .IsRequired();
        entity.Property(wallet => wallet.Copper)
            .HasColumnName("copper")
            .HasDefaultValue(0L)
            .IsRequired();
    }

    private static void ConfigureWalletTransaction(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WalletTransaction>();

        entity.ToTable("wallet_transactions");
        entity.HasKey(transaction => transaction.Id);

        entity.Property(transaction => transaction.Id).HasColumnName("id");
        entity.Property(transaction => transaction.PlayerId).HasColumnName("player_id");
        entity.Property(transaction => transaction.RequestId)
            .HasColumnName("request_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(transaction => transaction.Currency)
            .HasColumnName("currency")
            .HasMaxLength(16)
            .IsRequired();
        entity.Property(transaction => transaction.Amount)
            .HasColumnName("amount")
            .IsRequired();
        entity.Property(transaction => transaction.BalanceAfter)
            .HasColumnName("balance_after")
            .IsRequired();
        entity.Property(transaction => transaction.Reason)
            .HasColumnName("reason")
            .HasMaxLength(128)
            .IsRequired();
        entity.Property(transaction => transaction.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        entity.HasIndex(transaction => new
            {
                transaction.PlayerId,
                transaction.RequestId
            })
            .IsUnique();

        entity.HasOne(transaction => transaction.Player)
            .WithMany()
            .HasForeignKey(transaction => transaction.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePlayerInventoryItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PlayerInventoryItem>();

        entity.ToTable(
            "player_inventory_items",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_player_inventory_items_quantity_positive",
                "quantity > 0"));

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.PlayerId).HasColumnName("player_id");
        entity.Property(item => item.ItemId)
            .HasColumnName("item_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .IsRequired();
        entity.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
        entity.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        entity.HasIndex(item => new
            {
                item.PlayerId,
                item.ItemId
            })
            .IsUnique();
        entity.HasOne(item => item.Player)
            .WithMany()
            .HasForeignKey(item => item.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePlayerEquipmentItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PlayerEquipmentItem>();

        entity.ToTable("player_equipment_items");
        entity.HasKey(item => new
            {
                item.PlayerId,
                item.Slot
            });

        entity.Property(item => item.PlayerId).HasColumnName("player_id");
        entity.Property(item => item.Slot)
            .HasColumnName("slot")
            .HasMaxLength(32)
            .IsRequired();
        entity.Property(item => item.ItemId)
            .HasColumnName("item_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(item => item.ItemInstanceId)
            .HasColumnName("item_instance_id");
        entity.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        entity.HasIndex(item => new
            {
                item.PlayerId,
                item.ItemId
            })
            .IsUnique();
        entity.HasIndex(item => item.ItemInstanceId)
            .IsUnique()
            .HasFilter("item_instance_id IS NOT NULL");

        entity.HasOne(item => item.Player)
            .WithMany()
            .HasForeignKey(item => item.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(item => item.ItemInstance)
            .WithMany()
            .HasForeignKey(item => item.ItemInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePlayerItemInstance(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PlayerItemInstance>();

        entity.ToTable(
            "player_item_instances",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_player_item_instances_condition_range",
                "condition_percent BETWEEN 0 AND 100"));

        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.PlayerId).HasColumnName("player_id");
        entity.Property(item => item.ItemId)
            .HasColumnName("item_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(item => item.ConditionPercent)
            .HasColumnName("condition_percent")
            .HasDefaultValue(100)
            .IsRequired();
        entity.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
        entity.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        entity.HasIndex(item => new { item.PlayerId, item.ItemId });
        entity.HasOne(item => item.Player)
            .WithMany()
            .HasForeignKey(item => item.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureInventoryGrant(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InventoryGrant>();

        entity.ToTable(
            "inventory_grants",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_inventory_grants_quantity_positive",
                    "quantity > 0");
                tableBuilder.HasCheckConstraint(
                    "ck_inventory_grants_quantity_after_positive",
                    "quantity_after > 0");
            });

        entity.HasKey(grant => grant.Id);
        entity.Property(grant => grant.Id).HasColumnName("id");
        entity.Property(grant => grant.PlayerId).HasColumnName("player_id");
        entity.Property(grant => grant.RequestId)
            .HasColumnName("request_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(grant => grant.ItemId)
            .HasColumnName("item_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(grant => grant.Quantity)
            .HasColumnName("quantity")
            .IsRequired();
        entity.Property(grant => grant.QuantityAfter)
            .HasColumnName("quantity_after")
            .IsRequired();
        entity.Property(grant => grant.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        entity.HasIndex(grant => new
            {
                grant.PlayerId,
                grant.RequestId
            })
            .IsUnique();

        entity.HasOne(grant => grant.Player)
            .WithMany()
            .HasForeignKey(grant => grant.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureInventoryMutation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InventoryMutation>();

        entity.ToTable(
            "inventory_mutations",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_inventory_mutations_quantity_after_non_negative",
                "quantity_after >= 0"));

        entity.HasKey(mutation => mutation.Id);
        entity.Property(mutation => mutation.Id).HasColumnName("id");
        entity.Property(mutation => mutation.PlayerId).HasColumnName("player_id");
        entity.Property(mutation => mutation.RequestId)
            .HasColumnName("request_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(mutation => mutation.Operation)
            .HasColumnName("operation")
            .HasMaxLength(32)
            .IsRequired();
        entity.Property(mutation => mutation.ItemId)
            .HasColumnName("item_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(mutation => mutation.QuantityDelta)
            .HasColumnName("quantity_delta")
            .IsRequired();
        entity.Property(mutation => mutation.QuantityAfter)
            .HasColumnName("quantity_after")
            .IsRequired();
        entity.Property(mutation => mutation.EquipmentSlot)
            .HasColumnName("equipment_slot")
            .HasMaxLength(32)
            .IsRequired();
        entity.Property(mutation => mutation.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        entity.HasIndex(mutation => new
            {
                mutation.PlayerId,
                mutation.RequestId
            })
            .IsUnique();

        entity.HasOne(mutation => mutation.Player)
            .WithMany()
            .HasForeignKey(mutation => mutation.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureProgressMutation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProgressMutation>();

        entity.ToTable("progress_mutations");
        entity.HasKey(mutation => mutation.Id);
        entity.Property(mutation => mutation.Id).HasColumnName("id");
        entity.Property(mutation => mutation.PlayerId).HasColumnName("player_id");
        entity.Property(mutation => mutation.RequestId)
            .HasColumnName("request_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(mutation => mutation.Operation)
            .HasColumnName("operation")
            .HasMaxLength(32)
            .IsRequired();
        entity.Property(mutation => mutation.Payload)
            .HasColumnName("payload")
            .HasMaxLength(256)
            .IsRequired();
        entity.Property(mutation => mutation.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        entity.HasIndex(mutation => new
            {
                mutation.PlayerId,
                mutation.RequestId
            })
            .IsUnique();

        entity.HasOne(mutation => mutation.Player)
            .WithMany()
            .HasForeignKey(mutation => mutation.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureMarketOffer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MarketOffer>();

        entity.ToTable(
            "market_offers",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_market_offers_price_positive",
                    "price_copper > 0");
                tableBuilder.HasCheckConstraint(
                    "ck_market_offers_status_supported",
                    "status IN ('active', 'sold', 'cancelled')");
                tableBuilder.HasCheckConstraint(
                    "ck_market_offers_condition_range",
                    "condition_percent BETWEEN 0 AND 100");
            });

        entity.HasKey(offer => offer.Id);
        entity.Property(offer => offer.Id).HasColumnName("id");
        entity.Property(offer => offer.SellerPlayerId).HasColumnName("seller_player_id");
        entity.Property(offer => offer.CreateRequestId)
            .HasColumnName("create_request_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(offer => offer.ItemId)
            .HasColumnName("item_id")
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(offer => offer.ItemInstanceId).HasColumnName("item_instance_id");
        entity.Property(offer => offer.ConditionPercent)
            .HasColumnName("condition_percent")
            .HasDefaultValue(100)
            .IsRequired();
        entity.Property(offer => offer.PriceCopper).HasColumnName("price_copper").IsRequired();
        entity.Property(offer => offer.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired();
        entity.Property(offer => offer.BuyerPlayerId).HasColumnName("buyer_player_id");
        entity.Property(offer => offer.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        entity.Property(offer => offer.SoldAtUtc).HasColumnName("sold_at_utc");

        entity.HasIndex(offer => new { offer.SellerPlayerId, offer.CreateRequestId }).IsUnique();
        entity.HasIndex(offer => new { offer.ItemId, offer.Status, offer.PriceCopper, offer.CreatedAtUtc });
        entity.HasIndex(offer => offer.ItemInstanceId)
            .IsUnique()
            .HasFilter("item_instance_id IS NOT NULL AND status = 'active'");

        entity.HasOne(offer => offer.SellerPlayer)
            .WithMany()
            .HasForeignKey(offer => offer.SellerPlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(offer => offer.ItemInstance)
            .WithMany()
            .HasForeignKey(offer => offer.ItemInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }

}
