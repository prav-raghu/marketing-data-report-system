using System.Text.Json;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Types;
using Entities = DotNetMonoRepoTemplate.Database.Entities;

namespace ScheduleApi.Tests.Fixtures;

public static class WebhookDeliveryBuilder
{
    public static Entities.WebhookDelivery Build(string subscriptionId, Action<Entities.WebhookDelivery>? configure = null)
    {
        var delivery = new Entities.WebhookDelivery
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

    public static async Task<Entities.WebhookDelivery> CreateAsync(AppDbContext db, string subscriptionId, Action<Entities.WebhookDelivery>? configure = null)
    {
        var delivery = Build(subscriptionId, configure);
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        return delivery;
    }
}
