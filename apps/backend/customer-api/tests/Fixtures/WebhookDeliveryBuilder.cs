using System.Text.Json;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Types;

namespace CustomerApi.Tests.Fixtures;

public static class WebhookDeliveryBuilder
{
    public static WebhookDelivery Build(Action<WebhookDelivery>? configure = null)
    {
        var delivery = new WebhookDelivery
        {
            Id = Guid.NewGuid().ToString(),
            SubscriptionId = Guid.NewGuid().ToString(),
            EventType = WebhookEventType.UserCreated,
            Payload = JsonDocument.Parse("{\"event\":\"user.created\"}"),
            Status = WebhookDeliveryStatus.Pending,
            AttemptCount = 0,
        };
        configure?.Invoke(delivery);
        return delivery;
    }

    public static async Task<WebhookDelivery> CreateAsync(AppDbContext db, Action<WebhookDelivery>? configure = null)
    {
        var delivery = Build(configure);
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        return delivery;
    }
}
