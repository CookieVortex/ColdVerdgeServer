namespace ColdVerdge.Domain.Entities;

/// <summary>
/// Server-authoritative state for one non-stackable weapon or armor item.
/// Aggregate inventory rows remain the fast quantity source for all item types.
/// </summary>
public sealed class PlayerItemInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlayerId { get; set; }

    public string ItemId { get; set; } = string.Empty;

    public int ConditionPercent { get; set; } = 100;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Player Player { get; set; } = null!;
}
