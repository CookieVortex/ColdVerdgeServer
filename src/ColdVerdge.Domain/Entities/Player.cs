namespace ColdVerdge.Domain.Entities;

public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public PlayerWallet Wallet { get; set; } = null!;
}