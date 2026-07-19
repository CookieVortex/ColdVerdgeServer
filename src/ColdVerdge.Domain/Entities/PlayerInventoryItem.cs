namespace ColdVerdge.Domain.Entities;

public sealed class PlayerInventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlayerId { get; set; }

    public string ItemId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public Player Player { get; set; } = null!;
}