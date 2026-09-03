using System.Text.Json;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Types;
using Entities = DotNetMonoRepoTemplate.Database.Entities;

namespace CustomerApi.Tests.Fixtures;

public static class WebhookDeliveryBuilder
{
    public static Entities.WebhookDelivery Build(Action<Entities.WebhookDelivery>? configure = null)
    {
        var delivery = new Entities.WebhookDelivery
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

    public static async Task<Entities.WebhookDelivery> CreateAsync(AppDbContext db, Action<Entities.WebhookDelivery>? configure = null)
    {
        var delivery = Build(configure);
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        return delivery;
    }
}
