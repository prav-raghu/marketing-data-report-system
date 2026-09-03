---
applyTo: "apps/backend/**/Services/**,common/DotNetMonoRepoTemplate.Queue/**"
description: "Outbound webhook conventions — always HMAC-signed, always post-write, delivery is synchronous-inline with a cron retry sweep, not queue-backed"
---

When publishing domain events or delivering webhooks — see the `webhook-events` agent for the full picture; this is the condensed rule set.

## Publish After Write, Never Before

```csharp
var entity = new Entity { /* ... */ };
_db.Entities.Add(entity);
await _db.SaveChangesAsync(cancellationToken);
await _webhookDelivery.PublishEventAsync("entity.created", new Dictionary<string, object?> { ["id"] = entity.Id }, cancellationToken);
```

If publish/delivery fails, the DB write still stands — `ScheduleApi.Jobs.WebhookProcessorJob`'s cron sweep retries delivery on its own schedule, the entity is not lost. Never wrap the delivery call in the same `AppDbContext.Database.BeginTransactionAsync()` scope as the triggering DB write.

## Delivery is synchronous-inline, not queue-backed — read this before assuming otherwise

`WebhookDeliveryService.PublishEventAsync` calls `ProcessDeliveriesAsync()` **synchronously, in the same request**, right after creating the `WebhookDelivery` rows — it does **not** go through `DotNetMonoRepoTemplate.Queue`'s `JobDispatcher`. This is a faithful port of what the Node original did (`await this.processDeliveries()` inline, no BullMQ here), not a missed opportunity — see `webhook-events.md` (the agent) for the full reasoning and the case-mismatch bug this area of the code has history with. `ScheduleApi.Jobs.WebhookProcessorJob` (a cron job, not a queue worker) is the actual retry mechanism for anything that didn't succeed on the first synchronous attempt.

## Which Services Publish Events

Inject `WebhookDeliveryService` only in services that own state changes external systems might care about — e.g. an `OrderService` publishing `order.created`/`order.updated`. Read-only services (search, reporting) never publish events.

## Event Payload Shape

Keep payloads minimal — IDs and status fields only, via the `WebhookEventPayload` record (`Event`, `Timestamp`, `Data`). Subscribers fetch full data from your API if they need it:

```csharp
await _webhookDelivery.PublishEventAsync(
    "order.created",
    new Dictionary<string, object?> { ["id"] = order.Id, ["userId"] = order.UserId, ["status"] = order.Status, ["totalAmount"] = order.TotalAmount },
    cancellationToken);
```

NEVER include passwords, tokens, PII fields, or internal system IDs in webhook payloads.

## Delivery + Retry Locations

- `CustomerApi.Services.WebhookDeliveryService` — subscription CRUD, first-attempt delivery, HMAC signing (`DotNetMonoRepoTemplate.Utilities.WebhookSignatureService`)
- `ScheduleApi.Jobs.WebhookProcessorJob` — cron-driven retry sweep for anything left `Pending`/`Retrying` past its `NextRetryAt`

Not customer-api-and-admin-api-both — retry logic lives only in `schedule-api`, matching the Node original's split.

## SSRF Prevention

Before delivering to a subscriber URL, validate it is not a private address:

```csharp
var uri = new Uri(subscription.Url);
if (uri.Host is "localhost" or "127.0.0.1" or "::1" or "0.0.0.0")
{
    throw new InvalidOperationException("Webhook delivery to private addresses is not allowed");
}
```

In production, also block RFC-1918 address ranges (10.x.x.x, 172.16.x.x, 192.168.x.x). Verify this check actually exists in the current delivery path before assuming it's covered — it wasn't confirmed present during the migration's port pass.

## Delivery History Retention

`webhook_deliveries` rows accumulate fast. Add a scheduled cleanup job in `schedule-api` (via `CronSchedulerHostedService`, the same mechanism `WebhookProcessorJob` uses) that deletes delivered entries older than 30 days and failed entries older than 7 days. Never let this table grow unbounded. Not yet implemented as of this writing.
