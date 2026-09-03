---
name: webhook-events
description: Use when implementing outbound webhooks — registering webhook subscriptions, publishing domain events, extending the webhook delivery worker, adding HMAC signature verification, or adding new webhook event types. The EF Core entities (WebhookSubscription, WebhookDelivery) and WebhookDeliveryService/WebhookProcessorJob already exist; this agent wires them into new domain services and extends delivery infrastructure.
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

## What already exists — read these before writing anything

- `common/DotNetMonoRepoTemplate.Database/Entities/WebhookSubscription.cs`, `WebhookDelivery.cs`
- `common/DotNetMonoRepoTemplate.Types/WebhookTypes.cs` — `WebhookDeliveryStatus` (lowercase string constants: `pending`, `delivered`, `failed`, `retrying` — this exact casing matters, see "The case-mismatch bug" below)
- `CustomerApi.Services.WebhookSubscriptionService`/`WebhookDeliveryService` — subscription CRUD + delivery/retry logic, called synchronously inline after the triggering DB write (no queue — see "No queue in the delivery path" below)
- `ScheduleApi.Jobs.WebhookProcessorJob` — the cron-driven retry sweep for deliveries that are `Pending`/`Retrying` and past their `NextRetryAt`
- `DotNetMonoRepoTemplate.Utilities.WebhookSignatureService` — HMAC-SHA256 signing, shared between `customer-api` and `schedule-api`

Do not re-create any of these — extend them.

## The case-mismatch bug (know this before touching delivery-status code)

During the migration, a real pre-existing bug was found and **not reproduced**: the Node-era `schedule-api`'s `webhook-processor.job.ts` locally redefined `WebhookDeliveryStatus` with **uppercase** values (`"PENDING"`), shadowing the correct shared lowercase enum `customer-api` actually wrote to the database (`"pending"`). Because Postgres string comparison is case-sensitive, the Node cron job's query never matched any real row — the retry sweep was silently a no-op in production. The .NET port's `WebhookProcessorJob` uses the correct shared `DotNetMonoRepoTemplate.Types.WebhookDeliveryStatus` constants — if you ever see a local `static class`/string-literal redefinition of delivery statuses anywhere instead of a reference to the shared type, that's this bug re-appearing, not a stylistic variant.

## No queue in the delivery path — this is a faithful port, not a missed opportunity

`WebhookDeliveryService.PublishEventAsync` calls `ProcessDeliveriesAsync()` synchronously, inline, right after creating the delivery rows — not dispatched through `DotNetMonoRepoTemplate.Queue`'s `JobDispatcher`. The Node original didn't use BullMQ here either (`await this.processDeliveries()` after creating each delivery), so this matches it exactly. Don't "improve" this into a queued dispatch without being asked — it's a known, documented scope boundary, not an oversight. `ScheduleApi.Jobs.WebhookProcessorJob` is the actual retry mechanism for anything that didn't succeed on the first synchronous attempt.

## Architecture (as actually implemented)

```
Domain service (e.g. WebhookDeliveryService.PublishEventAsync)
  └─► load active WebhookSubscriptions matching the event type
        └─► create WebhookDelivery rows (Status = Pending), batched SaveChangesAsync
              └─► ProcessDeliveriesAsync() — synchronous, same request
                    ├─► sign payload with HMAC-SHA256 (WebhookSignatureService)
                    ├─► POST to subscriber URL (HttpClient, per-subscription timeout)
                    ├─► update WebhookDelivery.Status/HttpStatus/ResponseBody/DeliveredAt
                    └─► on failure: Status = Retrying, NextRetryAt = exponential backoff

ScheduleApi.Jobs.WebhookProcessorJob (cron, schedule-api)
  └─► sweep WebhookDeliveries where Status ∈ {Pending, Retrying} AND NextRetryAt <= now
        └─► retry each via the same sign+POST+record pattern
```

## Publishing a new event from a domain service

Inject `WebhookDeliveryService`, call after the triggering DB write succeeds:

```csharp
public sealed class OrderService
{
    private readonly AppDbContext _db;
    private readonly WebhookDeliveryService _webhookDelivery;

    public OrderService(AppDbContext db, WebhookDeliveryService webhookDelivery)
    {
        _db = db;
        _webhookDelivery = webhookDelivery;
    }

    public async Task<OrderResponseDto> CreateAsync(CreateOrderDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var order = new Order { /* ... */ CreatedBy = userId, ModifiedBy = userId };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        await _webhookDelivery.PublishEventAsync(
            "order.created",
            new Dictionary<string, object?> { ["id"] = order.Id, ["userId"] = order.UserId, ["totalAmount"] = order.TotalAmount },
            cancellationToken);

        return new OrderResponseDto { IsSuccessful = true, Data = Map(order) };
    }
}
```

Rule: publish AFTER the DB write succeeds, never before or inside the same transaction. A failed delivery attempt should not roll back the triggering DB write — `WebhookProcessorJob`'s retry sweep handles re-delivery.

`PublishEventAsync`'s payload is a `WebhookEventPayload` record (`Event`, `Timestamp`, `Data`) — a real type, not a loose `object`; this was fixed during the migration from an anonymous-typed `object payload` parameter. See `csharp-standards.md` for why that matters generally.

## Webhook management endpoints (`customer-api`, already implemented)

`WebhookEndpoints.cs` under `/api/v1/webhooks` — see the file directly for the current route list rather than assuming parity with any older doc's table; endpoints and permission requirements can drift from documentation faster than they drift from the code itself here.

## Adding a new event type

1. Decide the event-type string (dot-namespaced, e.g. `"order.created"`) — there's no central `WebhookEventType` string-constant class yet the way `WebhookDeliveryStatus` is one; if a domain accumulates several event types, consider adding one to `DotNetMonoRepoTemplate.Types` following that pattern rather than scattering string literals
2. Call `WebhookDeliveryService.PublishEventAsync(eventType, data)` from the relevant service method, after the DB write
3. Document the event payload shape in `documentation/webhooks.md`

## Subscriber signature verification (documentation for integrators)

```csharp
public static bool VerifySignature(string body, string signature, string secret)
{
    var expected = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
    return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(signature), Encoding.UTF8.GetBytes(expected));
}
```

`CryptographicOperations.FixedTimeEquals` is .NET's `crypto.timingSafeEqual` equivalent — use it, never `==`/`.Equals()` for signature comparison (that's what `WebhookSignatureService` already does internally; this snippet is for documenting the contract to external integrators, not a suggestion to re-implement verification client-side in this codebase).

## Critical rules

Never call subscriber URLs from a request handler in a way that blocks the response indefinitely — the current synchronous-inline design still bounds this via the per-subscription `TimeoutSeconds`, but don't remove that bound while "simplifying" the delivery path. Never log or store raw webhook secrets, only HMAC signatures. Never deliver to `localhost`, `127.0.0.1`, or private IP ranges in production (SSRF prevention) — check this is actually enforced before extending the delivery path to accept more dynamic destination URLs. Always use `CryptographicOperations.FixedTimeEquals` for signature comparison. Always publish events after a successful DB write. Always cap stored `ResponseBody` length (matches the existing 1000-character truncation in `AttemptDeliveryAsync`).
