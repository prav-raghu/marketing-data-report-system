---
name: enterprise-scale
description: Use as a cross-cutting reference when designing for 1M+ concurrent users — caching strategy, queue offloading, database scale patterns, frontend performance, or API client resilience. Trigger on "scale this", "enterprise scale", "high traffic", or when reviewing whether a feature is production-ready for heavy load.
tools: Read, Grep, Glob
model: inherit
---

All code in this monorepo is designed for enterprise scale (1M+ concurrent users) by default. These are the cross-cutting concerns to apply. Backend is ASP.NET Core (.NET 10)/EF Core now; frontend is unchanged (React/Next.js).

## Backend services (ASP.NET Core)

Stateless design — no in-memory sessions, no local file state, all state in Postgres or Redis. Horizontal scaling — every service runs behind a load balancer with N replicas, no singleton assumptions (DI-registered `Scoped`/`Singleton` services are per-request/per-process, not shared mutable state across replicas).

Graceful shutdown is handled by the ASP.NET Core host automatically — `WebApplication.Run()` hooks `SIGTERM`/`SIGINT` via `IHostApplicationLifetime` and drains in-flight requests. Nothing to hand-write per service unless it owns a resource the host doesn't already manage.

Every service exposes `GET /api/v1/ping` (liveness) and `GET /api/v1/ready` (readiness — only 200 if DB + Redis are healthy). Response compression: ASP.NET Core's `Microsoft.AspNetCore.ResponseCompression` middleware is not currently wired into any ported service — evaluate adding it rather than assuming parity with the Node era's Fastify compression plugin. `X-Correlation-Id` propagated through `RequestLoggingMiddleware` and logged on both request and response.

Rate limiting tiers: see `rules/backend.md` for the authoritative table (global 200/min per IP, `auth` 10/min, `sensitive` 5/min, `adminOperations` 100/min) — configured via `Microsoft.AspNetCore.RateLimiting`'s `PartitionedRateLimiter`, not a Fastify plugin.

## Database (EF Core + PostgreSQL)

Connection pooling is Npgsql's default pooling behavior via `DATABASE_URL` — tunable via connection-string params (`Maximum Pool Size=...`) if a service needs to override defaults; there's no `DATABASE_POOL_SIZE` env var wired up the way the Node era had one. Read-write separation via a read-replica connection string is not currently configured — if the project needs it, that's new infrastructure work, not a flag to flip. Query efficiency: always `.Select()` project on list/search queries, never materialize full entities on paginated endpoints. Cursor pagination for any customer-facing list that could exceed 10K rows — never `.Skip()` + `.Take()` on those. Optimistic locking via an `int Version` property with `IsConcurrencyToken()` on concurrent-write entities (orders, inventory, carts) — see `ef-core.md`. Batch writes: EF Core doesn't have a `createMany`/`updateMany` equivalent that skips change-tracking overhead the way Prisma's bulk operations did — for genuinely large batch writes, evaluate `ExecuteUpdateAsync`/`ExecuteDeleteAsync` (EF Core 7+'s bulk-update API, bypasses the change tracker) instead of looping `SaveChangesAsync` calls.

**No N+1 queries** — the most common way a feature that looked fine in dev falls over under load. See `ef-core.md`'s dedicated section and the three real fixes made during the migration.

## Caching (Redis via `RedisCacheService`)

Cache-aside on every read-heavy method: check Redis first, fall back to Postgres, then populate cache. TTL by entity type: catalog/reference data 15 min, user profiles 5 min, config/settings 30 min, transactional data 1 min or skip cache entirely. Cache key namespacing: `{service}:{entity}:{id}`. Invalidation deletes specific keys AND wildcard-matches list cache keys (via `IServer.KeysAsync(pattern: ...)`, as `TokenService.InvalidateAllUserRefreshTokensAsync` already does for a different purpose — same technique). For ultra-hot keys, avoid cache stampede via short TTL plus stale-while-revalidate or a distributed lock (`RedisCacheService` doesn't currently implement either — evaluate before assuming it's covered).

## Queue (`DotNetMonoRepoTemplate.Queue`, Hangfire-backed)

Offload to queues where genuinely needed: PDF/report generation, image processing/resizing, scheduled jobs (reminders, cleanups, analytics). **This scaffold is confirmed to have zero real callers as of the migration** — `WebhookDeliveryService`'s delivery path deliberately does *not* go through it (see `webhook-events.md`), matching what the Node original did. Before leaning on `JobDispatcher` for something load-bearing, verify it actually behaves the way you expect under real load — it was ported as a faithful translation layer, not validated against production traffic. Queue jobs must be idempotent — safe to retry on failure.

## Frontend (React / Next.js) — unchanged by the backend migration

Code splitting via `React.lazy()` and `Suspense` on routes and heavy components. Virtual scrolling (`react-window` or `@tanstack/virtual`) for lists over 100 items. Debounced search inputs, 300ms minimum, before triggering API calls. Optimistic UI on mutations — update immediately, roll back on error. Bundle size monitored via Vite `rollupOptions` manual chunks, initial JS under 200KB gzipped. Image optimization via Next.js `<Image>` (customer-web) or lazy-loaded `<img loading="lazy">` (admin-web). Error boundaries wrap every page and major feature section with retry UI.

## API client (Axios) — unchanged

Retry with exponential backoff — 3 retries at 1s/2s/4s on 5xx and network errors. Request deduplication handled by React Query — never bypass with raw Axios in components. On a 503, show degraded UI with a retry option rather than hammering the endpoint. Note: backend tokens are returned in the JSON response body (not cookies — see `jwt-security.md`), so the Axios client's interceptor is the sole place tokens are attached to outgoing requests and held in memory — never persisted client-side.

## Environment variables (scale-related)

Every backend service's `.env.example` includes what its `<Service>Options`/`<Service>OptionsValidator` actually reads — check the real file rather than assuming a fixed template, since not every service needs every tier. There is no `DATABASE_POOL_SIZE`/`QUEUE_CONCURRENCY` env var currently wired up the way the Node era's illustrative template implied — don't add one speculatively without a concrete tuning need.
