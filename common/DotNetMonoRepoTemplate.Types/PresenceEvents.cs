namespace DotNetMonoRepoTemplate.Types;

public sealed record PresenceUpdateEvent
{
    public required string UserId { get; init; }
    public required bool Online { get; init; }
    public DateTime? LastSeenAt { get; init; }
}

public sealed record PresenceHeartbeatEvent(string UserId, DateTime Timestamp);

public sealed record UserPresence(string UserId, bool Online, DateTime LastSeenAt);
