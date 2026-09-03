using System.Net;
using CustomerApi.Services;
using CustomerApi.Tests.Fixtures;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Types;
using DotNetMonoRepoTemplate.Utilities;
using FluentAssertions;
using Xunit;

namespace CustomerApi.Tests.Services;

public sealed class WebhookDeliveryServiceTests
{
    private static readonly WebhookSignatureService SignatureService = new();

    private static WebhookDeliveryService CreateService(
        AppDbContext db,
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        out FakeHttpMessageHandler handler)
    {
        handler = new FakeHttpMessageHandler(responder);
        return new WebhookDeliveryService(db, new FakeHttpClientFactory(handler));
    }

    private static HttpResponseMessage Ok(HttpRequestMessage _) => new(HttpStatusCode.OK) { Content = new StringContent("{}") };

    private static HttpResponseMessage ServerError(HttpRequestMessage _) => new(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") };

    [Fact]
    public async Task PublishEventAsync_CreatesDelivery_ForActiveMatchingSubscription()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s =>
        {
            s.IsActive = true;
            s.Events = new List<string> { WebhookEventType.UserCreated };
        });
        var service = CreateService(db, Ok, out _);

        await service.PublishEventAsync(WebhookEventType.UserCreated, new Dictionary<string, object?> { ["id"] = "u1" });

