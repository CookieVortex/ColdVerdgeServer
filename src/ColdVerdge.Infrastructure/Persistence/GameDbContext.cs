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

    public DbSet<WalletTransaction> WalletTransactions =>
        Set<WalletTransaction>();

    public DbSet<PlayerInventoryItem> PlayerInventoryItems =>
        Set<PlayerInventoryItem>();

    public DbSet<InventoryGrant> InventoryGrants =>
        Set<InventoryGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurePlayer(modelBuilder);
        ConfigurePlayerWallet(modelBuilder);
        ConfigureWalletTransaction(modelBuilder);
        ConfigurePlayerInventoryItem(modelBuilder);
        ConfigureInventoryGrant(modelBuilder);
    }

    private static void ConfigurePlayer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Player>();

        entity.ToTable("players");

        entity.HasKey(player => player.Id);

        entity.Property(player => player.Id)
            .HasColumnName("id");

        entity.Property(player => player.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(32)
            .IsRequired();

        entity.Property(player => player.NormalizedUserName)
            .HasColumnName("normalized_user_name")
            .HasMaxLength(32)
            .IsRequired();

        entity.HasIndex(player => player.NormalizedUserName)
            .IsUnique();

        entity.Property(player => player.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

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

        entity.Property(wallet => wallet.PlayerId)
            .HasColumnName("player_id");

        entity.Property(wallet => wallet.Gold)
            .HasColumnName("gold")
            .HasDefaultValue(0L)
            .IsRequired();

        entity.Property(wallet => wallet.Copper)
            .HasColumnName("copper")
            .HasDefaultValue(0L)
            .IsRequired();
    }

    private static void ConfigureWalletTransaction(
        ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WalletTransaction>();

        entity.ToTable("wallet_transactions");

        entity.HasKey(transaction => transaction.Id);

        entity.Property(transaction => transaction.Id)
            .HasColumnName("id");

        entity.Property(transaction => transaction.PlayerId)
            .HasColumnName("player_id");

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

    private static void ConfigurePlayerInventoryItem(
        ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PlayerInventoryItem>();

        entity.ToTable(
            "player_inventory_items",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_player_inventory_items_quantity_positive",
                    "quantity > 0");
            });

        entity.HasKey(item => item.Id);

        entity.Property(item => item.Id)
            .HasColumnName("id");

        entity.Property(item => item.PlayerId)
            .HasColumnName("player_id");

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

    private static void ConfigureInventoryGrant(
        ModelBuilder modelBuilder)
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

        entity.Property(grant => grant.Id)
            .HasColumnName("id");

        entity.Property(grant => grant.PlayerId)
            .HasColumnName("player_id");

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
}
