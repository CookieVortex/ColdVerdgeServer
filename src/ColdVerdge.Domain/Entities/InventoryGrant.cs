namespace ColdVerdge.Domain.Entities;

public sealed class InventoryGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlayerId { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int QuantityAfter { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Player Player { get; set; } = null!;
}
