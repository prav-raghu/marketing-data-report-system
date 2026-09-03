using System.Text.Json.Serialization;

namespace DotNetMonoRepoTemplate.Sms;

public sealed record SmsPortalFault
{
    [JsonPropertyName("faultId")]
    public string? FaultId { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed record SmsPortalErrorReport
{
    [JsonPropertyName("noNetwork")]
    public int? NoNetwork { get; init; }

    [JsonPropertyName("duplicates")]
    public int? Duplicates { get; init; }

    [JsonPropertyName("optedOuts")]
    public int? OptedOuts { get; init; }

    [JsonPropertyName("faults")]
    public IReadOnlyList<SmsPortalFault>? Faults { get; init; }
}

public sealed record SmsPortalSendResponse
{
    [JsonPropertyName("httpStatusCode")]
    public int? HttpStatusCode { get; init; }

    [JsonPropertyName("messages")]
    public int? Messages { get; init; }

    [JsonPropertyName("cost")]
    public decimal? Cost { get; init; }

    [JsonPropertyName("remainingBalance")]
    public decimal? RemainingBalance { get; init; }

    [JsonPropertyName("errorReport")]
    public SmsPortalErrorReport? ErrorReport { get; init; }
}

public sealed record SmsPortalResponse
{
    [JsonPropertyName("errors")]
    public IReadOnlyList<string>? Errors { get; init; }

    [JsonPropertyName("sendResponse")]
    public SmsPortalSendResponse? SendResponse { get; init; }
}
