namespace ColdVerdge.Api.Contracts.Progress;

public sealed class PlayerProgressResponse
{
    public string ProfessionId { get; init; } = string.Empty;

    public long ProfessionPlaySeconds { get; init; }

    public int ProfessionProgressPercent { get; init; }

    public int CombatRating { get; init; }

    public int Level { get; init; }

    public int CurrentExperience { get; init; }

    public int ExperienceToNextLevel { get; init; }

    public int FreeAttributePoints { get; init; }

    public int Strength { get; init; }

    public int Endurance { get; init; }

    public int Agility { get; init; }

    public int Perception { get; init; }

    public int Intelligence { get; init; }

    public int PistolsExperience { get; init; }

    public int SubmachineGunsExperience { get; init; }

    public int AssaultRiflesExperience { get; init; }

    public int ShotgunsExperience { get; init; }

    public int SniperRiflesExperience { get; init; }

    public int MachineGunsExperience { get; init; }

    public int ThrowablesExperience { get; init; }

    public int MedicineExperience { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class AddExperienceRequest
{
    public string RequestId { get; init; } = string.Empty;

    public int Amount { get; init; }
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

public sealed class AddSkillExperienceRequest
{
    public string RequestId { get; init; } = string.Empty;

    public string Skill { get; init; } = string.Empty;

    public int Amount { get; init; }
}

public sealed class SetProfessionRequest
{
    public string RequestId { get; init; } = string.Empty;

    public string ProfessionId { get; init; } = string.Empty;
}

public sealed class ProfessionOperationResponse
{
    public Guid MutationId { get; init; }

    public Guid PlayerId { get; init; }

    public string RequestId { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public bool WasAlreadyProcessed { get; init; }

    public long CopperAfter { get; init; }

    public PlayerProgressResponse Progress { get; init; } = new();
}

public sealed class ProgressOperationResponse
{
    public Guid MutationId { get; init; }

    public Guid PlayerId { get; init; }

    public string RequestId { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public bool WasAlreadyProcessed { get; init; }

    public PlayerProgressResponse Progress { get; init; } = new();
}
