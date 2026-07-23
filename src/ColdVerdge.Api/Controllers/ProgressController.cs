using System.Data;
using ColdVerdge.Api.Contracts.Progress;
using ColdVerdge.Domain.Characters;
using ColdVerdge.Domain.Entities;
using ColdVerdge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ColdVerdge.Api.Controllers;

[ApiController]
[Route("api/players/{playerId:guid}/progress")]
public sealed class ProgressController : ControllerBase
{
    private const int MaximumExperienceGrant = 1_000_000;
    private const int MaximumAttributeValue = 1_000_000;
    private const int FullMasteryExperience = 15_000;
    private const int AttributePointsPerLevel = 3;

    private static readonly HashSet<string> SupportedSkills =
        new(StringComparer.Ordinal)
        {
            "pistols",
            "submachine_guns",
            "assault_rifles",
            "shotguns",
            "sniper_rifles",
            "machine_guns",
            "throwables",
            "medicine"
        };

    private static readonly IReadOnlyDictionary<string, (int RequiredLevel, int TrainingCost)> Professions =
        new Dictionary<string, (int RequiredLevel, int TrainingCost)>(StringComparer.Ordinal)
        {
            ["miner"] = (1, 0),
            ["mercenary"] = (5, 125),
            ["engineer"] = (5, 150),
            ["scout"] = (10, 275),
            ["mayor"] = (10, 350)
        };

    private readonly GameDbContext _dbContext;

    public ProgressController(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PlayerProgressResponse>> GetProgress(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        Player? player = await _dbContext.Players
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == playerId, cancellationToken);

        return player is null
            ? NotFound()
            : Ok(MapProgress(player));
    }

