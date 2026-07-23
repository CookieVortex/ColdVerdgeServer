using System.Data;
using ColdVerdge.Api.GameData;
using ColdVerdge.Domain.Entities;
using ColdVerdge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ColdVerdge.Api.Controllers;

[ApiController]
[Route("api/players/{playerId:guid}/inventory")]
public sealed class InventoryController : ControllerBase
{
    private const int MaximumGrantQuantity = 100;
    private const int MaximumConsumeQuantity = 100_000;
    private const int MaximumSaleQuantity = 100_000;

    private static readonly HashSet<string> AllowedResourceIds =
        new(StringComparer.Ordinal)
        {
            "iron_ingot",
            "copper_ingot"
        };

    private static readonly HashSet<string> AllowedConsumeReasons =
        new(StringComparer.Ordinal)
        {
            "consume",
            "drop",
            "reload",
            "use"
        };

    private readonly GameDbContext _dbContext;

    public InventoryController(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<InventoryStateResponse>> GetState(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        if (!await PlayerExists(playerId, cancellationToken))
            return PlayerNotFound();

        return Ok(await BuildState(playerId, cancellationToken));
    }

    [HttpPost("grant-resource")]
    public async Task<ActionResult<GrantResourceResponse>> GrantResource(
        Guid playerId,
        GrantResourceRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = NormalizeRequestId(request.RequestId);
        string itemId = NormalizeItemId(request.ItemId);

        ActionResult? validationError = ValidateRequestId(requestId);
        if (validationError is not null)
            return validationError;

        if (!AllowedResourceIds.Contains(itemId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid resource",
                Detail = "Only iron_ingot and copper_ingot can be granted.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.Quantity is < 1 or > MaximumGrantQuantity)
            return InvalidPositiveQuantity(MaximumGrantQuantity);

        InventoryGrant? existingGrant = await FindGrant(
            playerId,
            requestId,
            cancellationToken);

        if (existingGrant is not null)
        {
            if (existingGrant.ItemId != itemId ||
                existingGrant.Quantity != request.Quantity)
            {
                return RequestPayloadMismatch();
            }

            return Ok(await MapGrantResponse(
                existingGrant,
                wasAlreadyProcessed: true,
                cancellationToken));
        }

        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (!await PlayerExists(playerId, cancellationToken))
            return PlayerNotFound();

        PlayerInventoryItem? inventoryItem =
            await _dbContext.PlayerInventoryItems.SingleOrDefaultAsync(
                item => item.PlayerId == playerId && item.ItemId == itemId,
                cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        int quantityAfter;

        try
        {
            if (inventoryItem is null)
            {
                quantityAfter = request.Quantity;
                inventoryItem = new PlayerInventoryItem
                {
                    Id = Guid.NewGuid(),
                    PlayerId = playerId,
                    ItemId = itemId,
                    Quantity = quantityAfter,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                _dbContext.PlayerInventoryItems.Add(inventoryItem);
            }
            else
            {
                quantityAfter = checked(inventoryItem.Quantity + request.Quantity);
                inventoryItem.Quantity = quantityAfter;
                inventoryItem.UpdatedAtUtc = now;
            }
        }
        catch (OverflowException)
        {
            return QuantityOverflow();
        }

        var grant = new InventoryGrant
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            ItemId = itemId,
            Quantity = request.Quantity,
            QuantityAfter = quantityAfter,
            CreatedAtUtc = now
        };
        _dbContext.InventoryGrants.Add(grant);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            InventoryGrant? duplicate = await FindGrant(
                playerId,
                requestId,
                cancellationToken);

            if (duplicate is null)
                throw;

            if (duplicate.ItemId != itemId || duplicate.Quantity != request.Quantity)
                return RequestPayloadMismatch();

            return Ok(await MapGrantResponse(
                duplicate,
                wasAlreadyProcessed: true,
                cancellationToken));
        }

        return Ok(await MapGrantResponse(
            grant,
            wasAlreadyProcessed: false,
            cancellationToken));
    }

    [HttpPost("consume")]
    public async Task<ActionResult<InventoryOperationResponse>> Consume(
        Guid playerId,
        ConsumeInventoryRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = NormalizeRequestId(request.RequestId);
        string itemId = NormalizeItemId(request.ItemId);
        string reason = NormalizeToken(request.Reason);

        ActionResult? validationError = ValidateRequestId(requestId);
        if (validationError is not null)
            return validationError;

        if (!GameItemCatalog.Contains(itemId))
            return InvalidItem();

        if (!AllowedConsumeReasons.Contains(reason))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid consume reason",
                Detail = "Reason must be consume, drop, reload or use.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.Quantity is < 1 or > MaximumConsumeQuantity)
            return InvalidPositiveQuantity(MaximumConsumeQuantity);

        InventoryMutation? existingMutation = await FindMutation(
            playerId,
            requestId,
            cancellationToken);

        if (existingMutation is not null)
        {
            if (!MutationMatches(
                    existingMutation,
                    reason,
                    itemId,
                    -request.Quantity,
                    string.Empty))
            {
                return RequestPayloadMismatch();
            }

            return Ok(await MapOperationResponse(
                existingMutation,
                wasAlreadyProcessed: true,
                cancellationToken));
        }

        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        PlayerInventoryItem? inventoryItem =
            await _dbContext.PlayerInventoryItems.SingleOrDefaultAsync(
                item => item.PlayerId == playerId && item.ItemId == itemId,
                cancellationToken);

        if (inventoryItem is null)
        {
            if (!await PlayerExists(playerId, cancellationToken))
                return PlayerNotFound();

            return NotEnoughItems(itemId, request.Quantity, 0);
        }

        if (inventoryItem.Quantity < request.Quantity)
        {
            return NotEnoughItems(
                itemId,
                request.Quantity,
                inventoryItem.Quantity);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        int quantityAfter = inventoryItem.Quantity - request.Quantity;

        if (quantityAfter == 0)
        {
            _dbContext.PlayerInventoryItems.Remove(inventoryItem);

            List<PlayerEquipmentItem> equipmentRows =
                await _dbContext.PlayerEquipmentItems
                    .Where(item =>
                        item.PlayerId == playerId &&
                        item.ItemId == itemId)
                    .ToListAsync(cancellationToken);

            _dbContext.PlayerEquipmentItems.RemoveRange(equipmentRows);
        }
        else
        {
            inventoryItem.Quantity = quantityAfter;
            inventoryItem.UpdatedAtUtc = now;
        }

        var mutation = new InventoryMutation
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            Operation = reason,
            ItemId = itemId,
            QuantityDelta = -request.Quantity,
            QuantityAfter = quantityAfter,
            EquipmentSlot = string.Empty,
            CreatedAtUtc = now
        };
        _dbContext.InventoryMutations.Add(mutation);

        ActionResult<InventoryOperationResponse>? duplicateResult =
            await SaveInventoryMutation(
                transaction,
                mutation,
                cancellationToken);

        if (duplicateResult is not null)
            return duplicateResult;

        return Ok(await MapOperationResponse(
            mutation,
            wasAlreadyProcessed: false,
            cancellationToken));
    }

    [HttpPost("set-equipment")]
    public async Task<ActionResult<InventoryOperationResponse>> SetEquipment(
        Guid playerId,
        SetEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = NormalizeRequestId(request.RequestId);
        string slot = NormalizeToken(request.Slot);
        string itemId = NormalizeItemId(request.ItemId);
        bool unequip = string.IsNullOrEmpty(itemId);
        string operation = unequip ? "unequip" : "equip";

        ActionResult? validationError = ValidateRequestId(requestId);
        if (validationError is not null)
            return validationError;

        if (!GameItemCatalog.IsValidEquipmentSlot(slot))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid equipment slot",
                Detail = "The requested equipment slot is not supported.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!unequip &&
            (!GameItemCatalog.TryGetEquipmentSlot(itemId, out string requiredSlot) ||
             requiredSlot != slot))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Item cannot be equipped in this slot",
                Detail = $"Item '{itemId}' cannot be equipped in slot '{slot}'.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        InventoryMutation? existingMutation = await FindMutation(
            playerId,
            requestId,
            cancellationToken);

        if (existingMutation is not null)
        {
            if (!MutationMatches(
                    existingMutation,
                    operation,
                    itemId,
                    0,
                    slot))
            {
                return RequestPayloadMismatch();
            }

            return Ok(await MapOperationResponse(
                existingMutation,
                wasAlreadyProcessed: true,
                cancellationToken));
        }

        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (!await PlayerExists(playerId, cancellationToken))
            return PlayerNotFound();

        int quantityAfter = 0;
        if (!unequip)
        {
            PlayerInventoryItem? ownedItem =
                await _dbContext.PlayerInventoryItems.SingleOrDefaultAsync(
                    item => item.PlayerId == playerId && item.ItemId == itemId,
                    cancellationToken);

            if (ownedItem is null || ownedItem.Quantity <= 0)
                return NotEnoughItems(itemId, 1, 0);

            quantityAfter = ownedItem.Quantity;
        }

        List<PlayerEquipmentItem> slotRows =
            await _dbContext.PlayerEquipmentItems
                .Where(item =>
                    item.PlayerId == playerId &&
                    (item.Slot == slot || (!unequip && item.ItemId == itemId)))
                .ToListAsync(cancellationToken);

        PlayerEquipmentItem? slotRow =
            slotRows.FirstOrDefault(item => item.Slot == slot);

        foreach (PlayerEquipmentItem duplicateItemRow in slotRows)
        {
            if (duplicateItemRow != slotRow && duplicateItemRow.ItemId == itemId)
                _dbContext.PlayerEquipmentItems.Remove(duplicateItemRow);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (unequip)
        {
            if (slotRow is not null)
                _dbContext.PlayerEquipmentItems.Remove(slotRow);
        }
        else if (slotRow is null)
        {
            _dbContext.PlayerEquipmentItems.Add(new PlayerEquipmentItem
            {
                PlayerId = playerId,
                Slot = slot,
                ItemId = itemId,
                UpdatedAtUtc = now
            });
        }
        else
        {
            slotRow.ItemId = itemId;
            slotRow.UpdatedAtUtc = now;
        }

        var mutation = new InventoryMutation
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            Operation = operation,
            ItemId = itemId,
            QuantityDelta = 0,
            QuantityAfter = quantityAfter,
            EquipmentSlot = slot,
            CreatedAtUtc = now
        };
        _dbContext.InventoryMutations.Add(mutation);

        ActionResult<InventoryOperationResponse>? duplicateResult =
            await SaveInventoryMutation(
                transaction,
                mutation,
                cancellationToken);

        if (duplicateResult is not null)
            return duplicateResult;

        return Ok(await MapOperationResponse(
            mutation,
            wasAlreadyProcessed: false,
            cancellationToken));
    }

    [HttpPost("sell-resource")]
    public async Task<ActionResult<SellResourceResponse>> SellResource(
        Guid playerId,
        SellResourceRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = NormalizeRequestId(request.RequestId);
        string itemId = NormalizeItemId(request.ItemId);

        ActionResult? validationError = ValidateRequestId(requestId);
        if (validationError is not null)
            return validationError;

        if (!GameItemCatalog.TryGetFactoryPrice(itemId, out int unitPriceCopper))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported resource",
                Detail = "The requested item cannot be sold at the factory.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.Quantity is < 1 or > MaximumSaleQuantity)
            return InvalidPositiveQuantity(MaximumSaleQuantity);

        InventoryMutation? existingMutation = await FindMutation(
            playerId,
            requestId,
            cancellationToken);

        if (existingMutation is not null)
        {
            if (!MutationMatches(
                    existingMutation,
                    "sell",
                    itemId,
                    -request.Quantity,
                    string.Empty))
            {
                return RequestPayloadMismatch();
            }

            return Ok(await MapSaleResponse(
                existingMutation,
                unitPriceCopper,
                wasAlreadyProcessed: true,
                cancellationToken));
        }

        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        PlayerWallet? wallet = await _dbContext.PlayerWallets.SingleOrDefaultAsync(
            item => item.PlayerId == playerId,
            cancellationToken);

        if (wallet is null)
            return PlayerNotFound();

        PlayerInventoryItem? inventoryItem =
            await _dbContext.PlayerInventoryItems.SingleOrDefaultAsync(
                item => item.PlayerId == playerId && item.ItemId == itemId,
                cancellationToken);

        if (inventoryItem is null || inventoryItem.Quantity < request.Quantity)
        {
            return NotEnoughItems(
                itemId,
                request.Quantity,
                inventoryItem?.Quantity ?? 0);
        }

        long grossCopper = checked((long)unitPriceCopper * request.Quantity);
        long taxCopper = RoundDivide(
            grossCopper * GameItemCatalog.FactoryTaxBasisPoints,
            10_000L);
        long netCopper = Math.Max(0L, grossCopper - taxCopper);

        long copperAfter;
        try
        {
            copperAfter = checked(wallet.Copper + netCopper);
        }
        catch (OverflowException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Wallet balance overflow",
                Detail = "The resulting copper balance is too large.",
                Status = StatusCodes.Status409Conflict
            });
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        int quantityAfter = inventoryItem.Quantity - request.Quantity;

        if (quantityAfter == 0)
            _dbContext.PlayerInventoryItems.Remove(inventoryItem);
        else
        {
            inventoryItem.Quantity = quantityAfter;
            inventoryItem.UpdatedAtUtc = now;
        }

        wallet.Copper = copperAfter;

        var mutation = new InventoryMutation
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            Operation = "sell",
            ItemId = itemId,
            QuantityDelta = -request.Quantity,
            QuantityAfter = quantityAfter,
            EquipmentSlot = string.Empty,
            CreatedAtUtc = now
        };

