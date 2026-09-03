namespace DotNetMonoRepoTemplate.Cache;

public sealed record UserPresenceCacheEntry(string SocketId, long LastSeen);
