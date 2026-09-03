using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Types;

namespace CustomerApi.Tests.Fixtures;

public static class WebhookSubscriptionBuilder
{
    public static WebhookSubscription Build(Action<WebhookSubscription>? configure = null)
    {
        var subscription = new WebhookSubscription
        {
            Id = Guid.NewGuid().ToString(),
            Url = "https://example.com/webhooks",
            Secret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            Events = new List<string> { WebhookEventType.UserCreated },
            RetryCount = 3,
            TimeoutSeconds = 30,
            CreatedBy = "test-user",
            ModifiedBy = "test-user",
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
