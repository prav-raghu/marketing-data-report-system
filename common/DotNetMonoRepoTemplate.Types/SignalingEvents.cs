using System.Text.Json;

namespace DotNetMonoRepoTemplate.Types;

public static class SignalingEventType
{
    public const string Offer = "offer";
    public const string Answer = "answer";
    public const string Candidate = "candidate";
}

public sealed record SignalingEvent
{
    public required string Type { get; init; }
    public required string SenderId { get; init; }
    public required string RecipientId { get; init; }
    public required JsonElement Data { get; init; }
}

public sealed record SignalingResponse
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
}
