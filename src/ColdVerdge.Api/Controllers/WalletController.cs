using ColdVerdge.Domain.Entities;
using ColdVerdge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ColdVerdge.Api.Controllers;

[ApiController]
[Route("api/players/{playerId:guid}/wallet")]
public sealed class WalletController : ControllerBase
{
    private readonly GameDbContext _dbContext;

    public WalletController(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<WalletResponse>> GetWallet(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var wallet = await _dbContext.PlayerWallets
            .AsNoTracking()
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

        return Ok(new WalletResponse
        {
            PlayerId = wallet.PlayerId,
            Gold = wallet.Gold,
            Copper = wallet.Copper
        });
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<WalletTransactionResponse>>>
        GetTransactions(
            Guid playerId,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid limit",
                Detail = "Limit must be between 1 and 100.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var playerExists = await _dbContext.Players
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

        var transactions = await _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(transaction => transaction.PlayerId == playerId)
            .OrderByDescending(transaction => transaction.CreatedAtUtc)
            .Take(limit)
            .Select(transaction => new WalletTransactionResponse
            {
                TransactionId = transaction.Id,
                PlayerId = transaction.PlayerId,
                RequestId = transaction.RequestId,
                Currency = transaction.Currency,
                Amount = transaction.Amount,
                BalanceAfter = transaction.BalanceAfter,
                Reason = transaction.Reason,
                CreatedAtUtc = transaction.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(transactions);
    }

    [HttpPost("add")]
    public async Task<ActionResult<WalletOperationResponse>> Add(
        Guid playerId,
        WalletOperationRequest request,
        CancellationToken cancellationToken)
    {
        return await ChangeBalance(
            playerId,
            request,
            isSpend: false,
            cancellationToken);
    }

    [HttpPost("spend")]
    public async Task<ActionResult<WalletOperationResponse>> Spend(
        Guid playerId,
        WalletOperationRequest request,
        CancellationToken cancellationToken)
    {
        return await ChangeBalance(
            playerId,
            request,
            isSpend: true,
            cancellationToken);
    }

    private async Task<ActionResult<WalletOperationResponse>> ChangeBalance(
        Guid playerId,
        WalletOperationRequest request,
        bool isSpend,
        CancellationToken cancellationToken)
    {
        var requestId = request.RequestId.Trim();
        var currency = request.Currency.Trim().ToLowerInvariant();
        var reason = request.Reason.Trim();

        if (requestId.Length is < 1 or > 64)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid requestId",
                Detail = "requestId must contain between 1 and 64 characters.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (currency is not "gold" and not "copper")
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid currency",
                Detail = "Currency must be either gold or copper.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid amount",
                Detail = "Amount must be greater than zero.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (reason.Length is < 1 or > 128)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid reason",
                Detail = "Reason must contain between 1 and 128 characters.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var existingTransaction = await _dbContext.WalletTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                transaction =>
                    transaction.PlayerId == playerId &&
                    transaction.RequestId == requestId,
                cancellationToken);

        if (existingTransaction is not null)
        {
            return Ok(MapOperation(
                existingTransaction,
                wasAlreadyProcessed: true));
        }

        await using var databaseTransaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var wallet = await _dbContext.PlayerWallets
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

        long signedAmount;
        long balanceAfter;

        try
        {
            signedAmount = isSpend
                ? checked(-request.Amount)
                : request.Amount;

            if (currency == "gold")
            {
                balanceAfter = checked(wallet.Gold + signedAmount);

                if (balanceAfter < 0)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Insufficient funds",
                        Detail = "The player does not have enough gold.",
                        Status = StatusCodes.Status409Conflict
                    });
                }

                wallet.Gold = balanceAfter;
            }
            else
            {
                balanceAfter = checked(wallet.Copper + signedAmount);

                if (balanceAfter < 0)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Insufficient funds",
                        Detail = "The player does not have enough copper.",
                        Status = StatusCodes.Status409Conflict
                    });
                }

                wallet.Copper = balanceAfter;
            }
        }
        catch (OverflowException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Balance overflow",
                Detail = "The wallet balance is outside the supported range.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var walletTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            Currency = currency,
            Amount = signedAmount,
            BalanceAfter = balanceAfter,
            Reason = reason,
            CreatedAtUtc = DateTimeOffset.UtcNow
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

            var duplicateTransaction = await _dbContext.WalletTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    transaction =>
                        transaction.PlayerId == playerId &&
                        transaction.RequestId == requestId,
                    cancellationToken);

            if (duplicateTransaction is not null)
            {
                return Ok(MapOperation(
                    duplicateTransaction,
                    wasAlreadyProcessed: true));
            }

            throw;
        }

        return Ok(MapOperation(
            walletTransaction,
            wasAlreadyProcessed: false));
    }

    private static WalletOperationResponse MapOperation(
        WalletTransaction transaction,
        bool wasAlreadyProcessed)
    {
        return new WalletOperationResponse
        {
            TransactionId = transaction.Id,
            PlayerId = transaction.PlayerId,
            Currency = transaction.Currency,
            Amount = transaction.Amount,
            BalanceAfter = transaction.BalanceAfter,
            RequestId = transaction.RequestId,
            Reason = transaction.Reason,
            CreatedAtUtc = transaction.CreatedAtUtc,
            WasAlreadyProcessed = wasAlreadyProcessed
        };
    }
}

public sealed class WalletOperationRequest
{
    public string RequestId { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    public long Amount { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public sealed class WalletOperationResponse
{
    public Guid TransactionId { get; init; }

    public Guid PlayerId { get; init; }

    public string Currency { get; init; } = string.Empty;

    public long Amount { get; init; }

    public long BalanceAfter { get; init; }

    public string RequestId { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public bool WasAlreadyProcessed { get; init; }
}

public sealed class WalletResponse
{
    public Guid PlayerId { get; init; }

    public long Gold { get; init; }

    public long Copper { get; init; }
}

public sealed class WalletTransactionResponse
{
    public Guid TransactionId { get; init; }

    public Guid PlayerId { get; init; }

    public string RequestId { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    public long Amount { get; init; }

    public long BalanceAfter { get; init; }

    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }
}