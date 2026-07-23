namespace ColdVerdge.Domain.Entities;

public sealed class ProgressMutation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlayerId { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Player Player { get; set; } = null!;
}
