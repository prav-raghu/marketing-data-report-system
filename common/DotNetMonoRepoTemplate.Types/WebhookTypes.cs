namespace DotNetMonoRepoTemplate.Types;

public static class WebhookEventType
{
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeleted = "user.deleted";
    public const string OrderCreated = "order.created";
    public const string OrderUpdated = "order.updated";
    public const string OrderCompleted = "order.completed";
    public const string PaymentSuccess = "payment.success";
    public const string PaymentFailed = "payment.failed";
}

public static class WebhookDeliveryStatus
{
    public const string Pending = "pending";
    public const string Delivered = "delivered";
    public const string Failed = "failed";
    public const string Retrying = "retrying";
}

public sealed record WebhookPayload
{
    public required string Event { get; init; }
    public required string Timestamp { get; init; }
    public required IReadOnlyDictionary<string, object?> Data { get; init; }
}

public sealed record WebhookSubscription
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required string Secret { get; init; }
    public required IReadOnlyList<string> Events { get; init; }
    public required bool IsActive { get; init; }
    public required int RetryCount { get; init; }
    public required int TimeoutSeconds { get; init; }
    public string? CreatedBy { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public DateTime? LastTriggeredAt { get; init; }
}

public sealed record WebhookDelivery
{
    public required string Id { get; init; }
    public required string SubscriptionId { get; init; }
    public required string EventType { get; init; }
    public required IReadOnlyDictionary<string, object?> Payload { get; init; }
    public required string Status { get; init; }
    public int? HttpStatus { get; init; }
    public string? ResponseBody { get; init; }
    public string? ErrorMessage { get; init; }
    public required int AttemptCount { get; init; }
    public DateTime? NextRetryAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record CreateWebhookSubscriptionDto
{
    public required string Url { get; init; }
    public required string Secret { get; init; }
    public required IReadOnlyList<string> Events { get; init; }
    public int? RetryCount { get; init; }
    public int? TimeoutSeconds { get; init; }
}

public sealed record UpdateWebhookSubscriptionDto
{
    public string? Url { get; init; }
    public string? Secret { get; init; }
    public IReadOnlyList<string>? Events { get; init; }
    public bool? IsActive { get; init; }
    public int? RetryCount { get; init; }
    public int? TimeoutSeconds { get; init; }
}
