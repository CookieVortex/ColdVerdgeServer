namespace ColdVerdge.Domain.Entities;

public sealed class PlayerWallet
{
    public Guid PlayerId { get; set; }

    public long Gold { get; set; }

    public long Copper { get; set; }

    public Player Player { get; set; } = null!;
}