using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Types;
using DotNetMonoRepoTemplate.Utilities;
using ScheduleApi.Jobs;
using ScheduleApi.Tests.Fixtures;
using Xunit;

namespace ScheduleApi.Tests.Jobs;

public sealed class WebhookProcessorJobTests
{
    private static WebhookProcessorJob CreateJob(AppDbContext db, Mock<IHttpClientFactory> httpClientFactory)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(AppDbContext))).Returns(db);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

        return new WebhookProcessorJob(scopeFactory.Object, httpClientFactory.Object);
    }

    private static Mock<IHttpClientFactory> CreateHttpClientFactory(FakeHttpMessageHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(nameof(WebhookProcessorJob)))
            .Returns(() => new HttpClient(handler));
        return httpClientFactory;
    }

    [Fact]
    public async Task ProcessWebhooksAsync_MarksDeliveryAsDelivered_WhenHttpCallSucceeds()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id);

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var job = CreateJob(db, httpClientFactory);

        await job.ProcessWebhooksAsync(CancellationToken.None);

        var updated = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
        Assert.Equal(WebhookDeliveryStatus.Delivered, updated.Status);
        Assert.NotNull(updated.DeliveredAt);
        Assert.Equal(0, updated.AttemptCount);
    }

    [Fact]
    public async Task ProcessWebhooksAsync_SendsCorrectlySignedRequest_WhenDeliveringWebhook()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id);

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var job = CreateJob(db, httpClientFactory);

        await job.ProcessWebhooksAsync(CancellationToken.None);

        var expectedPayload = delivery.Payload.RootElement.GetRawText();
        var expectedSignature = new WebhookSignatureService().GenerateSignature(expectedPayload, subscription.Secret);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(subscription.Url, handler.LastRequest!.RequestUri!.ToString());
        Assert.True(handler.LastRequest.Headers.TryGetValues("X-Webhook-Signature", out var signatureValues));
        Assert.Equal(expectedSignature, signatureValues!.Single());
        Assert.Equal(expectedPayload, handler.LastRequestBody);
    }

    [Fact]
    public async Task ProcessWebhooksAsync_IncrementsAttemptCountAndSchedulesRetry_WhenHttpCallFails()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id);

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var job = CreateJob(db, httpClientFactory);

        var beforeRun = DateTime.UtcNow;
        await job.ProcessWebhooksAsync(CancellationToken.None);

        var updated = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
        Assert.Equal(WebhookDeliveryStatus.Pending, updated.Status);
        Assert.Equal(1, updated.AttemptCount);
        Assert.NotNull(updated.NextRetryAt);
        Assert.InRange(updated.NextRetryAt!.Value, beforeRun.AddSeconds(55), beforeRun.AddSeconds(70));
    }

    [Fact]
    public async Task ProcessWebhooksAsync_AppliesExponentialBackoff_ForSubsequentFailures()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id, d => d.AttemptCount = 2);

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var job = CreateJob(db, httpClientFactory);

        var beforeRun = DateTime.UtcNow;
        await job.ProcessWebhooksAsync(CancellationToken.None);

        var updated = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
        Assert.Equal(WebhookDeliveryStatus.Pending, updated.Status);
        Assert.Equal(3, updated.AttemptCount);
        Assert.NotNull(updated.NextRetryAt);
        Assert.InRange(updated.NextRetryAt!.Value, beforeRun.AddSeconds(235), beforeRun.AddSeconds(250));
    }

    [Fact]
    public async Task ProcessWebhooksAsync_MarksDeliveryAsFailed_WhenMaxAttemptsReached()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id, d => d.AttemptCount = 4);

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var job = CreateJob(db, httpClientFactory);

        await job.ProcessWebhooksAsync(CancellationToken.None);

        var updated = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
        Assert.Equal(WebhookDeliveryStatus.Failed, updated.Status);
        Assert.Equal(5, updated.AttemptCount);
    }

    [Fact]
    public async Task ProcessWebhooksAsync_TreatsThrownException_AsFailedDeliveryAttempt()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id);

        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var job = CreateJob(db, httpClientFactory);

        await job.ProcessWebhooksAsync(CancellationToken.None);

        var updated = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
        Assert.Equal(WebhookDeliveryStatus.Pending, updated.Status);
        Assert.Equal(1, updated.AttemptCount);
        Assert.NotNull(updated.NextRetryAt);
    }

    [Fact]
    public async Task ProcessWebhooksAsync_SkipsDelivery_WhenSubscriptionNoLongerExists()
    {
        await using var db = TestDbContextFactory.Create();
        var delivery = await WebhookDeliveryBuilder.CreateAsync(db, "nonexistent-subscription-id");

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var job = CreateJob(db, httpClientFactory);

        await job.ProcessWebhooksAsync(CancellationToken.None);

        var updated = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
        Assert.Equal(WebhookDeliveryStatus.Pending, updated.Status);
        Assert.Equal(0, updated.AttemptCount);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ProcessWebhooksAsync_IgnoresDeliveries_WhenNotYetDueOrAlreadyResolved()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var eligible = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id);
        var notYetDue = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id, d => d.NextRetryAt = DateTime.UtcNow.AddHours(1));
        var alreadyDelivered = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id, d =>
        {
            d.Status = WebhookDeliveryStatus.Delivered;
            d.DeliveredAt = DateTime.UtcNow;
        });

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var job = CreateJob(db, httpClientFactory);

        await job.ProcessWebhooksAsync(CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        var eligibleAfter = await db.WebhookDeliveries.SingleAsync(d => d.Id == eligible.Id);
        var notYetDueAfter = await db.WebhookDeliveries.SingleAsync(d => d.Id == notYetDue.Id);
        var alreadyDeliveredAfter = await db.WebhookDeliveries.SingleAsync(d => d.Id == alreadyDelivered.Id);
        Assert.Equal(WebhookDeliveryStatus.Delivered, eligibleAfter.Status);
        Assert.Equal(WebhookDeliveryStatus.Pending, notYetDueAfter.Status);
        Assert.Equal(0, notYetDueAfter.AttemptCount);
        Assert.Equal(WebhookDeliveryStatus.Delivered, alreadyDeliveredAfter.Status);
    }

    [Fact]
    public async Task ProcessWebhooksAsync_ProcessesAtMostOneHundredDeliveries_WhenMoreAreEligible()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        for (var i = 0; i < 105; i++)
        {
            await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id);
        }

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var httpClientFactory = CreateHttpClientFactory(handler);
        var job = CreateJob(db, httpClientFactory);

        await job.ProcessWebhooksAsync(CancellationToken.None);

        Assert.Equal(100, handler.RequestCount);
        var remainingPending = await db.WebhookDeliveries.CountAsync(d => d.Status == WebhookDeliveryStatus.Pending);
        Assert.Equal(5, remainingPending);
    }
}
