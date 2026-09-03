namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class WebhookSubscription : AuditableEntity
{
    public required string Url { get; set; }
    public required string Secret { get; set; }
    public List<string> Events { get; set; } = new();
    public int RetryCount { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public DateTime? LastTriggeredAt { get; set; }
    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}
