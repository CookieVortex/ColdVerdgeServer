using System.Data;
using ColdVerdge.Domain.Entities;
using ColdVerdge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ColdVerdge.Api.Controllers;

[ApiController]
[Route("api/players/{playerId:guid}/inventory")]
public sealed class InventoryController : ControllerBase
{
    private const int MaximumGrantQuantity = 100;

    private static readonly HashSet<string> AllowedResourceIds =
        new(StringComparer.Ordinal)
        {
            "iron_ingot",
            "copper_ingot"
        };

    private readonly GameDbContext _dbContext;

    public InventoryController(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("grant-resource")]
    public async Task<ActionResult<GrantResourceResponse>> GrantResource(
        Guid playerId,
        GrantResourceRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = request.RequestId.Trim();
        string itemId = request.ItemId.Trim().ToLowerInvariant();

        if (requestId.Length is < 1 or > 64)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid requestId",
                Detail = "requestId must contain between 1 and 64 characters.",
                Status = StatusCodes.Status400BadRequest
            });
        }

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
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid quantity",
                Detail = $"Quantity must be between 1 and {MaximumGrantQuantity}.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        InventoryGrant? existingGrant = await _dbContext.InventoryGrants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                grant =>
                    grant.PlayerId == playerId &&
                    grant.RequestId == requestId,
                cancellationToken);

        if (existingGrant is not null)
        {
            if (existingGrant.ItemId != itemId ||
                existingGrant.Quantity != request.Quantity)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "requestId payload mismatch",
                    Detail = "This requestId was already used with different resource data.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            return Ok(MapResponse(existingGrant, wasAlreadyProcessed: true));
        }

        await using var databaseTransaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        bool playerExists = await _dbContext.Players
            .AnyAsync(player => player.Id == playerId, cancellationToken);

        if (!playerExists)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Player not found",
                Detail = "Player was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        PlayerInventoryItem? inventoryItem =
            await _dbContext.PlayerInventoryItems
                .SingleOrDefaultAsync(
                    item =>
                        item.PlayerId == playerId &&
                        item.ItemId == itemId,
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
            return Conflict(new ProblemDetails
            {
                Title = "Inventory quantity overflow",
                Detail = "The resource quantity is too large.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var inventoryGrant = new InventoryGrant
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            ItemId = itemId,
            Quantity = request.Quantity,
            QuantityAfter = quantityAfter,
            CreatedAtUtc = now
        };

        _dbContext.InventoryGrants.Add(inventoryGrant);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            InventoryGrant? duplicateGrant =
                await _dbContext.InventoryGrants
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        grant =>
                            grant.PlayerId == playerId &&
                            grant.RequestId == requestId,
                        cancellationToken);

            if (duplicateGrant is null)
                throw;

            if (duplicateGrant.ItemId != itemId ||
                duplicateGrant.Quantity != request.Quantity)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "requestId payload mismatch",
                    Detail = "This requestId was already used with different resource data.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            return Ok(MapResponse(duplicateGrant, wasAlreadyProcessed: true));
        }

        return Ok(MapResponse(inventoryGrant, wasAlreadyProcessed: false));
    }

    private static GrantResourceResponse MapResponse(
        InventoryGrant grant,
        bool wasAlreadyProcessed)
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
            WasAlreadyProcessed = wasAlreadyProcessed
        };
    }
}

public sealed class GrantResourceRequest
{
    public string RequestId { get; init; } = string.Empty;

    public string ItemId { get; init; } = string.Empty;

    public int Quantity { get; init; }
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
}
