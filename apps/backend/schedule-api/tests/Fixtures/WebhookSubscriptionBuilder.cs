using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;

namespace ScheduleApi.Tests.Fixtures;

public static class WebhookSubscriptionBuilder
{
    public static WebhookSubscription Build(Action<WebhookSubscription>? configure = null)
    {
        var subscription = new WebhookSubscription
        {
            Url = $"https://example.test/webhooks/{Guid.NewGuid():N}",
            Secret = $"secret-{Guid.NewGuid():N}",
            Events = new List<string> { "order.created" },
            RetryCount = 3,
            TimeoutSeconds = 30,
        };
        configure?.Invoke(subscription);
        return subscription;
    }

    public static async Task<WebhookSubscription> CreateAsync(AppDbContext db, Action<WebhookSubscription>? configure = null)
    {
        var subscription = Build(configure);
        db.WebhookSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription;
    }
}
