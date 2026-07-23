namespace ColdVerdge.Domain.Entities;

public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public int Level { get; set; } = 1;

    public int CurrentExperience { get; set; }

    public int ExperienceToNextLevel { get; set; } = 100;

    public int FreeAttributePoints { get; set; } = 5;

    public int Strength { get; set; } = 10;

    public int Endurance { get; set; } = 10;

    public int Agility { get; set; } = 10;

    public int Perception { get; set; } = 10;

    public int Intelligence { get; set; } = 10;

    public int Survival { get; set; } = 10;

    public int PistolsExperience { get; set; }

    public int SubmachineGunsExperience { get; set; }

    public int AssaultRiflesExperience { get; set; }

    public int ShotgunsExperience { get; set; }

    public int SniperRiflesExperience { get; set; }

    public int MachineGunsExperience { get; set; }

    public int ThrowablesExperience { get; set; }

    public int MedicineExperience { get; set; }

    public string ProfessionId { get; set; } = string.Empty;

    public DateTimeOffset ProgressUpdatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public PlayerWallet Wallet { get; set; } = null!;
}
