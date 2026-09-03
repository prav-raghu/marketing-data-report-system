# Sentry Wiring Guide

This guide covers how to wire up Sentry error monitoring in any project that was forked from this template. The `common/observability` package is already built — this is purely the integration steps per service.

Sentry is **disabled automatically when `SENTRY_DSN` is not set**, so local development needs no configuration.

---

## Template status

All four backend services in this template are wired. When you fork the template, only the scope prefix changes — replace `@node-mono-repo-template/` with your project scope everywhere below.

| Service | Wired |
|---|---|
| `api-gateway` | ✅ |
| `admin-api` | ✅ |
| `customer-api` | ✅ |
| `schedule-api` | ✅ |

---

## Wiring a backend service (copy-paste checklist)

### 1. Add the workspace dependency

In the service's `package.json`, add under `dependencies`:

```json
"@your-scope/observability": "workspace:*"
```

Then run from the repo root:

```bash
pnpm install
```

---

### 2. `main.ts` — first two lines before any other import

```ts
import { initSentry } from '@your-scope/observability';
initSentry();
```

This must be the very first import so Sentry can instrument the Node.js module graph. If anything else imports first, Sentry misses those modules.

---

### 3. `error-handler.plugin.ts` — call `captureException` for 5xx errors

Add the import at the top:

```ts
import { captureException } from '@your-scope/observability';
```

Inside `setErrorHandler`, alongside `logger.error`:

```ts
if (statusCode >= 500) {
  logger.error('Unhandled request error', error);
  captureException(error);
}
```

Only call `captureException` for 5xx — 4xx errors (validation, auth, not found) are not bugs.

---

### 4. Add Sentry vars to `.env.example`

```dotenv
# Sentry — leave empty to disable error monitoring (local dev)
SENTRY_DSN=
SENTRY_RELEASE=
SENTRY_TRACES_SAMPLE_RATE=0.1
```

---

### 5. Set `SENTRY_DSN` in Coolify per app

Each Coolify application gets its own backend DSN from your Sentry project → Settings → Client Keys. Paste the DSN into the app's environment variables panel in Coolify.

`SENTRY_DSN` blank → Sentry is disabled (safe for local dev and CI).

---

## Environment variable reference

| Variable | Where set | Notes |
|---|---|---|
| `SENTRY_DSN` | Coolify env panel | Empty string disables Sentry entirely. |
| `NODE_ENV` | Coolify env panel | Used as the Sentry environment tag (`production`, `staging`, etc.). |
| `SENTRY_RELEASE` | Coolify env panel or CI | Optional. Tag errors with a release version or git SHA. |
| `SENTRY_TRACES_SAMPLE_RATE` | Coolify env panel | Float `0.0`–`1.0`. Defaults to `0.1` (10% of traces). |

---

## Frontend wiring (separate packages — not via `common/observability`)

### `admin-web` (Vite + React)

Install:

```bash
pnpm add @sentry/browser @sentry/vite-plugin
```

`src/main.tsx` — before `ReactDOM.createRoot`:

```ts
import * as Sentry from '@sentry/browser';

Sentry.init({
  dsn: import.meta.env.VITE_SENTRY_DSN,
  environment: import.meta.env.MODE,
  tracesSampleRate: 0.1,
});
```

`vite.config.ts` — add the plugin for source map upload (only active when `SENTRY_AUTH_TOKEN` is set):

```ts
import { sentryVitePlugin } from '@sentry/vite-plugin';

export default defineConfig({
  plugins: [
    react(),
    sentryVitePlugin({
      org: process.env.SENTRY_ORG,
      project: process.env.SENTRY_PROJECT,
      authToken: process.env.SENTRY_AUTH_TOKEN,
    }),
  ],
  build: { sourcemap: true },
});
```

`.env.example`:

```dotenv
VITE_SENTRY_DSN=
SENTRY_ORG=
SENTRY_PROJECT=
SENTRY_AUTH_TOKEN=
```

---

### `customer-web` (Next.js)

Install:

```bash
pnpm add @sentry/nextjs
```

Run the wizard (optional but generates the config files):

```bash
npx @sentry/wizard@latest -i nextjs
```

Or add manually:

`sentry.client.config.ts`:

```ts
import * as Sentry from '@sentry/nextjs';

Sentry.init({
  dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
  tracesSampleRate: 0.1,
});
```

`sentry.server.config.ts` and `sentry.edge.config.ts` — same content, same `dsn`.

`next.config.ts`:

```ts
import { withSentryConfig } from '@sentry/nextjs';

const nextConfig = { /* your existing config */ };

export default withSentryConfig(nextConfig, {
  org: process.env.SENTRY_ORG,
  project: process.env.SENTRY_PROJECT,
  authToken: process.env.SENTRY_AUTH_TOKEN,
  silent: true,
});
```

`.env.example`:

```dotenv
NEXT_PUBLIC_SENTRY_DSN=
SENTRY_ORG=
SENTRY_PROJECT=
SENTRY_AUTH_TOKEN=
```

---

## Summary of what `common/observability` exports

```ts
import { initSentry }        from '@your-scope/observability'; // call once in main.ts
import { captureException }  from '@your-scope/observability'; // call in error-handler for 5xx
import { resolveSentryConfig } from '@your-scope/observability'; // rarely needed directly
```
