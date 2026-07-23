using ColdVerdge.Api.Contracts.Progress;

namespace ColdVerdge.Api.Contracts.Players;

public sealed class PlayerResponse
{
    public Guid Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public long Gold { get; init; }

    public long Copper { get; init; }

    public PlayerProgressResponse Progress { get; init; } = new();

    public DateTimeOffset CreatedAtUtc { get; init; }
}
