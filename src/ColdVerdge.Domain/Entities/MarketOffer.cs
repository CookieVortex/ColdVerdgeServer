namespace ColdVerdge.Domain.Entities;

public sealed class MarketOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SellerPlayerId { get; set; }

    public string CreateRequestId { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    public Guid? ItemInstanceId { get; set; }

    public int ConditionPercent { get; set; } = 100;

    public int PriceCopper { get; set; }

    public string Status { get; set; } = "active";

    public Guid? BuyerPlayerId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SoldAtUtc { get; set; }

    public Player SellerPlayer { get; set; } = null!;

    public PlayerItemInstance? ItemInstance { get; set; }
}
