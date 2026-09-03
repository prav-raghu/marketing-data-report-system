using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Types;
using DotNetMonoRepoTemplate.Utilities;
using Entities = DotNetMonoRepoTemplate.Database.Entities;

namespace CustomerApi.Services;

public sealed record WebhookOperationResult<T>
{
    public required bool IsSuccessful { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
}

public sealed class WebhookSubscriptionService
{
    private const string SubscriptionNotFound = "Subscription not found";

    private readonly AppDbContext _db;

    public WebhookSubscriptionService(AppDbContext db) => _db = db;

    public async Task<WebhookOperationResult<Entities.WebhookSubscription>> CreateSubscriptionAsync(
        CreateWebhookSubscriptionDto dto,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        var secret = string.IsNullOrEmpty(dto.Secret) ? WebhookSignatureService.GenerateSecret() : dto.Secret;

        var subscription = new Entities.WebhookSubscription
        {
            Url = dto.Url,
            Secret = secret,
            Events = dto.Events.ToList(),
            RetryCount = dto.RetryCount ?? 3,
            TimeoutSeconds = dto.TimeoutSeconds ?? 30,
            CreatedBy = createdBy,
            ModifiedBy = createdBy,
        };

        _db.WebhookSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(cancellationToken);

        return new WebhookOperationResult<Entities.WebhookSubscription> { IsSuccessful = true, Data = subscription };
    }

    public async Task<WebhookOperationResult<Entities.WebhookSubscription>> GetSubscriptionAsync(
        string id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.CreatedBy == userId, cancellationToken);

        return subscription is null
            ? new WebhookOperationResult<Entities.WebhookSubscription> { IsSuccessful = false, Message = SubscriptionNotFound }
            : new WebhookOperationResult<Entities.WebhookSubscription> { IsSuccessful = true, Data = subscription };
    }

    public async Task<WebhookOperationResult<IReadOnlyList<Entities.WebhookSubscription>>> ListSubscriptionsAsync(
        string userId,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = _db.WebhookSubscriptions.Where(s => s.CreatedBy == userId);
        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        var subscriptions = await query.OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
        return new WebhookOperationResult<IReadOnlyList<Entities.WebhookSubscription>> { IsSuccessful = true, Data = subscriptions };
    }

    public async Task<WebhookOperationResult<Entities.WebhookSubscription>> UpdateSubscriptionAsync(
        string id,
        string userId,
        UpdateWebhookSubscriptionDto dto,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.CreatedBy == userId, cancellationToken);
        if (subscription is null)
        {
            return new WebhookOperationResult<Entities.WebhookSubscription> { IsSuccessful = false, Message = SubscriptionNotFound };
        }

        if (dto.Url is not null)
        {
            subscription.Url = dto.Url;
        }
        if (dto.Secret is not null)
        {
            subscription.Secret = dto.Secret;
        }
        if (dto.Events is not null)
        {
            subscription.Events = dto.Events.ToList();
        }
        if (dto.IsActive.HasValue)
        {
            subscription.IsActive = dto.IsActive.Value;
        }
        if (dto.RetryCount.HasValue)
        {
            subscription.RetryCount = dto.RetryCount.Value;
        }
        if (dto.TimeoutSeconds.HasValue)
        {
            subscription.TimeoutSeconds = dto.TimeoutSeconds.Value;
        }
        subscription.ModifiedBy = userId;

        await _db.SaveChangesAsync(cancellationToken);
        return new WebhookOperationResult<Entities.WebhookSubscription> { IsSuccessful = true, Data = subscription };
    }

    public async Task<WebhookOperationResult<string>> DeleteSubscriptionAsync(
        string id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.CreatedBy == userId, cancellationToken);
        if (subscription is null)
        {
            return new WebhookOperationResult<string> { IsSuccessful = false, Message = SubscriptionNotFound };
        }

        _db.WebhookSubscriptions.Remove(subscription);
        await _db.SaveChangesAsync(cancellationToken);
        return new WebhookOperationResult<string> { IsSuccessful = true, Data = id };
    }

    public async Task<WebhookOperationResult<IReadOnlyList<Entities.WebhookDelivery>>> GetDeliveriesAsync(
        string subscriptionId,
        string userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.CreatedBy == userId, cancellationToken);
        if (subscription is null)
        {
            return new WebhookOperationResult<IReadOnlyList<Entities.WebhookDelivery>>
            {
                IsSuccessful = false,
                Data = Array.Empty<Entities.WebhookDelivery>(),
                Message = SubscriptionNotFound,
            };
        }

        var deliveries = await _db.WebhookDeliveries
            .Where(d => d.SubscriptionId == subscriptionId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new WebhookOperationResult<IReadOnlyList<Entities.WebhookDelivery>> { IsSuccessful = true, Data = deliveries };
    }

    public async Task<WebhookOperationResult<Entities.WebhookSubscription>> RegenerateSecretAsync(
        string id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.CreatedBy == userId, cancellationToken);
        if (subscription is null)
        {
            return new WebhookOperationResult<Entities.WebhookSubscription> { IsSuccessful = false, Message = SubscriptionNotFound };
        }

        subscription.Secret = WebhookSignatureService.GenerateSecret();
        subscription.ModifiedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);

        return new WebhookOperationResult<Entities.WebhookSubscription> { IsSuccessful = true, Data = subscription };
    }
}