        var walletTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            Currency = "copper",
            Amount = netCopper,
            BalanceAfter = copperAfter,
            Reason = $"factory_sale:{itemId}",
            CreatedAtUtc = now
        };

        _dbContext.InventoryMutations.Add(mutation);
        _dbContext.WalletTransactions.Add(walletTransaction);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            InventoryMutation? duplicate = await FindMutation(
                playerId,
                requestId,
                cancellationToken);

            if (duplicate is null)
                throw;

            if (!MutationMatches(
                    duplicate,
                    "sell",
                    itemId,
                    -request.Quantity,
                    string.Empty))
            {
                return RequestPayloadMismatch();
            }

            return Ok(await MapSaleResponse(
                duplicate,
                unitPriceCopper,
                wasAlreadyProcessed: true,
                cancellationToken));
        }

        return Ok(await MapSaleResponse(
            mutation,
            unitPriceCopper,
            wasAlreadyProcessed: false,
            cancellationToken));
    }

    private async Task<ActionResult<InventoryOperationResponse>?> SaveInventoryMutation(
        IDbContextTransaction transaction,
        InventoryMutation mutation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            InventoryMutation? duplicate = await FindMutation(
                mutation.PlayerId,
                mutation.RequestId,
                cancellationToken);

            if (duplicate is null)
                throw;

            if (!MutationMatches(
                    duplicate,
                    mutation.Operation,
                    mutation.ItemId,
                    mutation.QuantityDelta,
                    mutation.EquipmentSlot))
            {
                return RequestPayloadMismatch();
            }

            return Ok(await MapOperationResponse(
                duplicate,
                wasAlreadyProcessed: true,
                cancellationToken));
        }
    }

    private async Task<GrantResourceResponse> MapGrantResponse(
        InventoryGrant grant,
        bool wasAlreadyProcessed,
        CancellationToken cancellationToken)
    {
        return new GrantResourceResponse
        {
            GrantId = grant.Id,
            PlayerId = grant.PlayerId,
            RequestId = grant.RequestId,
            ItemId = grant.ItemId,
            Quantity = grant.Quantity,
            QuantityAfter = grant.QuantityAfter,
            CreatedAtUtc = grant.CreatedAtUtc,
            WasAlreadyProcessed = wasAlreadyProcessed,
            State = await BuildState(grant.PlayerId, cancellationToken)
        };
    }

    private async Task<InventoryOperationResponse> MapOperationResponse(
        InventoryMutation mutation,
        bool wasAlreadyProcessed,
        CancellationToken cancellationToken)
    {
        return new InventoryOperationResponse
        {
            MutationId = mutation.Id,
            PlayerId = mutation.PlayerId,
            RequestId = mutation.RequestId,
            Operation = mutation.Operation,
            ItemId = mutation.ItemId,
            QuantityDelta = mutation.QuantityDelta,
            QuantityAfter = mutation.QuantityAfter,
            EquipmentSlot = mutation.EquipmentSlot,
            CreatedAtUtc = mutation.CreatedAtUtc,
            WasAlreadyProcessed = wasAlreadyProcessed,
            State = await BuildState(mutation.PlayerId, cancellationToken)
        };
    }

    private async Task<SellResourceResponse> MapSaleResponse(
        InventoryMutation mutation,
        int unitPriceCopper,
        bool wasAlreadyProcessed,
        CancellationToken cancellationToken)
    {
        int quantity = Math.Max(0, -mutation.QuantityDelta);
        long grossCopper = checked((long)unitPriceCopper * quantity);
        long taxCopper = RoundDivide(
            grossCopper * GameItemCatalog.FactoryTaxBasisPoints,
            10_000L);
        long netCopper = Math.Max(0L, grossCopper - taxCopper);

        WalletTransaction? walletTransaction =
            await _dbContext.WalletTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.PlayerId == mutation.PlayerId &&
                        item.RequestId == mutation.RequestId,
                    cancellationToken);

        return new SellResourceResponse
        {
            MutationId = mutation.Id,
            PlayerId = mutation.PlayerId,
            RequestId = mutation.RequestId,
            ItemId = mutation.ItemId,
            Quantity = quantity,
            QuantityAfter = mutation.QuantityAfter,
            UnitPriceCopper = unitPriceCopper,
            GrossCopper = grossCopper,
            TaxCopper = taxCopper,
            NetCopper = netCopper,
            CopperAfter = walletTransaction?.BalanceAfter ?? 0L,
            CreatedAtUtc = mutation.CreatedAtUtc,
            WasAlreadyProcessed = wasAlreadyProcessed,
            State = await BuildState(mutation.PlayerId, cancellationToken)
        };
    }

    private async Task<InventoryStateResponse> BuildState(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        List<InventoryItemResponse> items =
            await _dbContext.PlayerInventoryItems
                .AsNoTracking()
                .Where(item => item.PlayerId == playerId && item.Quantity > 0)
                .OrderBy(item => item.ItemId)
                .Select(item => new InventoryItemResponse
                {
                    ItemId = item.ItemId,
                    Quantity = item.Quantity,
                    UpdatedAtUtc = item.UpdatedAtUtc
                })
                .ToListAsync(cancellationToken);

        HashSet<string> ownedItemIds = items
            .Select(item => item.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        List<PlayerEquipmentItem> equipmentRows =
            await _dbContext.PlayerEquipmentItems
                .AsNoTracking()
                .Where(item => item.PlayerId == playerId)
                .OrderBy(item => item.Slot)
                .ToListAsync(cancellationToken);

        List<EquipmentItemResponse> equipment = equipmentRows
            .Where(item =>
                ownedItemIds.Contains(item.ItemId) &&
                GameItemCatalog.TryGetEquipmentSlot(item.ItemId, out string requiredSlot) &&
                requiredSlot == item.Slot)
            .Select(item => new EquipmentItemResponse
            {
                Slot = item.Slot,
                ItemId = item.ItemId,
                UpdatedAtUtc = item.UpdatedAtUtc
            })
            .ToList();

        return new InventoryStateResponse
        {
            PlayerId = playerId,
            Items = items,
            Equipment = equipment
        };
    }

    private Task<bool> PlayerExists(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Players
            .AsNoTracking()
            .AnyAsync(player => player.Id == playerId, cancellationToken);
    }

    private Task<InventoryGrant?> FindGrant(
        Guid playerId,
        string requestId,
        CancellationToken cancellationToken)
    {
        return _dbContext.InventoryGrants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                grant =>
                    grant.PlayerId == playerId &&
                    grant.RequestId == requestId,
                cancellationToken);
    }

    private Task<InventoryMutation?> FindMutation(
        Guid playerId,
        string requestId,
        CancellationToken cancellationToken)
    {
        return _dbContext.InventoryMutations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                mutation =>
                    mutation.PlayerId == playerId &&
                    mutation.RequestId == requestId,
                cancellationToken);
    }

    private static bool MutationMatches(
        InventoryMutation mutation,
        string operation,
        string itemId,
        int quantityDelta,
        string equipmentSlot)
    {
        return mutation.Operation == operation &&
               mutation.ItemId == itemId &&
               mutation.QuantityDelta == quantityDelta &&
               mutation.EquipmentSlot == equipmentSlot;
    }

    private static string NormalizeRequestId(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string NormalizeItemId(string? value)
    {
        return NormalizeToken(value);
    }

    private static string NormalizeToken(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private ActionResult? ValidateRequestId(string requestId)
    {
        if (requestId.Length is >= 1 and <= 64)
            return null;

        return BadRequest(new ProblemDetails
        {
            Title = "Invalid requestId",
            Detail = "requestId must contain between 1 and 64 characters.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private ActionResult InvalidItem()
    {
        return BadRequest(new ProblemDetails
        {
            Title = "Invalid item",
            Detail = "The requested item is not registered by the server.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private ActionResult InvalidPositiveQuantity(int maximum)
    {
        return BadRequest(new ProblemDetails
        {
            Title = "Invalid quantity",
            Detail = $"Quantity must be between 1 and {maximum}.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private ActionResult QuantityOverflow()
    {
        return Conflict(new ProblemDetails
        {
            Title = "Inventory quantity overflow",
            Detail = "The item quantity is too large.",
            Status = StatusCodes.Status409Conflict
        });
    }

    private ActionResult PlayerNotFound()
    {
        return NotFound(new ProblemDetails
        {
            Title = "Player not found",
            Detail = "Player or wallet was not found.",
            Status = StatusCodes.Status404NotFound
        });
    }

    private ActionResult NotEnoughItems(
        string itemId,
        int requested,
        int available)
    {
        return Conflict(new ProblemDetails
        {
            Title = "Not enough items",
            Detail =
                $"Requested {requested} of '{itemId}', but only " +
                $"{available} are available.",
            Status = StatusCodes.Status409Conflict
        });
    }

    private ActionResult RequestPayloadMismatch()
    {
        return Conflict(new ProblemDetails
        {
            Title = "requestId payload mismatch",
            Detail =
                "This requestId was already used with different operation data.",
            Status = StatusCodes.Status409Conflict
        });
    }

    private static long RoundDivide(long numerator, long denominator)
    {
        if (denominator <= 0)
            return 0L;

        return (numerator + denominator / 2L) / denominator;
    }
}

public sealed class GrantResourceRequest
{
    public string RequestId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

public sealed class ConsumeInventoryRequest
{
    public string RequestId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Reason { get; init; } = "consume";
}

public sealed class SetEquipmentRequest
{
    public string RequestId { get; init; } = string.Empty;
    public string Slot { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
}

public sealed class SellResourceRequest
{
    public string RequestId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

public sealed class InventoryStateResponse
{
    public Guid PlayerId { get; init; }
    public IReadOnlyList<InventoryItemResponse> Items { get; init; } =
        Array.Empty<InventoryItemResponse>();
    public IReadOnlyList<EquipmentItemResponse> Equipment { get; init; } =
        Array.Empty<EquipmentItemResponse>();
}

public sealed class InventoryItemResponse
{
    public string ItemId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class EquipmentItemResponse
{
    public string Slot { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class GrantResourceResponse
{
    public Guid GrantId { get; init; }
    public Guid PlayerId { get; init; }
    public string RequestId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public int QuantityAfter { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool WasAlreadyProcessed { get; init; }
    public InventoryStateResponse State { get; init; } = new();
}

public sealed class InventoryOperationResponse
{
    public Guid MutationId { get; init; }
    public Guid PlayerId { get; init; }
    public string RequestId { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public int QuantityDelta { get; init; }
    public int QuantityAfter { get; init; }
    public string EquipmentSlot { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool WasAlreadyProcessed { get; init; }
    public InventoryStateResponse State { get; init; } = new();
}

public sealed class SellResourceResponse
{
    public Guid MutationId { get; init; }
    public Guid PlayerId { get; init; }
    public string RequestId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public int QuantityAfter { get; init; }
    public int UnitPriceCopper { get; init; }
    public long GrossCopper { get; init; }
    public long TaxCopper { get; init; }
    public long NetCopper { get; init; }
    public long CopperAfter { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool WasAlreadyProcessed { get; init; }
    public InventoryStateResponse State { get; init; } = new();
}
