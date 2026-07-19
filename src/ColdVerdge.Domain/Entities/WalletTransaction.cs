namespace ColdVerdge.Domain.Entities;

public sealed class WalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlayerId { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public long Amount { get; set; }

    public long BalanceAfter { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public Player Player { get; set; } = null!;
}