        var deliveries = db.WebhookDeliveries.Where(d => d.SubscriptionId == subscription.Id).ToList();
        deliveries.Should().ContainSingle();
        deliveries[0].EventType.Should().Be(WebhookEventType.UserCreated);
    }

    [Fact]
    public async Task PublishEventAsync_SkipsInactiveSubscription()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s =>
        {
            s.IsActive = false;
            s.Events = new List<string> { WebhookEventType.UserCreated };
        });
        var service = CreateService(db, Ok, out _);

        await service.PublishEventAsync(WebhookEventType.UserCreated, new Dictionary<string, object?>());

        db.WebhookDeliveries.Any(d => d.SubscriptionId == subscription.Id).Should().BeFalse();
    }

    [Fact]
    public async Task PublishEventAsync_SkipsSubscriptionNotSubscribedToEvent()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s =>
        {
            s.IsActive = true;
            s.Events = new List<string> { WebhookEventType.OrderCreated };
        });
        var service = CreateService(db, Ok, out _);

        await service.PublishEventAsync(WebhookEventType.UserCreated, new Dictionary<string, object?>());

        db.WebhookDeliveries.Any(d => d.SubscriptionId == subscription.Id).Should().BeFalse();
    }

    [Fact]
    public async Task PublishEventAsync_StampsLastTriggeredAt_OnMatchingSubscription()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s =>
        {
            s.IsActive = true;
            s.Events = new List<string> { WebhookEventType.UserCreated };
            s.LastTriggeredAt = null;
        });
        var service = CreateService(db, Ok, out _);

        await service.PublishEventAsync(WebhookEventType.UserCreated, new Dictionary<string, object?>());

        var reloaded = await db.WebhookSubscriptions.FindAsync(subscription.Id);
        reloaded!.LastTriggeredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishEventAsync_AttemptsImmediateDelivery()
    {
        await using var db = TestDbContextFactory.Create();
        await WebhookSubscriptionBuilder.CreateAsync(db, s =>
        {
            s.IsActive = true;
            s.Events = new List<string> { WebhookEventType.UserCreated };
        });
        var service = CreateService(db, Ok, out var handler);

        await service.PublishEventAsync(WebhookEventType.UserCreated, new Dictionary<string, object?>());

        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessDeliveriesAsync_MarksDelivered_OnSuccessfulResponse()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, d =>
        {
            d.SubscriptionId = subscription.Id;
            d.Status = WebhookDeliveryStatus.Pending;
        });
        var service = CreateService(db, Ok, out _);

        await service.ProcessDeliveriesAsync();

        var reloaded = await db.WebhookDeliveries.FindAsync(delivery.Id);
        reloaded!.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        reloaded.HttpStatus.Should().Be(200);
        reloaded.DeliveredAt.Should().NotBeNull();
        reloaded.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessDeliveriesAsync_SignsPayload_WithSubscriptionSecret()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.Secret = "shared-secret");
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, d =>
        {
            d.SubscriptionId = subscription.Id;
            d.Status = WebhookDeliveryStatus.Pending;
        });
        var service = CreateService(db, Ok, out var handler);

        await service.ProcessDeliveriesAsync();

        var request = handler.Requests.Should().ContainSingle().Which;
        var signature = request.Headers.GetValues("X-Webhook-Signature").Single();
        var payload = delivery.Payload.RootElement.GetRawText();
        SignatureService.VerifySignature(payload, signature, "shared-secret").Should().BeTrue();
    }

    [Fact]
    public async Task ProcessDeliveriesAsync_SetsRetrying_OnFailureBelowRetryCount()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.RetryCount = 3);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, d =>
        {
            d.SubscriptionId = subscription.Id;
            d.Status = WebhookDeliveryStatus.Pending;
            d.AttemptCount = 0;
        });
        var service = CreateService(db, ServerError, out _);

        await service.ProcessDeliveriesAsync();

        var reloaded = await db.WebhookDeliveries.FindAsync(delivery.Id);
        reloaded!.Status.Should().Be(WebhookDeliveryStatus.Retrying);
        reloaded.AttemptCount.Should().Be(1);
        reloaded.NextRetryAt.Should().NotBeNull();
        reloaded.NextRetryAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task ProcessDeliveriesAsync_SetsFailed_WhenAttemptCountReachesRetryCount()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db, s => s.RetryCount = 3);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, d =>
        {
            d.SubscriptionId = subscription.Id;
            d.Status = WebhookDeliveryStatus.Retrying;
            d.AttemptCount = 2;
        });
        var service = CreateService(db, ServerError, out _);

        await service.ProcessDeliveriesAsync();

        var reloaded = await db.WebhookDeliveries.FindAsync(delivery.Id);
        reloaded!.Status.Should().Be(WebhookDeliveryStatus.Failed);
        reloaded.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessDeliveriesAsync_SkipsDelivery_WhenNextRetryIsInTheFuture()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        await WebhookDeliveryBuilder.CreateAsync(db, d =>
        {
            d.SubscriptionId = subscription.Id;
            d.Status = WebhookDeliveryStatus.Retrying;
            d.NextRetryAt = DateTime.UtcNow.AddHours(1);
        });
        var service = CreateService(db, Ok, out var handler);

        await service.ProcessDeliveriesAsync();

        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(WebhookDeliveryStatus.Delivered)]
    [InlineData(WebhookDeliveryStatus.Failed)]
    public async Task ProcessDeliveriesAsync_SkipsDeliveries_InTerminalStatus(string status)
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        await WebhookDeliveryBuilder.CreateAsync(db, d =>
        {
            d.SubscriptionId = subscription.Id;
            d.Status = status;
        });
        var service = CreateService(db, Ok, out var handler);

        await service.ProcessDeliveriesAsync();

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDeliveriesAsync_NoOps_WhenSubscriptionNoLongerExists()
    {
        await using var db = TestDbContextFactory.Create();
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, d =>
        {
            d.SubscriptionId = Guid.NewGuid().ToString();
            d.Status = WebhookDeliveryStatus.Pending;
        });
        var service = CreateService(db, Ok, out var handler);

        await service.ProcessDeliveriesAsync();

        handler.Requests.Should().BeEmpty();
        var reloaded = await db.WebhookDeliveries.FindAsync(delivery.Id);
        reloaded!.Status.Should().Be(WebhookDeliveryStatus.Pending);
    }

    [Fact]
    public async Task RetryFailedDeliveryAsync_ThrowsInvalidOperationException_WhenDeliveryNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, Ok, out _);

        var act = () => service.RetryFailedDeliveryAsync(Guid.NewGuid().ToString());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RetryFailedDeliveryAsync_ThrowsInvalidOperationException_WhenAlreadyDelivered()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, d =>
        {
            d.SubscriptionId = subscription.Id;
            d.Status = WebhookDeliveryStatus.Delivered;
        });
        var service = CreateService(db, Ok, out _);

        var act = () => service.RetryFailedDeliveryAsync(delivery.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RetryFailedDeliveryAsync_RedeliversFailedDelivery_WhenTargetIsNowReachable()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, d =>
        {
            d.SubscriptionId = subscription.Id;
            d.Status = WebhookDeliveryStatus.Failed;
            d.AttemptCount = 3;
        });
        var service = CreateService(db, Ok, out var handler);

        await service.RetryFailedDeliveryAsync(delivery.Id);

        handler.Requests.Should().ContainSingle();
        var reloaded = await db.WebhookDeliveries.FindAsync(delivery.Id);
        reloaded!.Status.Should().Be(WebhookDeliveryStatus.Delivered);
    }
}
