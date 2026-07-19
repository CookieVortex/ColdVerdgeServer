namespace ColdVerdge.Api.Contracts.Players;

public sealed class CreatePlayerRequest
{
    public string UserName { get; init; } = string.Empty;
}