    [HttpPost("add-experience")]
    public async Task<ActionResult<ProgressOperationResponse>> AddExperience(
        Guid playerId,
        AddExperienceRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = NormalizeRequestId(request.RequestId);
        if (ValidateRequestId(requestId) is ActionResult requestError)
            return requestError;

        if (request.Amount is < 1 or > MaximumExperienceGrant)
            return InvalidAmount(MaximumExperienceGrant);

        string payload = $"amount={request.Amount}";
        ProgressMutation? existing = await FindMutation(
            playerId,
            requestId,
            cancellationToken);

        if (existing is not null)
            return await ReturnExisting(existing, "add_experience", payload, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        Player? player = await _dbContext.Players.SingleOrDefaultAsync(
            item => item.Id == playerId,
            cancellationToken);

        if (player is null)
            return NotFound();

        int remaining = request.Amount;
        player.Level = Math.Max(1, player.Level);
        player.CurrentExperience = Math.Max(0, player.CurrentExperience);
        player.ExperienceToNextLevel = Math.Max(
            1,
            player.ExperienceToNextLevel);
        player.FreeAttributePoints = Math.Max(0, player.FreeAttributePoints);

        checked
        {
            player.CurrentExperience += remaining;
        }

        while (player.CurrentExperience >= player.ExperienceToNextLevel)
        {
            player.CurrentExperience -= player.ExperienceToNextLevel;
            player.Level++;
            player.ExperienceToNextLevel += 50;
            player.FreeAttributePoints += AttributePointsPerLevel;
        }

        player.ProgressUpdatedAtUtc = DateTimeOffset.UtcNow;

        ProgressMutation mutation = CreateMutation(
            playerId,
            requestId,
            "add_experience",
            payload);
        _dbContext.ProgressMutations.Add(mutation);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(MapOperation(mutation, player, false));
    }

    [HttpPost("set-attributes")]
    public async Task<ActionResult<ProgressOperationResponse>> SetAttributes(
        Guid playerId,
        SetAttributesRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = NormalizeRequestId(request.RequestId);
        if (ValidateRequestId(requestId) is ActionResult requestError)
            return requestError;

        int[] values =
        {
            request.Strength,
            request.Endurance,
            request.Agility,
            request.Perception,
            request.Intelligence,
            request.FreeAttributePoints
        };

        if (values.Any(value => value < 0 || value > MaximumAttributeValue))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid character attributes",
                Detail = $"Every attribute and free point value must be between 0 and {MaximumAttributeValue}.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        string payload = string.Join(
            ";",
            new[]
            {
                request.Strength,
                request.Endurance,
                request.Agility,
                request.Perception,
                request.Intelligence,
                request.FreeAttributePoints
            });

        ProgressMutation? existing = await FindMutation(
            playerId,
            requestId,
            cancellationToken);

        if (existing is not null)
            return await ReturnExisting(existing, "set_attributes", payload, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        Player? player = await _dbContext.Players.SingleOrDefaultAsync(
            item => item.Id == playerId,
            cancellationToken);

        if (player is null)
            return NotFound();

        long currentPointTotal =
            (long)Math.Max(0, player.Strength) +
            Math.Max(0, player.Endurance) +
            Math.Max(0, player.Agility) +
            Math.Max(0, player.Perception) +
            Math.Max(0, player.Intelligence) +
            Math.Max(0, player.FreeAttributePoints);
        long requestedPointTotal = values.Sum(value => (long)value);

        if (requestedPointTotal != currentPointTotal)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Invalid attribute point total",
                Detail = "Character attributes may be redistributed, but the client cannot create or remove attribute points.",
                Status = StatusCodes.Status409Conflict
            });
        }

        player.Strength = request.Strength;
        player.Endurance = request.Endurance;
        player.Agility = request.Agility;
        player.Perception = request.Perception;
        player.Intelligence = request.Intelligence;
        player.FreeAttributePoints = request.FreeAttributePoints;
        player.ProgressUpdatedAtUtc = DateTimeOffset.UtcNow;

        ProgressMutation mutation = CreateMutation(
            playerId,
            requestId,
            "set_attributes",
            payload);
        _dbContext.ProgressMutations.Add(mutation);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(MapOperation(mutation, player, false));
    }

    [HttpPost("add-skill-experience")]
    public async Task<ActionResult<ProgressOperationResponse>> AddSkillExperience(
        Guid playerId,
        AddSkillExperienceRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = NormalizeRequestId(request.RequestId);
        if (ValidateRequestId(requestId) is ActionResult requestError)
            return requestError;

        if (request.Amount is < 1 or > MaximumExperienceGrant)
            return InvalidAmount(MaximumExperienceGrant);

        string skill = NormalizeToken(request.Skill);
        if (!SupportedSkills.Contains(skill))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported skill",
                Detail = "The requested skill is not supported.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        string payload = $"skill={skill};amount={request.Amount}";
        ProgressMutation? existing = await FindMutation(
            playerId,
            requestId,
            cancellationToken);

        if (existing is not null)
            return await ReturnExisting(existing, "add_skill_experience", payload, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        Player? player = await _dbContext.Players.SingleOrDefaultAsync(
            item => item.Id == playerId,
            cancellationToken);

        if (player is null)
            return NotFound();

        SetSkillExperience(
            player,
            skill,
            Math.Min(
                FullMasteryExperience,
                GetSkillExperience(player, skill) + request.Amount));
        player.ProgressUpdatedAtUtc = DateTimeOffset.UtcNow;

        ProgressMutation mutation = CreateMutation(
            playerId,
            requestId,
            "add_skill_experience",
            payload);
        _dbContext.ProgressMutations.Add(mutation);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(MapOperation(mutation, player, false));
    }

    [HttpPost("set-profession")]
    public async Task<ActionResult<ProfessionOperationResponse>> SetProfession(
        Guid playerId,
        SetProfessionRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = NormalizeRequestId(request.RequestId);
        if (ValidateRequestId(requestId) is ActionResult requestError)
            return requestError;

        string professionId = NormalizeToken(request.ProfessionId);
        bool isAbandon = string.IsNullOrEmpty(professionId);
        if (!isAbandon && !Professions.ContainsKey(professionId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported profession",
                Detail = "The requested profession is not supported.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        string payload = $"profession={professionId}";
        ProgressMutation? existing = await FindMutation(
            playerId,
            requestId,
            cancellationToken);

        if (existing is not null)
            return await ReturnExistingProfession(existing, payload, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        Player? player = await _dbContext.Players
            .Include(item => item.Wallet)
            .SingleOrDefaultAsync(item => item.Id == playerId, cancellationToken);

        if (player is null)
            return NotFound();

        if (isAbandon)
        {
            if (string.IsNullOrEmpty(player.ProfessionId))
            {
                return Conflict(new ProblemDetails
                {
                    Title = "No active profession",
                    Detail = "The player has no profession to leave.",
                    Status = StatusCodes.Status409Conflict
                });
            }
        }
        else
        {
            if (string.Equals(player.ProfessionId, professionId, StringComparison.Ordinal))
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Profession is already active",
                    Detail = "The selected profession is already active for this player.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            (int requiredLevel, int trainingCost) = Professions[professionId];
            if (player.Level < requiredLevel)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Player level is too low",
                    Detail = $"Profession '{professionId}' requires player level {requiredLevel}.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            if (player.Wallet.Copper < trainingCost)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Insufficient copper",
                    Detail = $"Profession '{professionId}' training costs {trainingCost} copper.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            if (trainingCost > 0)
            {
                player.Wallet.Copper -= trainingCost;
                _dbContext.WalletTransactions.Add(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    RequestId = requestId,
                    Currency = "copper",
                    Amount = -trainingCost,
                    BalanceAfter = player.Wallet.Copper,
                    Reason = $"profession_training:{professionId}",
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        player.ProfessionId = professionId;
        player.ProfessionPlaySeconds = 0;
        player.ProfessionLastHeartbeatAtUtc = isAbandon ? null : now;
        player.ProgressUpdatedAtUtc = now;

        ProgressMutation mutation = CreateMutation(
            playerId,
            requestId,
            "set_profession",
            payload);
        _dbContext.ProgressMutations.Add(mutation);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(MapProfessionOperation(mutation, player, false));
    }

    [HttpPost("profession-heartbeat")]
    public async Task<ActionResult<PlayerProgressResponse>> ProfessionHeartbeat(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        Player? player = await _dbContext.Players.SingleOrDefaultAsync(
            item => item.Id == playerId,
            cancellationToken);
        if (player is null)
            return NotFound();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(player.ProfessionId))
        {
            if (player.ProfessionLastHeartbeatAtUtc is DateTimeOffset previous)
            {
                double elapsed = (now - previous).TotalSeconds;
                if (elapsed is >= 1 and <= 120)
                {
                    player.ProfessionPlaySeconds = Math.Min(
                        CharacterRules.ProfessionMasterySeconds,
                        player.ProfessionPlaySeconds + (long)Math.Floor(elapsed));
                }
            }
            player.ProfessionLastHeartbeatAtUtc = now;
            player.ProgressUpdatedAtUtc = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(MapProgress(player));
    }

    private async Task<ActionResult<ProfessionOperationResponse>> ReturnExistingProfession(
        ProgressMutation mutation,
        string payload,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(mutation.Operation, "set_profession", StringComparison.Ordinal) ||
            !string.Equals(mutation.Payload, payload, StringComparison.Ordinal))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Request identifier was already used",
                Detail = "The same requestId cannot be reused with different profession data.",
                Status = StatusCodes.Status409Conflict
            });
        }

        Player? player = await _dbContext.Players
            .AsNoTracking()
            .Include(item => item.Wallet)
            .SingleOrDefaultAsync(item => item.Id == mutation.PlayerId, cancellationToken);

        return player is null
            ? NotFound()
            : Ok(MapProfessionOperation(mutation, player, true));
    }

    private async Task<ActionResult<ProgressOperationResponse>> ReturnExisting(
        ProgressMutation mutation,
        string operation,
        string payload,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(mutation.Operation, operation, StringComparison.Ordinal) ||
            !string.Equals(mutation.Payload, payload, StringComparison.Ordinal))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Request identifier was already used",
                Detail = "The same requestId cannot be reused with different progress data.",
                Status = StatusCodes.Status409Conflict
            });
        }

        Player? player = await _dbContext.Players
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == mutation.PlayerId, cancellationToken);

        return player is null
            ? NotFound()
            : Ok(MapOperation(mutation, player, true));
    }

    private async Task<ProgressMutation?> FindMutation(
        Guid playerId,
        string requestId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ProgressMutations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.PlayerId == playerId && item.RequestId == requestId,
                cancellationToken);
    }

    private static ProgressMutation CreateMutation(
        Guid playerId,
        string requestId,
        string operation,
        string payload)
    {
        return new ProgressMutation
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            RequestId = requestId,
            Operation = operation,
            Payload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static ProgressOperationResponse MapOperation(
        ProgressMutation mutation,
        Player player,
        bool wasAlreadyProcessed)
    {
        return new ProgressOperationResponse
        {
            MutationId = mutation.Id,
            PlayerId = player.Id,
            RequestId = mutation.RequestId,
            Operation = mutation.Operation,
            WasAlreadyProcessed = wasAlreadyProcessed,
            Progress = MapProgress(player)
        };
    }

    private static ProfessionOperationResponse MapProfessionOperation(
        ProgressMutation mutation,
        Player player,
        bool wasAlreadyProcessed)
    {
        return new ProfessionOperationResponse
        {
            MutationId = mutation.Id,
            PlayerId = player.Id,
            RequestId = mutation.RequestId,
            Operation = mutation.Operation,
            WasAlreadyProcessed = wasAlreadyProcessed,
            CopperAfter = player.Wallet.Copper,
            Progress = MapProgress(player)
        };
    }

    public static PlayerProgressResponse MapProgress(Player player)
    {
        return new PlayerProgressResponse
        {
            ProfessionId = player.ProfessionId ?? string.Empty,
            ProfessionPlaySeconds = Math.Clamp(player.ProfessionPlaySeconds, 0, CharacterRules.ProfessionMasterySeconds),
            ProfessionProgressPercent = (int)Math.Floor(
                Math.Clamp(player.ProfessionPlaySeconds, 0, CharacterRules.ProfessionMasterySeconds) * 100d /
                CharacterRules.ProfessionMasterySeconds),
            CombatRating = CharacterRules.CombatRating(
                player.Level,
                player.Strength,
                player.Endurance,
                player.Agility,
                player.Perception,
                player.Intelligence,
                100 + Math.Max(0, player.Endurance) * 5,
                Math.Max(0, player.Endurance / 10),
                1 + (int)Math.Round(Math.Max(0, player.Strength) * .45d),
                Math.Max(0, player.Perception) * .25d,
                30d + Math.Max(0, player.Strength) * 1.5d),
            Level = Math.Max(1, player.Level),
            CurrentExperience = Math.Max(0, player.CurrentExperience),
            ExperienceToNextLevel = Math.Max(1, player.ExperienceToNextLevel),
            FreeAttributePoints = Math.Max(0, player.FreeAttributePoints),
            Strength = Math.Max(0, player.Strength),
            Endurance = Math.Max(0, player.Endurance),
            Agility = Math.Max(0, player.Agility),
            Perception = Math.Max(0, player.Perception),
            Intelligence = Math.Max(0, player.Intelligence),
            PistolsExperience = ClampSkill(player.PistolsExperience),
            SubmachineGunsExperience = ClampSkill(player.SubmachineGunsExperience),
            AssaultRiflesExperience = ClampSkill(player.AssaultRiflesExperience),
            ShotgunsExperience = ClampSkill(player.ShotgunsExperience),
            SniperRiflesExperience = ClampSkill(player.SniperRiflesExperience),
            MachineGunsExperience = ClampSkill(player.MachineGunsExperience),
            ThrowablesExperience = ClampSkill(player.ThrowablesExperience),
            MedicineExperience = ClampSkill(player.MedicineExperience),
            UpdatedAtUtc = player.ProgressUpdatedAtUtc
        };
    }

    private static int GetSkillExperience(Player player, string skill)
    {
        return skill switch
        {
            "pistols" => player.PistolsExperience,
            "submachine_guns" => player.SubmachineGunsExperience,
            "assault_rifles" => player.AssaultRiflesExperience,
            "shotguns" => player.ShotgunsExperience,
            "sniper_rifles" => player.SniperRiflesExperience,
            "machine_guns" => player.MachineGunsExperience,
            "throwables" => player.ThrowablesExperience,
            "medicine" => player.MedicineExperience,
            _ => 0
        };
    }

    private static void SetSkillExperience(Player player, string skill, int value)
    {
        value = ClampSkill(value);
        switch (skill)
        {
            case "pistols": player.PistolsExperience = value; break;
            case "submachine_guns": player.SubmachineGunsExperience = value; break;
            case "assault_rifles": player.AssaultRiflesExperience = value; break;
            case "shotguns": player.ShotgunsExperience = value; break;
            case "sniper_rifles": player.SniperRiflesExperience = value; break;
            case "machine_guns": player.MachineGunsExperience = value; break;
            case "throwables": player.ThrowablesExperience = value; break;
            case "medicine": player.MedicineExperience = value; break;
        }
    }

    private static int ClampSkill(int value) =>
        Math.Clamp(value, 0, FullMasteryExperience);

    private static string NormalizeRequestId(string value) =>
        value?.Trim() ?? string.Empty;

    private static string NormalizeToken(string value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    private ActionResult? ValidateRequestId(string requestId)
    {
        if (requestId.Length is >= 1 and <= 64)
            return null;

        return BadRequest(new ProblemDetails
        {
            Title = "Invalid request identifier",
            Detail = "requestId must contain between 1 and 64 characters.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private ActionResult InvalidAmount(int maximum)
    {
        return BadRequest(new ProblemDetails
        {
            Title = "Invalid experience amount",
            Detail = $"Amount must contain a value between 1 and {maximum}.",
            Status = StatusCodes.Status400BadRequest
        });
    }
}
