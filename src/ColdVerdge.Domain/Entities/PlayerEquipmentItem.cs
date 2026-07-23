namespace ColdVerdge.Domain.Entities;

public sealed class PlayerEquipmentItem
{
    public Guid PlayerId { get; set; }

    public string Slot { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    public Guid? ItemInstanceId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public Player Player { get; set; } = null!;

    public PlayerItemInstance? ItemInstance { get; set; }
}
