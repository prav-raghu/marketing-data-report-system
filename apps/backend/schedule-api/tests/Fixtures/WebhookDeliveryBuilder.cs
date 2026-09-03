using System.Text.Json;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Types;

namespace ScheduleApi.Tests.Fixtures;

public static class WebhookDeliveryBuilder
{
    public static WebhookDelivery Build(string subscriptionId, Action<WebhookDelivery>? configure = null)
    {
        var delivery = new WebhookDelivery
        {
            SubscriptionId = subscriptionId,
            EventType = WebhookEventType.OrderCreated,
            Payload = JsonDocument.Parse("{\"orderId\":\"order-1\"}"),
            Status = WebhookDeliveryStatus.Pending,
            AttemptCount = 0,
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1),
        };
        configure?.Invoke(delivery);
        return delivery;
    }

    public static async Task<WebhookDelivery> CreateAsync(AppDbContext db, string subscriptionId, Action<WebhookDelivery>? configure = null)
    {
        var delivery = Build(subscriptionId, configure);
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        return delivery;
    }
}
