using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;

namespace AdminApi.Tests.Fixtures;

public static class WebhookSubscriptionBuilder
{
    public static WebhookSubscription Build(Action<WebhookSubscription>? configure = null)
    {
        var subscription = new WebhookSubscription
        {
            Id = Guid.NewGuid().ToString(),
            Url = "https://example.test/webhooks",
            Secret = Guid.NewGuid().ToString("N"),
            Events = new List<string> { "user.created" },
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
