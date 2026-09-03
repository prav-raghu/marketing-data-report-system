using CustomerApi.Services;
using CustomerApi.Tests.Fixtures;
using DotNetMonoRepoTemplate.Types;
using FluentAssertions;
using Xunit;

namespace CustomerApi.Tests.Services;

public sealed class WebhookSubscriptionServiceTests
{
    [Fact]
    public async Task CreateSubscriptionAsync_PersistsSubscription_WithProvidedSecret()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new WebhookSubscriptionService(db);
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/hook",
            Secret = "explicit-secret",
            Events = new List<string> { WebhookEventType.UserCreated },
        };

        var result = await service.CreateSubscriptionAsync(dto, createdBy: "user-1");

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Secret.Should().Be("explicit-secret");
        result.Data.CreatedBy.Should().Be("user-1");
        result.Data.ModifiedBy.Should().Be("user-1");
        (await db.WebhookSubscriptions.FindAsync(result.Data.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSubscriptionAsync_GeneratesSecret_WhenSecretIsEmpty()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new WebhookSubscriptionService(db);
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/hook",
            Secret = string.Empty,
            Events = new List<string> { WebhookEventType.UserCreated },
        };

        var result = await service.CreateSubscriptionAsync(dto, createdBy: "user-1");

        result.Data!.Secret.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateSubscriptionAsync_DefaultsRetryAndTimeout_WhenNotProvided()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new WebhookSubscriptionService(db);
        var dto = new CreateWebhookSubscriptionDto
        {
            Url = "https://example.com/hook",
            Secret = "secret",
            Events = new List<string> { WebhookEventType.UserCreated },
        };

        var result = await service.CreateSubscriptionAsync(dto, createdBy: "user-1");

        result.Data!.RetryCount.Should().Be(3);
        result.Data.TimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public async Task GetSubscriptionAsync_ReturnsSubscription_WhenOwnedByUser()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.CreatedBy = "user-1");
        var service = new WebhookSubscriptionService(db);

        var result = await service.GetSubscriptionAsync(subscription.Id, "user-1");

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Id.Should().Be(subscription.Id);
    }

    [Fact]
    public async Task GetSubscriptionAsync_ReturnsNotFound_WhenOwnedByDifferentUser()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.CreatedBy = "user-1");
        var service = new WebhookSubscriptionService(db);

        var result = await service.GetSubscriptionAsync(subscription.Id, "user-2");

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Subscription not found");
    }

    [Fact]
    public async Task GetSubscriptionAsync_ReturnsNotFound_WhenIdDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new WebhookSubscriptionService(db);

        var result = await service.GetSubscriptionAsync(Guid.NewGuid().ToString(), "user-1");

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task ListSubscriptionsAsync_ReturnsOnlyOwnedSubscriptions()
    {
        await using var db = TestDbContextFactory.Create();
        await WebhookSubscriptionBuilder.CreateAsync(db, s => s.CreatedBy = "user-1");
        await WebhookSubscriptionBuilder.CreateAsync(db, s => s.CreatedBy = "user-2");
        var service = new WebhookSubscriptionService(db);

        var result = await service.ListSubscriptionsAsync("user-1", isActive: null);

        result.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task ListSubscriptionsAsync_FiltersByIsActive()
    {
        await using var db = TestDbContextFactory.Create();
        await WebhookSubscriptionBuilder.CreateAsync(db, s => { s.CreatedBy = "user-1"; s.IsActive = true; });
        await WebhookSubscriptionBuilder.CreateAsync(db, s => { s.CreatedBy = "user-1"; s.IsActive = false; });
        var service = new WebhookSubscriptionService(db);

        var result = await service.ListSubscriptionsAsync("user-1", isActive: true);

        result.Data.Should().ContainSingle(s => s.IsActive);
    }

    [Fact]
    public async Task ListSubscriptionsAsync_ReturnsEmpty_WhenUserHasNoSubscriptions()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new WebhookSubscriptionService(db);

        var result = await service.ListSubscriptionsAsync("user-1", isActive: null);

        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_UpdatesProvidedFields_AndLeavesOthersUnchanged()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s =>
        {
            s.CreatedBy = "user-1";
            s.Url = "https://old.example.com";
            s.RetryCount = 3;
        });
        var service = new WebhookSubscriptionService(db);
        var dto = new UpdateWebhookSubscriptionDto { Url = "https://new.example.com" };

        var result = await service.UpdateSubscriptionAsync(subscription.Id, "user-1", dto);

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Url.Should().Be("https://new.example.com");
        result.Data.RetryCount.Should().Be(3);
        result.Data.ModifiedBy.Should().Be("user-1");
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_ReturnsNotFound_WhenSubscriptionDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new WebhookSubscriptionService(db);

        var result = await service.UpdateSubscriptionAsync(Guid.NewGuid().ToString(), "user-1", new UpdateWebhookSubscriptionDto());

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_RemovesSubscription_WhenOwnedByUser()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.CreatedBy = "user-1");
        var service = new WebhookSubscriptionService(db);

        var result = await service.DeleteSubscriptionAsync(subscription.Id, "user-1");

        result.IsSuccessful.Should().BeTrue();
        (await db.WebhookSubscriptions.FindAsync(subscription.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_ReturnsNotFound_WhenOwnedByDifferentUser()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.CreatedBy = "user-1");
        var service = new WebhookSubscriptionService(db);

        var result = await service.DeleteSubscriptionAsync(subscription.Id, "user-2");

        result.IsSuccessful.Should().BeFalse();
        (await db.WebhookSubscriptions.FindAsync(subscription.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetDeliveriesAsync_ReturnsDeliveries_ForOwnedSubscription()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.CreatedBy = "user-1");
        await WebhookDeliveryBuilder.CreateAsync(db, d => d.SubscriptionId = subscription.Id);
        await WebhookDeliveryBuilder.CreateAsync(db, d => d.SubscriptionId = subscription.Id);
        var service = new WebhookSubscriptionService(db);

        var result = await service.GetDeliveriesAsync(subscription.Id, "user-1", limit: 10);

        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDeliveriesAsync_ReturnsNotFound_WhenSubscriptionNotOwnedByUser()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.CreatedBy = "user-1");
        var service = new WebhookSubscriptionService(db);

        var result = await service.GetDeliveriesAsync(subscription.Id, "user-2", limit: 10);

        result.IsSuccessful.Should().BeFalse();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDeliveriesAsync_RespectsLimit()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.CreatedBy = "user-1");
        for (var i = 0; i < 5; i++)
        {
            await WebhookDeliveryBuilder.CreateAsync(db, d => d.SubscriptionId = subscription.Id);
        }
        var service = new WebhookSubscriptionService(db);

        var result = await service.GetDeliveriesAsync(subscription.Id, "user-1", limit: 2);

        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task RegenerateSecretAsync_ChangesSecret_WhenOwnedByUser()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s =>
        {
            s.CreatedBy = "user-1";
            s.Secret = "original-secret";
        });
        var service = new WebhookSubscriptionService(db);

        var result = await service.RegenerateSecretAsync(subscription.Id, "user-1");

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Secret.Should().NotBe("original-secret");
        result.Data.ModifiedBy.Should().Be("user-1");
    }

    [Fact]
    public async Task RegenerateSecretAsync_ReturnsNotFound_WhenSubscriptionDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new WebhookSubscriptionService(db);

        var result = await service.RegenerateSecretAsync(Guid.NewGuid().ToString(), "user-1");

        result.IsSuccessful.Should().BeFalse();
    }
}
