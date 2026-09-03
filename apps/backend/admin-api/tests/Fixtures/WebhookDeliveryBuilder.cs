using System.Text.Json;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;

namespace AdminApi.Tests.Fixtures;

public static class WebhookDeliveryBuilder
{
    public static WebhookDelivery Build(string subscriptionId, Action<WebhookDelivery>? configure = null)
    {
        var delivery = new WebhookDelivery
        {
            Id = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            EventType = "user.created",
            Payload = JsonDocument.Parse("{}"),
            Status = "delivered",
            AttemptCount = 1,
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
