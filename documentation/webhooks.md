# Webhook Events

Catalog of outbound webhook event types and their payload shapes. Add a new entry here whenever a new `WebhookEventType` is introduced, per `.claude/agents/webhook-events.md`'s "Adding new event types" step.

See `.claude/agents/webhook-events.md` for the delivery architecture (event bus → queue → worker → HMAC-signed POST) and `.claude/instructions/webhook-events.instructions.md` for the conventions each publishing service must follow (publish after write, minimal payloads, SSRF prevention).

## Envelope

Every webhook delivery POSTs this shape, defined in `common/types/src/webhook.types.ts`:

```typescript
interface WebhookPayload {
  event: WebhookEventType;
  timestamp: string;   // ISO 8601
  data: Record<string, unknown>;
}
```

Signed with `X-Webhook-Signature: sha256=<hmac>` (HMAC-SHA256 over the raw JSON body using the subscription's secret), plus `X-Webhook-Event` and `X-Webhook-Timestamp` headers. Verify with `crypto.timingSafeEqual`, never `===` — see `webhook-events.md` for the verification snippet.

## Event types (`WebhookEventType` enum)

| Event | `data` payload | Published by |
|---|---|---|
| `user.created` | `{ id, ...fields TBD by UserService }` | `UserService.create` |
| `user.updated` | `{ id, ...changed fields TBD by UserService }` | `UserService.update` |
| `user.deleted` | `{ id }` | `UserService.softDelete` |
| `order.created` | `{ id, userId, status, totalAmount }` | `OrderService.create` |
| `order.updated` | `{ id, ...changed fields TBD by OrderService }` | `OrderService.update` |
| `order.completed` | `{ id, ...fields TBD by OrderService }` | `OrderService` (completion flow) |
| `payment.success` | `{ id, orderId, amount, ...fields TBD by PaymentService }` | `PaymentService` |
| `payment.failed` | `{ id, orderId, reason, ...fields TBD by PaymentService }` | `PaymentService` |

`order.created`'s payload shape above is confirmed from the worked example in `.claude/agents/webhook-events.md`. The other rows list the enum members that exist in `common/types/src/webhook.types.ts` today — their exact payload fields aren't fixed yet because the publishing services haven't been built. **When implementing a publisher for one of these events, fill in its exact `data` shape here** so subscribers have one place to check instead of reading service source.

Payloads are intentionally minimal — IDs and status fields only, never passwords, tokens, PII, or internal system IDs (see `webhook-events.instructions.md`). Subscribers fetch full records from the API if they need more than the event tells them.

## Adding a new event type

1. Add the member to `WebhookEventType` in `common/types/src/webhook.types.ts`.
2. Publish it from the relevant service method, after the DB write succeeds — see `webhook-events.md` for the `EventBusService.publish()` pattern.
3. Add a row to the table above with the payload shape and publishing service.
