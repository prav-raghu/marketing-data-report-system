using System.Text.Json;

namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class WebhookDelivery : TimestampedEntity
{
    public required string SubscriptionId { get; set; }
    public WebhookSubscription? Subscription { get; set; }
    public required string EventType { get; set; }
    public required JsonDocument Payload { get; set; }
    public required string Status { get; set; }
    public int? HttpStatus { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
