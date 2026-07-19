using ColdVerdge.Domain.Entities;
using ColdVerdge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ColdVerdge.Api.Controllers;

[ApiController]
[Route("api/players/{playerId:guid}/shop")]
public sealed class ShopController : ControllerBase
{
    private const string WeaponItemId = "ak";
    private const int WeaponPriceCopper = 180;

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
        string requestId = request.RequestId.Trim();

        if (requestId.Length is < 1 or > 64)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid requestId",
                Detail = "requestId must contain between 1 and 64 characters.",
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
            PlayerInventoryItem? existingItem =
                await _dbContext.PlayerInventoryItems
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item =>
                            item.PlayerId == playerId &&
                            item.ItemId == WeaponItemId,
                        cancellationToken);

            return Ok(new BuyWeaponResponse
            {
                PlayerId = playerId,
                ItemId = WeaponItemId,
                Quantity = existingItem?.Quantity ?? 0,
                PriceCopper = WeaponPriceCopper,
                CopperAfter = existingTransaction.BalanceAfter,
                TransactionId = existingTransaction.Id,
                WasAlreadyProcessed = true
            });
        }

        await using var databaseTransaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

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

        if (wallet.Copper < WeaponPriceCopper)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Insufficient funds",
                Detail =
                    $"The weapon costs {WeaponPriceCopper} copper. " +
                    $"Current balance is {wallet.Copper}.",
                Status = StatusCodes.Status409Conflict
            });
        }

        PlayerInventoryItem? inventoryItem =
            await _dbContext.PlayerInventoryItems
                .SingleOrDefaultAsync(
                    item =>
                        item.PlayerId == playerId &&
                        item.ItemId == WeaponItemId,
                    cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (inventoryItem is null)
        {
            inventoryItem = new PlayerInventoryItem
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                ItemId = WeaponItemId,
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

        wallet.Copper -= WeaponPriceCopper;

        var walletTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            Currency = "copper",
            Amount = -WeaponPriceCopper,
            BalanceAfter = wallet.Copper,
            Reason = $"shop_purchase:{WeaponItemId}",
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

            PlayerInventoryItem? duplicateItem =
                await _dbContext.PlayerInventoryItems
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item =>
                            item.PlayerId == playerId &&
                            item.ItemId == WeaponItemId,
                        cancellationToken);

            return Ok(new BuyWeaponResponse
            {
                PlayerId = playerId,
                ItemId = WeaponItemId,
                Quantity = duplicateItem?.Quantity ?? 0,
                PriceCopper = WeaponPriceCopper,
                CopperAfter = duplicateTransaction.BalanceAfter,
                TransactionId = duplicateTransaction.Id,
                WasAlreadyProcessed = true
            });
        }

        return Ok(new BuyWeaponResponse
        {
            PlayerId = playerId,
            ItemId = WeaponItemId,
            Quantity = inventoryItem.Quantity,
            PriceCopper = WeaponPriceCopper,
            CopperAfter = wallet.Copper,
            TransactionId = walletTransaction.Id,
            WasAlreadyProcessed = false
        });
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
}

public sealed class BuyWeaponResponse
{
    public Guid PlayerId { get; init; }

    public string ItemId { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public int PriceCopper { get; init; }

    public long CopperAfter { get; init; }

    public Guid TransactionId { get; init; }

    public bool WasAlreadyProcessed { get; init; }
}

public sealed class PlayerInventoryItemResponse
{
    public string ItemId { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}