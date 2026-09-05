namespace IngestionApi.Connectors.TikTok;

public sealed class TikTokApiException : Exception
{
    public TikTokApiException(int code, string? message, string? requestId)
        : base($"TikTok API returned code {code}: {message ?? "no message"} (request {requestId ?? "unknown"})")
    {
        Code = code;
        RequestId = requestId;
    }

    public int Code { get; }

    public string? RequestId { get; }
}
