using ColdVerdge.Api.Contracts.Players;
using ColdVerdge.Domain.Entities;
using ColdVerdge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ColdVerdge.Api.Controllers;

[ApiController]
[Route("api/players")]
public sealed class PlayersController : ControllerBase
{
    private readonly GameDbContext _dbContext;

    public PlayersController(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [ProducesResponseType<PlayerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayerResponse>> CreatePlayer(
        CreatePlayerRequest request,
        CancellationToken cancellationToken)
    {
        var userName = request.UserName.Trim();

        if (userName.Length is < 3 or > 32)
        {
            ModelState.AddModelError(
                nameof(request.UserName),
                "UserName must contain between 3 and 32 characters.");

            return ValidationProblem(ModelState);
        }

        var normalizedUserName = userName.ToUpperInvariant();

        var userNameExists = await _dbContext.Players
            .AnyAsync(
                player => player.NormalizedUserName == normalizedUserName,
                cancellationToken);

        if (userNameExists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "User name already exists",
                Detail = "A player with this user name already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var player = new Player
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = normalizedUserName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Wallet = new PlayerWallet
            {
                Gold = 0,
                Copper = 0
            }
        };

        _dbContext.Players.Add(player);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = MapPlayer(player);

        return CreatedAtAction(
            nameof(GetPlayer),
            new { playerId = player.Id },
            response);
    }

    [HttpGet("{playerId:guid}")]
    [ProducesResponseType<PlayerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerResponse>> GetPlayer(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var player = await _dbContext.Players
            .AsNoTracking()
            .Include(item => item.Wallet)
            .SingleOrDefaultAsync(
                item => item.Id == playerId,
                cancellationToken);

        if (player is null)
        {
            return NotFound();
        }

        return Ok(MapPlayer(player));
    }

    private static PlayerResponse MapPlayer(Player player)
    {
        return new PlayerResponse
        {
            Id = player.Id,
            UserName = player.UserName,
            Gold = player.Wallet.Gold,
            Copper = player.Wallet.Copper,
            CreatedAtUtc = player.CreatedAtUtc
        };
    }
}