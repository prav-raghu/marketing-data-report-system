using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Types;
using DotNetMonoRepoTemplate.Utilities;
using Entities = DotNetMonoRepoTemplate.Database.Entities;

namespace ScheduleApi.Jobs;

public sealed class WebhookProcessorJob
{
    private const int BatchSize = 100;
    private const int MaxAttempts = 5;
    private const int BaseDelaySeconds = 60;
    private const int MaxDelaySeconds = 3600;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Logger _logger = new(nameof(WebhookProcessorJob));

    public WebhookProcessorJob(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
    }

    public async Task ProcessWebhooksAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            var deliveries = await db.WebhookDeliveries
                .Include(delivery => delivery.Subscription)
                .Where(delivery => delivery.Status == WebhookDeliveryStatus.Pending && delivery.NextRetryAt != null && delivery.NextRetryAt <= now)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var delivery in deliveries)
            {
                await DeliverWebhookAsync(db, delivery, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error processing webhooks", ex);
        }
    }

    private async Task DeliverWebhookAsync(AppDbContext db, Entities.WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.Subscription is null)
        {
            return;
        }

        try
        {
            var payloadJson = delivery.Payload.RootElement.GetRawText();
            var signature = WebhookSignatureService.GenerateSignature(payloadJson, delivery.Subscription.Secret);

            using var request = new HttpRequestMessage(HttpMethod.Post, delivery.Subscription.Url)
            {
                Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Webhook-Signature", signature);

            var client = _httpClientFactory.CreateClient(nameof(WebhookProcessorJob));
            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = WebhookDeliveryStatus.Delivered;
                delivery.DeliveredAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                _logger.Info("Webhook delivered successfully", new Dictionary<string, object?> { ["deliveryId"] = delivery.Id });
            }
            else
            {
                await HandleFailedDeliveryAsync(db, delivery, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(
                "Webhook delivery failed",
                new Dictionary<string, object?> { ["deliveryId"] = delivery.Id, ["error"] = ex.Message });
            await HandleFailedDeliveryAsync(db, delivery, cancellationToken);
        }
    }

    private static async Task HandleFailedDeliveryAsync(AppDbContext db, Entities.WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        var newAttemptCount = delivery.AttemptCount + 1;
        delivery.AttemptCount = newAttemptCount;

        if (newAttemptCount >= MaxAttempts)
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
        }
        else
        {
            delivery.Status = WebhookDeliveryStatus.Pending;
            delivery.NextRetryAt = CalculateNextRetry(newAttemptCount);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static DateTime CalculateNextRetry(int attemptCount)
    {
        var delaySeconds = Math.Min(BaseDelaySeconds * Math.Pow(2, attemptCount - 1), MaxDelaySeconds);
        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }
}
