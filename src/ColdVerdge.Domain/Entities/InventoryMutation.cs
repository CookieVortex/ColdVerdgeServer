namespace ColdVerdge.Domain.Entities;

public sealed class InventoryMutation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlayerId { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    public int QuantityDelta { get; set; }

    public int QuantityAfter { get; set; }

    public string EquipmentSlot { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public Player Player { get; set; } = null!;
}
