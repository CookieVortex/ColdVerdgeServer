using System.Data;
using ColdVerdge.Domain.Entities;
using ColdVerdge.Api.GameData;
using ColdVerdge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ColdVerdge.Api.Controllers;

[ApiController]
[Route("api/players/{playerId:guid}/shop")]
public sealed class ShopController : ControllerBase
{
    private readonly GameDbContext _dbContext;

    public ShopController(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("buy-weapon")]
    public async Task<ActionResult<BuyWeaponResponse>> BuyWeapon(
        Guid playerId,
        BuyWeaponRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = (request.RequestId ?? string.Empty).Trim();
        string weaponItemId = (request.WeaponId ?? string.Empty).Trim().ToLowerInvariant();

        if (requestId.Length is < 1 or > 64)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid requestId",
                Detail = "requestId must contain between 1 and 64 characters.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!GameItemCatalog.TryGetShopWeapon(
                weaponItemId,
                out int weaponPriceCopper,
                out _))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unknown weapon",
                Detail = "The requested weapon is not available in this shop.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        WalletTransaction? existingTransaction =
            await _dbContext.WalletTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    transaction =>
                        transaction.PlayerId == playerId &&
                        transaction.RequestId == requestId,
                    cancellationToken);

        if (existingTransaction is not null)
        {
            string processedItemId = ResolvePurchasedWeaponId(existingTransaction, weaponItemId);
            GameItemCatalog.TryGetShopWeapon(processedItemId, out int processedPriceCopper, out _);
            PlayerInventoryItem? existingItem =
                await _dbContext.PlayerInventoryItems
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item =>
                            item.PlayerId == playerId &&
                            item.ItemId == processedItemId,
                        cancellationToken);
            PlayerItemInstance? existingInstance =
                await _dbContext.PlayerItemInstances
                    .AsNoTracking()
                    .Where(instance =>
                        instance.PlayerId == playerId &&
                        instance.ItemId == processedItemId)
                    .OrderByDescending(instance => instance.CreatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);

            return Ok(new BuyWeaponResponse
            {
                PlayerId = playerId,
                ItemId = processedItemId,
                Quantity = existingItem?.Quantity ?? 0,
                PriceCopper = processedPriceCopper,
                CopperAfter = existingTransaction.BalanceAfter,
                TransactionId = existingTransaction.Id,
                ItemInstanceId = existingInstance?.Id,
                ConditionPercent = existingInstance?.ConditionPercent ?? 100,
                WasAlreadyProcessed = true
            });
        }

        await using var databaseTransaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        Player? player = await _dbContext.Players
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == playerId,
                cancellationToken);

        if (player is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Player not found",
                Detail = "Player was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        PlayerWallet? wallet = await _dbContext.PlayerWallets
            .SingleOrDefaultAsync(
                item => item.PlayerId == playerId,
                cancellationToken);

        if (wallet is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Wallet not found",
                Detail = "Player or wallet was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        if (wallet.Copper < weaponPriceCopper)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Insufficient funds",
                Detail =
                    $"The weapon costs {weaponPriceCopper} copper. " +
                    $"Current balance is {wallet.Copper}.",
                Status = StatusCodes.Status409Conflict
            });
        }

        PlayerInventoryItem? inventoryItem =
            await _dbContext.PlayerInventoryItems
                .SingleOrDefaultAsync(
                    item =>
                        item.PlayerId == playerId &&
                        item.ItemId == weaponItemId,
                    cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (inventoryItem is null)
        {
            inventoryItem = new PlayerInventoryItem
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                ItemId = weaponItemId,
                Quantity = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _dbContext.PlayerInventoryItems.Add(inventoryItem);
        }
        else
        {
            try
            {
                inventoryItem.Quantity =
                    checked(inventoryItem.Quantity + 1);
            }
            catch (OverflowException)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Inventory quantity overflow",
                    Detail = "The item quantity is too large.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            inventoryItem.UpdatedAtUtc = now;
        }

        var purchasedInstance = new PlayerItemInstance
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            ItemId = weaponItemId,
            ConditionPercent = 100,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _dbContext.PlayerItemInstances.Add(purchasedInstance);

        wallet.Copper -= weaponPriceCopper;

        var walletTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            Currency = "copper",
            Amount = -weaponPriceCopper,
            BalanceAfter = wallet.Copper,
            Reason = $"shop_purchase:{weaponItemId}",
            CreatedAtUtc = now
        };

        _dbContext.WalletTransactions.Add(walletTransaction);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            WalletTransaction? duplicateTransaction =
                await _dbContext.WalletTransactions
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        transaction =>
                            transaction.PlayerId == playerId &&
                            transaction.RequestId == requestId,
                        cancellationToken);

            if (duplicateTransaction is null)
                throw;

            string processedItemId = ResolvePurchasedWeaponId(duplicateTransaction, weaponItemId);
            GameItemCatalog.TryGetShopWeapon(processedItemId, out int processedPriceCopper, out _);

            PlayerInventoryItem? duplicateItem =
                await _dbContext.PlayerInventoryItems
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item =>
                            item.PlayerId == playerId &&
                            item.ItemId == processedItemId,
                        cancellationToken);
            PlayerItemInstance? duplicateInstance =
                await _dbContext.PlayerItemInstances
                    .AsNoTracking()
                    .Where(instance =>
                        instance.PlayerId == playerId &&
                        instance.ItemId == processedItemId)
                    .OrderByDescending(instance => instance.CreatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);

            return Ok(new BuyWeaponResponse
            {
                PlayerId = playerId,
                ItemId = processedItemId,
                Quantity = duplicateItem?.Quantity ?? 0,
                PriceCopper = processedPriceCopper,
                CopperAfter = duplicateTransaction.BalanceAfter,
                TransactionId = duplicateTransaction.Id,
                ItemInstanceId = duplicateInstance?.Id,
                ConditionPercent = duplicateInstance?.ConditionPercent ?? 100,
                WasAlreadyProcessed = true
            });
        }

        return Ok(new BuyWeaponResponse
        {
            PlayerId = playerId,
            ItemId = weaponItemId,
            Quantity = inventoryItem.Quantity,
            PriceCopper = weaponPriceCopper,
            CopperAfter = wallet.Copper,
            TransactionId = walletTransaction.Id,
            ItemInstanceId = purchasedInstance.Id,
            ConditionPercent = purchasedInstance.ConditionPercent,
            WasAlreadyProcessed = false
        });
    }

    [HttpGet("market/weapons")]
    public async Task<ActionResult<MarketWeaponCatalogResponse>> GetMarketWeapons(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Players.AsNoTracking().AnyAsync(player => player.Id == playerId, cancellationToken))
            return NotFound();

        List<MarketWeaponSummaryResponse> summaries = await _dbContext.MarketOffers
            .AsNoTracking()
            .Where(offer => offer.Status == "active")
            .GroupBy(offer => offer.ItemId)
            .Select(group => new MarketWeaponSummaryResponse
            {
                ItemId = group.Key,
                OfferCount = group.Count(),
                LowestPriceCopper = group.Min(offer => offer.PriceCopper),
                AveragePriceCopper = (int)Math.Round(group.Average(offer => offer.PriceCopper))
            })
            .OrderBy(summary => summary.LowestPriceCopper)
            .ThenBy(summary => summary.ItemId)
            .ToListAsync(cancellationToken);

        return Ok(new MarketWeaponCatalogResponse
        {
            Weapons = summaries.Where(summary =>
                GameItemCatalog.TryGetShopWeapon(summary.ItemId, out _, out _)).ToArray()
        });
    }

    [HttpGet("market/weapons/{weaponItemId}/offers")]
    public async Task<ActionResult<MarketOfferListResponse>> GetMarketOffers(
        Guid playerId,
        string weaponItemId,
        CancellationToken cancellationToken)
    {
        string itemId = (weaponItemId ?? string.Empty).Trim().ToLowerInvariant();
        if (!GameItemCatalog.TryGetShopWeapon(itemId, out _, out _))
            return BadRequest(new ProblemDetails { Title = "Unknown weapon", Status = StatusCodes.Status400BadRequest });

        if (!await _dbContext.Players.AsNoTracking().AnyAsync(player => player.Id == playerId, cancellationToken))
            return NotFound();

        MarketOfferResponse[] offers = await _dbContext.MarketOffers
            .AsNoTracking()
            .Where(offer => offer.ItemId == itemId && offer.Status == "active")
            .OrderBy(offer => offer.PriceCopper)
            .ThenBy(offer => offer.CreatedAtUtc)
            .Select(offer => new MarketOfferResponse
            {
                OfferId = offer.Id,
                SellerPlayerId = offer.SellerPlayerId,
                SellerName = offer.SellerPlayer.UserName,
                SellerLevel = offer.SellerPlayer.Level,
                ItemId = offer.ItemId,
                ItemInstanceId = offer.ItemInstanceId,
                ConditionPercent = offer.ConditionPercent,
                PriceCopper = offer.PriceCopper,
                CreatedAtUtc = offer.CreatedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return Ok(new MarketOfferListResponse { ItemId = itemId, Offers = offers });
    }

    [HttpPost("market/create-offer")]
    public async Task<ActionResult<MarketOfferResponse>> CreateMarketOffer(
        Guid playerId,
        CreateMarketOfferRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = (request.RequestId ?? string.Empty).Trim();
        string itemId = (request.ItemId ?? request.WeaponId ?? string.Empty).Trim().ToLowerInvariant();
        if (requestId.Length is < 1 or > 64)
            return BadRequest(new ProblemDetails { Title = "Invalid requestId", Status = StatusCodes.Status400BadRequest });
        if (request.PriceCopper is < 1 or > 10_000_000)
            return BadRequest(new ProblemDetails { Title = "Invalid offer price", Status = StatusCodes.Status400BadRequest });
        if (!GameItemCatalog.TracksCondition(itemId))
            return BadRequest(new ProblemDetails { Title = "Only weapons and armor can be listed here", Status = StatusCodes.Status400BadRequest });

        MarketOffer? existing = await _dbContext.MarketOffers
            .AsNoTracking()
            .Include(offer => offer.SellerPlayer)
            .SingleOrDefaultAsync(
                offer => offer.SellerPlayerId == playerId && offer.CreateRequestId == requestId,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.ItemId != itemId ||
                existing.PriceCopper != request.PriceCopper ||
                (request.ItemInstanceId.HasValue && existing.ItemInstanceId != request.ItemInstanceId))
                return Conflict(new ProblemDetails { Title = "Request identifier was already used", Status = StatusCodes.Status409Conflict });
            return Ok(MapOffer(existing));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        Player? seller = await _dbContext.Players.SingleOrDefaultAsync(player => player.Id == playerId, cancellationToken);
        if (seller is null)
            return NotFound();

        PlayerInventoryItem? inventoryItem = await _dbContext.PlayerInventoryItems.SingleOrDefaultAsync(
            item => item.PlayerId == playerId && item.ItemId == itemId,
            cancellationToken);
        if (inventoryItem is null || inventoryItem.Quantity < 1)
            return Conflict(new ProblemDetails { Title = "Weapon is not owned", Status = StatusCodes.Status409Conflict });

        IQueryable<PlayerItemInstance> availableInstances = _dbContext.PlayerItemInstances
            .Where(instance => instance.PlayerId == playerId && instance.ItemId == itemId)
            .Where(instance => !_dbContext.PlayerEquipmentItems.Any(
                equipment => equipment.ItemInstanceId == instance.Id))
            .Where(instance => !_dbContext.MarketOffers.Any(
                listed => listed.ItemInstanceId == instance.Id && listed.Status == "active"));

        PlayerItemInstance? itemInstance = request.ItemInstanceId.HasValue
            ? await availableInstances.SingleOrDefaultAsync(
                instance => instance.Id == request.ItemInstanceId.Value,
                cancellationToken)
            : await availableInstances
                .OrderBy(instance => instance.ConditionPercent)
                .ThenBy(instance => instance.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

        if (itemInstance is null)
            return Conflict(new ProblemDetails
            {
                Title = "Item instance is unavailable",
                Detail = "The selected item is equipped, already listed, or no longer owned.",
                Status = StatusCodes.Status409Conflict
            });

        inventoryItem.Quantity--;
        if (inventoryItem.Quantity == 0)
            _dbContext.PlayerInventoryItems.Remove(inventoryItem);
        else
            inventoryItem.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var offer = new MarketOffer
        {
            Id = Guid.NewGuid(),
            SellerPlayerId = playerId,
            CreateRequestId = requestId,
            ItemId = itemId,
            ItemInstanceId = itemInstance.Id,
            ConditionPercent = itemInstance.ConditionPercent,
            PriceCopper = request.PriceCopper,
            Status = "active",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SellerPlayer = seller
        };
        _dbContext.MarketOffers.Add(offer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(MapOffer(offer));
    }

    [HttpPost("market/buy-offer")]
    public async Task<ActionResult<BuyMarketOfferResponse>> BuyMarketOffer(
        Guid playerId,
        BuyMarketOfferRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = (request.RequestId ?? string.Empty).Trim();
        if (requestId.Length is < 1 or > 64)
            return BadRequest(new ProblemDetails { Title = "Invalid requestId", Status = StatusCodes.Status400BadRequest });

        WalletTransaction? previousPurchase = await _dbContext.WalletTransactions.AsNoTracking().SingleOrDefaultAsync(
            entry => entry.PlayerId == playerId && entry.RequestId == requestId,
            cancellationToken);
        if (previousPurchase is not null)
            return await ReturnExistingMarketPurchase(playerId, previousPurchase, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        MarketOffer? offer = await _dbContext.MarketOffers
            .Include(item => item.SellerPlayer)
            .Include(item => item.ItemInstance)
            .SingleOrDefaultAsync(item => item.Id == request.OfferId, cancellationToken);
        if (offer is null)
            return NotFound(new ProblemDetails { Title = "Offer not found", Status = StatusCodes.Status404NotFound });
        if (offer.Status != "active")
            return Conflict(new ProblemDetails { Title = "Offer is no longer available", Status = StatusCodes.Status409Conflict });
        if (offer.SellerPlayerId == playerId)
            return Conflict(new ProblemDetails { Title = "You cannot buy your own offer", Status = StatusCodes.Status409Conflict });

        Player? buyer = await _dbContext.Players.AsNoTracking().SingleOrDefaultAsync(item => item.Id == playerId, cancellationToken);
        if (buyer is null)
            return NotFound();
        PlayerWallet? buyerWallet = await _dbContext.PlayerWallets.SingleOrDefaultAsync(item => item.PlayerId == playerId, cancellationToken);
        PlayerWallet? sellerWallet = await _dbContext.PlayerWallets.SingleOrDefaultAsync(item => item.PlayerId == offer.SellerPlayerId, cancellationToken);
        if (buyerWallet is null || sellerWallet is null)
            return NotFound(new ProblemDetails { Title = "Wallet not found", Status = StatusCodes.Status404NotFound });
        if (buyerWallet.Copper < offer.PriceCopper)
            return Conflict(new ProblemDetails { Title = "Insufficient funds", Status = StatusCodes.Status409Conflict });

        PlayerInventoryItem? buyerItem = await _dbContext.PlayerInventoryItems.SingleOrDefaultAsync(
            item => item.PlayerId == playerId && item.ItemId == offer.ItemId,
            cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (buyerItem is null)
        {
            buyerItem = new PlayerInventoryItem
            {
                Id = Guid.NewGuid(), PlayerId = playerId, ItemId = offer.ItemId,
                Quantity = 1, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            _dbContext.PlayerInventoryItems.Add(buyerItem);
        }
        else
        {
            buyerItem.Quantity = checked(buyerItem.Quantity + 1);
            buyerItem.UpdatedAtUtc = now;
        }

        if (offer.ItemInstance is null)
            return Conflict(new ProblemDetails
            {
                Title = "Offer item instance is missing",
                Detail = "The offer was created before item-instance migration and cannot be transferred safely.",
                Status = StatusCodes.Status409Conflict
            });

        offer.ItemInstance.PlayerId = playerId;
        offer.ItemInstance.UpdatedAtUtc = now;

        buyerWallet.Copper -= offer.PriceCopper;
        sellerWallet.Copper += offer.PriceCopper;
        offer.Status = "sold";
        offer.BuyerPlayerId = playerId;
        offer.SoldAtUtc = now;

        var buyerTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(), PlayerId = playerId, RequestId = requestId,
            Currency = "copper", Amount = -offer.PriceCopper, BalanceAfter = buyerWallet.Copper,
            Reason = $"market_purchase:{offer.Id:N}", CreatedAtUtc = now
        };
        _dbContext.WalletTransactions.Add(buyerTransaction);
        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            Id = Guid.NewGuid(), PlayerId = offer.SellerPlayerId, RequestId = $"market-sale-{offer.Id:N}",
            Currency = "copper", Amount = offer.PriceCopper, BalanceAfter = sellerWallet.Copper,
            Reason = $"market_sale:{offer.Id:N}", CreatedAtUtc = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new BuyMarketOfferResponse
        {
            OfferId = offer.Id,
            ItemId = offer.ItemId,
            SellerName = offer.SellerPlayer.UserName,
            PriceCopper = offer.PriceCopper,
            CopperAfter = buyerWallet.Copper,
            QuantityAfter = buyerItem.Quantity,
            TransactionId = buyerTransaction.Id,
            ItemInstanceId = offer.ItemInstanceId,
            ConditionPercent = offer.ConditionPercent,
            WasAlreadyProcessed = false
        });
    }

    private async Task<ActionResult<BuyMarketOfferResponse>> ReturnExistingMarketPurchase(
        Guid playerId,
        WalletTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string prefix = "market_purchase:";
        if (!transaction.Reason.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(transaction.Reason[prefix.Length..], "N", out Guid offerId))
        {
            return Conflict(new ProblemDetails { Title = "Request identifier was already used", Status = StatusCodes.Status409Conflict });
        }

        MarketOffer? offer = await _dbContext.MarketOffers.AsNoTracking()
            .Include(item => item.SellerPlayer)
            .SingleOrDefaultAsync(item => item.Id == offerId && item.BuyerPlayerId == playerId, cancellationToken);
        if (offer is null)
            return NotFound();
        int quantity = await _dbContext.PlayerInventoryItems.AsNoTracking()
            .Where(item => item.PlayerId == playerId && item.ItemId == offer.ItemId)
            .Select(item => item.Quantity)
            .SingleOrDefaultAsync(cancellationToken);

        return Ok(new BuyMarketOfferResponse
        {
            OfferId = offer.Id, ItemId = offer.ItemId, SellerName = offer.SellerPlayer.UserName,
            PriceCopper = offer.PriceCopper, CopperAfter = transaction.BalanceAfter,
            QuantityAfter = quantity, TransactionId = transaction.Id,
            ItemInstanceId = offer.ItemInstanceId, ConditionPercent = offer.ConditionPercent,
            WasAlreadyProcessed = true
        });
    }

    private static MarketOfferResponse MapOffer(MarketOffer offer) => new()
    {
        OfferId = offer.Id,
        SellerPlayerId = offer.SellerPlayerId,
        SellerName = offer.SellerPlayer.UserName,
        SellerLevel = offer.SellerPlayer.Level,
        ItemId = offer.ItemId,
        ItemInstanceId = offer.ItemInstanceId,
        ConditionPercent = offer.ConditionPercent,
        PriceCopper = offer.PriceCopper,
        CreatedAtUtc = offer.CreatedAtUtc
    };

    private static string ResolvePurchasedWeaponId(
        WalletTransaction transaction,
        string fallbackWeaponId)
    {
        const string reasonPrefix = "shop_purchase:";
        if (!string.IsNullOrWhiteSpace(transaction.Reason) &&
            transaction.Reason.StartsWith(reasonPrefix, StringComparison.Ordinal))
        {
            string itemId = transaction.Reason[reasonPrefix.Length..];
            if (GameItemCatalog.TryGetShopWeapon(itemId, out _, out _))
                return itemId;
        }

        return fallbackWeaponId;
    }

    [HttpGet("inventory")]
    public async Task<ActionResult<IReadOnlyList<PlayerInventoryItemResponse>>>
        GetInventory(
            Guid playerId,
            CancellationToken cancellationToken)
    {
        bool playerExists = await _dbContext.Players
            .AsNoTracking()
            .AnyAsync(
                player => player.Id == playerId,
                cancellationToken);

        if (!playerExists)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Player not found",
                Detail = "Player was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        List<PlayerInventoryItemResponse> items =
            await _dbContext.PlayerInventoryItems
                .AsNoTracking()
                .Where(item => item.PlayerId == playerId)
                .OrderBy(item => item.ItemId)
                .Select(item => new PlayerInventoryItemResponse
                {
                    ItemId = item.ItemId,
                    Quantity = item.Quantity,
                    UpdatedAtUtc = item.UpdatedAtUtc
                })
                .ToListAsync(cancellationToken);

        return Ok(items);
    }
}

public sealed class BuyWeaponRequest
{
    public string RequestId { get; init; } = string.Empty;

    public string WeaponId { get; init; } = string.Empty;
}

public sealed class BuyWeaponResponse
{
    public Guid PlayerId { get; init; }

    public string ItemId { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public int PriceCopper { get; init; }

    public long CopperAfter { get; init; }

    public Guid TransactionId { get; init; }

    public Guid? ItemInstanceId { get; init; }

    public int ConditionPercent { get; init; } = 100;

    public bool WasAlreadyProcessed { get; init; }
}

public sealed class PlayerInventoryItemResponse
{
    public string ItemId { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class MarketWeaponSummaryResponse
{
    public string ItemId { get; init; } = string.Empty;
    public int OfferCount { get; init; }
    public int LowestPriceCopper { get; init; }
    public int AveragePriceCopper { get; init; }
}

public sealed class MarketWeaponCatalogResponse
{
    public IReadOnlyList<MarketWeaponSummaryResponse> Weapons { get; init; } = [];
}

public sealed class MarketOfferResponse
{
    public Guid OfferId { get; init; }
    public Guid SellerPlayerId { get; init; }
    public string SellerName { get; init; } = string.Empty;
    public int SellerLevel { get; init; }
    public string ItemId { get; init; } = string.Empty;
    public Guid? ItemInstanceId { get; init; }
    public int ConditionPercent { get; init; } = 100;
    public int PriceCopper { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class MarketOfferListResponse
{
    public string ItemId { get; init; } = string.Empty;
    public IReadOnlyList<MarketOfferResponse> Offers { get; init; } = [];
}

public sealed class CreateMarketOfferRequest
{
    public string RequestId { get; init; } = string.Empty;
    public string WeaponId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public Guid? ItemInstanceId { get; init; }
    public int PriceCopper { get; init; }
}

public sealed class BuyMarketOfferRequest
{
    public string RequestId { get; init; } = string.Empty;
    public Guid OfferId { get; init; }
}

public sealed class BuyMarketOfferResponse
{
    public Guid OfferId { get; init; }
    public string ItemId { get; init; } = string.Empty;
    public string SellerName { get; init; } = string.Empty;
    public int PriceCopper { get; init; }
    public long CopperAfter { get; init; }
    public int QuantityAfter { get; init; }
    public Guid TransactionId { get; init; }
    public Guid? ItemInstanceId { get; init; }
    public int ConditionPercent { get; init; } = 100;
    public bool WasAlreadyProcessed { get; init; }
}
