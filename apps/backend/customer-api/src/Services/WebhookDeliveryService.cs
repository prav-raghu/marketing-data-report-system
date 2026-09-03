using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Types;
using DotNetMonoRepoTemplate.Utilities;

namespace CustomerApi.Services;

public sealed record WebhookDeliveryResult
{
    public required bool Success { get; init; }
    public int? HttpStatus { get; init; }
    public string? ResponseBody { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record WebhookEventPayload
{
    public required string Event { get; init; }
    public required string Timestamp { get; init; }
    public required IReadOnlyDictionary<string, object?> Data { get; init; }
}

public sealed class WebhookDeliveryService
{
    private const int BatchSize = 50;

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebhookSignatureService _signatureService = new();
    private readonly Logger _logger = new(nameof(WebhookDeliveryService));

    public WebhookDeliveryService(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public async Task PublishEventAsync(string eventType, IReadOnlyDictionary<string, object?> data, CancellationToken cancellationToken = default)
    {
        var payload = new WebhookEventPayload
        {
            Event = eventType,
            Timestamp = DateTime.UtcNow.ToString("O"),
            Data = data,
        };
        var payloadJson = JsonSerializer.Serialize(payload);

        var subscriptions = await _db.WebhookSubscriptions
            .Where(s => s.IsActive && s.Events.Contains(eventType))
            .ToListAsync(cancellationToken);

        var triggeredAt = DateTime.UtcNow;
        foreach (var subscription in subscriptions)
        {
            _db.WebhookDeliveries.Add(new WebhookDelivery
            {
                SubscriptionId = subscription.Id,
                EventType = eventType,
                Payload = JsonDocument.Parse(payloadJson),
                Status = WebhookDeliveryStatus.Pending,
                AttemptCount = 0,
            });
            subscription.LastTriggeredAt = triggeredAt;
        }

        if (subscriptions.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        await ProcessDeliveriesAsync(cancellationToken);
    }

    public async Task ProcessDeliveriesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var pendingDeliveries = await _db.WebhookDeliveries
            .Include(d => d.Subscription)
            .Where(d =>
                (d.Status == WebhookDeliveryStatus.Pending || d.Status == WebhookDeliveryStatus.Retrying)
                && (d.NextRetryAt == null || d.NextRetryAt <= now))
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var failedCount = 0;
        foreach (var delivery in pendingDeliveries)
        {
            try
            {
                await DeliverWebhookAsync(delivery, cancellationToken);
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.Error("Webhook delivery threw", ex);
            }
        }

        if (failedCount > 0)
        {
            _logger.Warn($"Webhook delivery: {failedCount}/{pendingDeliveries.Count} deliveries failed");
        }
    }

    public async Task RetryFailedDeliveryAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        var delivery = await _db.WebhookDeliveries
            .Include(d => d.Subscription)
            .FirstOrDefaultAsync(d => d.Id == deliveryId, cancellationToken);

        if (delivery is null)
        {
            throw new InvalidOperationException($"Delivery {deliveryId} not found");
        }
        if (delivery.Status == WebhookDeliveryStatus.Delivered)
        {
            throw new InvalidOperationException("Cannot retry a successful delivery");
        }

        await DeliverWebhookAsync(delivery, cancellationToken);
    }

    private async Task DeliverWebhookAsync(WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.Subscription is null)
        {
            return;
        }

        var payloadString = delivery.Payload.RootElement.GetRawText();
        var signature = _signatureService.GenerateSignature(payloadString, delivery.Subscription.Secret);

        _logger.Info($"Delivering webhook {delivery.Id} to {delivery.Subscription.Url}");

        var result = await AttemptDeliveryAsync(
            delivery.Subscription.Url,
            payloadString,
            signature,
            delivery.Subscription.TimeoutSeconds,
            cancellationToken);

        var attemptCount = delivery.AttemptCount + 1;
        var shouldRetry = !result.Success && attemptCount < delivery.Subscription.RetryCount;

        if (result.Success)
        {
            delivery.Status = WebhookDeliveryStatus.Delivered;
            delivery.HttpStatus = result.HttpStatus;
            delivery.ResponseBody = result.ResponseBody;
            delivery.DeliveredAt = DateTime.UtcNow;
            delivery.NextRetryAt = null;
        }
        else if (shouldRetry)
        {
            delivery.Status = WebhookDeliveryStatus.Retrying;
            delivery.AttemptCount = attemptCount;
            delivery.HttpStatus = result.HttpStatus;
            delivery.ErrorMessage = result.ErrorMessage;
            delivery.NextRetryAt = CalculateNextRetry(attemptCount);
        }
        else
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
            delivery.HttpStatus = result.HttpStatus;
            delivery.ErrorMessage = result.ErrorMessage;
            delivery.NextRetryAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<WebhookDeliveryResult> AttemptDeliveryAsync(
        string url,
        string payload,
        string signature,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Webhook-Signature", signature);
            request.Headers.Add("User-Agent", "WebhookService/1.0");

            var client = _httpClientFactory.CreateClient(nameof(WebhookDeliveryService));
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncated = responseBody.Length > 1000 ? responseBody[..1000] : responseBody;

            return new WebhookDeliveryResult
            {
                Success = response.IsSuccessStatusCode,
                HttpStatus = (int)response.StatusCode,
                ResponseBody = truncated,
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Webhook delivery failed: {ex.Message}", ex);
            return new WebhookDeliveryResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static DateTime CalculateNextRetry(int attemptCount)
    {
        const int baseDelaySeconds = 60;
        const int maxDelaySeconds = 3600;
        var delaySeconds = Math.Min(baseDelaySeconds * Math.Pow(2, attemptCount - 1), maxDelaySeconds);
        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }
}
