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
            },
            Progress = new PlayerProgress
            {
                Level = 1,
                Strength = 10,
                Endurance = 10,
                Agility = 10,
                Perception = 10,
                Intelligence = 10
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
            .Include(item => item.Progress)
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
            Progress = PlayerProgressResponse.FromEntity(player.Progress),
            CreatedAtUtc = player.CreatedAtUtc
        };
    }
}

public sealed class PlayerProgressResponse
{
    public int Level { get; init; }
    public int CurrentExperience { get; init; }
    public int ExperienceToNextLevel { get; init; }
    public int FreeAttributePoints { get; init; }
    public int Strength { get; init; }
    public int Endurance { get; init; }
    public int Agility { get; init; }
    public int Perception { get; init; }
    public int Intelligence { get; init; }
    public string ProfessionId { get; init; } = string.Empty;
    public int ProfessionExperience { get; init; }
    public int PistolsExperience { get; init; }
    public int SubmachineGunsExperience { get; init; }
    public int AssaultRiflesExperience { get; init; }
    public int ShotgunsExperience { get; init; }
    public int SniperRiflesExperience { get; init; }
    public int MachineGunsExperience { get; init; }
    public int ThrowablesExperience { get; init; }
    public int MedicineExperience { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }

    public static PlayerProgressResponse FromEntity(PlayerProgress progress) => new()
    {
        Level = progress.Level,
        CurrentExperience = progress.CurrentExperience,
        ExperienceToNextLevel = Math.Max(100, progress.Level * 100),
        FreeAttributePoints = progress.FreeAttributePoints,
        Strength = progress.Strength,
        Endurance = progress.Endurance,
        Agility = progress.Agility,
        Perception = progress.Perception,
        Intelligence = progress.Intelligence,
        ProfessionId = progress.ProfessionId,
        ProfessionExperience = progress.ProfessionExperience,
        PistolsExperience = progress.Pistols,
        SubmachineGunsExperience = progress.SubmachineGuns,
        AssaultRiflesExperience = progress.AssaultRifles,
        ShotgunsExperience = progress.Shotguns,
        SniperRiflesExperience = progress.SniperRifles,
        MachineGunsExperience = progress.MachineGuns,
        ThrowablesExperience = progress.Throwables,
        MedicineExperience = progress.Medicine,
        UpdatedAtUtc = progress.UpdatedAtUtc
    };
}
