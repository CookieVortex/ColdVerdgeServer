using ColdVerdge.Domain.Entities;
using ColdVerdge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ColdVerdge.Api.Controllers;

[ApiController]
[Route("api/players/{playerId:guid}/progress")]
public sealed class ProgressController : ControllerBase
{
    private static readonly HashSet<string> AllowedProfessions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "",
            "miner",
            "mercenary",
            "stormtrooper",
            "medic",
            "engineer",
            "scout",
            "mayor"
        };

    private readonly GameDbContext _dbContext;

    public ProgressController(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("set-attributes")]
    public async Task<ActionResult<ProgressOperationResponse>> SetAttributes(
        Guid playerId,
        SetAttributesRequest request,
        CancellationToken cancellationToken)
    {
        PlayerProgress? progress = await FindProgress(playerId, cancellationToken);
        if (progress is null)
            return NotFound();

        int[] values =
        [
            request.Strength,
            request.Endurance,
            request.Agility,
            request.Perception,
            request.Intelligence,
            request.FreeAttributePoints
        ];
        if (values.Any(value => value < 0))
            return BadRequest(Problem("Attributes and free points cannot be negative."));

        int currentBudget =
            progress.Strength +
            progress.Endurance +
            progress.Agility +
            progress.Perception +
            progress.Intelligence +
            progress.FreeAttributePoints;
        int requestedBudget = values.Sum();
        if (requestedBudget != currentBudget)
            return UnprocessableEntity(Problem("The total attribute point budget cannot change."));

        progress.Strength = request.Strength;
        progress.Endurance = request.Endurance;
        progress.Agility = request.Agility;
        progress.Perception = request.Perception;
        progress.Intelligence = request.Intelligence;
        progress.FreeAttributePoints = request.FreeAttributePoints;
        progress.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapOperation(playerId, request.RequestId, "set_attributes", progress));
    }

    [HttpPost("set-profession")]
    public async Task<ActionResult<ProgressOperationResponse>> SetProfession(
        Guid playerId,
        SetProfessionRequest request,
        CancellationToken cancellationToken)
    {
        PlayerProgress? progress = await FindProgress(playerId, cancellationToken);
        if (progress is null)
            return NotFound();

        string profession = request.ProfessionId.Trim().ToLowerInvariant();
        if (!AllowedProfessions.Contains(profession))
            return BadRequest(Problem("Unknown profession."));

        if (!string.Equals(progress.ProfessionId, profession, StringComparison.Ordinal))
        {
            progress.ProfessionId = profession;
            progress.ProfessionExperience = 0;
        }

        progress.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapOperation(playerId, request.RequestId, "set_profession", progress));
    }

    [HttpPost("add-profession-experience")]
    public async Task<ActionResult<ProgressOperationResponse>> AddProfessionExperience(
        Guid playerId,
        AddProfessionExperienceRequest request,
        CancellationToken cancellationToken)
    {
        PlayerProgress? progress = await FindProgress(playerId, cancellationToken);
        if (progress is null)
            return NotFound();
        if (string.IsNullOrWhiteSpace(progress.ProfessionId))
            return Conflict(Problem("A profession must be selected first."));
        if (request.Amount is < 1 or > 10_000)
            return BadRequest(Problem("Amount must be between 1 and 10000."));

        progress.ProfessionExperience = checked(progress.ProfessionExperience + request.Amount);
        progress.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapOperation(
            playerId,
            request.RequestId,
            "add_profession_experience",
            progress));
    }

    private Task<PlayerProgress?> FindProgress(
        Guid playerId,
        CancellationToken cancellationToken) =>
        _dbContext.PlayerProgress.SingleOrDefaultAsync(
            progress => progress.PlayerId == playerId,
            cancellationToken);

    private static ProgressOperationResponse MapOperation(
        Guid playerId,
        string requestId,
        string operation,
        PlayerProgress progress) => new()
    {
        PlayerId = playerId,
        RequestId = requestId.Trim(),
        Operation = operation,
        Progress = PlayerProgressResponse.FromEntity(progress)
    };

    private static ProblemDetails Problem(string detail) => new()
    {
        Title = "Invalid progress request",
        Detail = detail,
        Status = StatusCodes.Status400BadRequest
    };
}

public sealed class SetAttributesRequest
{
    public string RequestId { get; init; } = string.Empty;
    public int Strength { get; init; }
    public int Endurance { get; init; }
    public int Agility { get; init; }
    public int Perception { get; init; }
    public int Intelligence { get; init; }
    public int FreeAttributePoints { get; init; }
}

public sealed class SetProfessionRequest
{
    public string RequestId { get; init; } = string.Empty;
    public string ProfessionId { get; init; } = string.Empty;
}

public sealed class AddProfessionExperienceRequest
{
    public string RequestId { get; init; } = string.Empty;
    public int Amount { get; init; }
}

public sealed class ProgressOperationResponse
{
    public Guid PlayerId { get; init; }
    public string RequestId { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public PlayerProgressResponse Progress { get; init; } = new();
}
