namespace ColdVerdge.Domain.Entities;

public sealed class PlayerProgress
{
    public Guid PlayerId { get; set; }

    public int Level { get; set; } = 1;
    public int CurrentExperience { get; set; }
    public int FreeAttributePoints { get; set; }

    public int Strength { get; set; } = 10;
    public int Endurance { get; set; } = 10;
    public int Agility { get; set; } = 10;
    public int Perception { get; set; } = 10;
    public int Intelligence { get; set; } = 10;

    public string ProfessionId { get; set; } = string.Empty;
    public int ProfessionExperience { get; set; }

    public int Pistols { get; set; }
    public int SubmachineGuns { get; set; }
    public int AssaultRifles { get; set; }
    public int Shotguns { get; set; }
    public int SniperRifles { get; set; }
    public int MachineGuns { get; set; }
    public int Throwables { get; set; }
    public int Medicine { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Player Player { get; set; } = null!;
}
