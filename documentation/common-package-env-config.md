# Common Package Environment Configuration

Reference for the `EnvConfig` pattern used across `common/*` packages, and the Redis TLS behavior it drives. Linked from `.claude/commands/deploy-coolify.md`.

## The pattern

Every `common/*` package that reads environment variables validates them through an AJV-backed `EnvConfig` class local to that package (see `.claude/instructions/env-config.instructions.md` for the full pattern and the backend-service version of this same convention). Each package only declares the env vars it actually needs.

Example — `common/cache/src/config/env.config.ts`:

```typescript
const envSchema = {
    type: "object",
    required: ["REDIS_URL"],
    properties: {
        REDIS_URL: { type: "string", minLength: 1 },
        REDIS_TLS_REJECT_UNAUTHORIZED: { type: "string" },
    },
    additionalProperties: true,
} as const;
```

`REDIS_URL` is required — there is no discrete `REDIS_HOST`/`REDIS_PORT`/`REDIS_PASSWORD` fallback. `redis.service.ts` throws immediately at startup (caught and logged, cache runs in degraded no-op mode) if `REDIS_URL` is missing, rather than silently connecting to `127.0.0.1:6379`. Local dev still needs a real `REDIS_URL` in `.env` — point it at `devops/docker-compose.dev.yml`'s local Redis container, e.g. `redis://localhost:6379`.

## Redis TLS behavior (`common/cache/src/services/redis.service.ts`)

The Redis client decides whether to use TLS based on the connection string scheme, not a separate flag:

```typescript
const redisUrl = EnvConfig.get("REDIS_URL");
if (!redisUrl) {
    throw new Error("REDIS_URL is required — no discrete REDIS_HOST/REDIS_PORT fallback is supported");
}
const rejectUnauthorized = EnvConfig.get("REDIS_TLS_REJECT_UNAUTHORIZED") === "true";
const useTls = redisUrl.startsWith("rediss://");
const tlsOptions: RedisOptions = useTls ? { tls: { rejectUnauthorized } } : {};
```

- A `rediss://` URL (double "s") turns TLS on automatically — this is what Coolify's managed Redis resource uses.
- `REDIS_TLS_REJECT_UNAUTHORIZED` controls certificate validation, and **defaults to rejecting the connection unless explicitly set to the string `"true"`** — so it must be set to `"false"` for Coolify-managed Redis, which presents a self-signed certificate. This is why `docker-compose.yaml` sets `REDIS_TLS_REJECT_UNAUTHORIZED: ${REDIS_TLS_REJECT_UNAUTHORIZED:-false}` for every backend service — the default of `false` accepts the managed self-signed cert without requiring an explicit env var in Coolify.
- Set `REDIS_TLS_REJECT_UNAUTHORIZED=true` only if a CA-signed Redis certificate is in use (i.e. not Coolify's default managed Redis).

The connection also retries up to 3 times with capped exponential backoff (`Math.min(times * 100, 2000)` ms) before giving up and logging a warning — the service degrades to running without cache rather than crashing when Redis is briefly unavailable.

## Diagnosing connection errors

Errors surface in this order as each layer gets fixed, per `.claude/commands/deploy-coolify.md`'s "Managed Redis uses TLS with a self-signed cert" section:

1. `REDIS_URL is required` thrown at startup — `REDIS_URL` isn't set in the environment at all.
2. `EAI_AGAIN <random-id>` — DNS failure; the service isn't on the same Docker network as the managed Redis resource (needs the `coolify` external network).
3. `self-signed certificate in certificate chain` — network is correct, but TLS cert validation is rejecting Coolify's self-signed cert (`REDIS_TLS_REJECT_UNAUTHORIZED` needs to be `false`).
4. `Redis connected successfully` — all three layers are correct.
