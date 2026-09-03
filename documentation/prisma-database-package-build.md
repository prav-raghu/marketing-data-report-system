# Prisma Database Package Build

Reference for how `common/database` builds and ships its generated Prisma client. Linked from `.claude/commands/deploy-coolify.md` and `.claude/agents/deployment-coolify.md`.

## Build script

`common/database/package.json`:

```json
{
    "scripts": {
        "build": "prisma generate && tsc -p tsconfig.json",
        "typecheck": "prisma generate && tsc --noEmit -p tsconfig.json",
        "prisma:generate": "prisma generate",
        "prisma:migrate": "prisma migrate dev",
        "prisma:migrate:deploy": "prisma migrate deploy",
        "prisma:seed": "tsx prisma/seed.ts"
    }
}
```

`build` runs `prisma generate` (CJS client output) and `tsc` in a single step. This means:

- There is no separate `prisma generate` step anywhere else in the Dockerfiles — the database package's own `build` script handles it.
- No runner-stage regeneration is needed — the compiled Prisma client travels inside the deployed `dist` directory produced by this build.
- Any Dockerfile or CI step for a service that depends on `@node-mono-repo-template/database` only needs `pnpm --filter "@node-mono-repo-template/<service>..." build`, which pulls in the database package's build via the workspace dependency graph.

## Prisma 7 configuration

The datasource URL is declared in `prisma.config.ts`, not in `schema.prisma` (Prisma 7 no longer supports a `url` in the schema's `datasource` block):

```typescript
// common/database/prisma.config.ts
import "dotenv/config";
import { defineConfig } from "prisma/config";

export default defineConfig({
    schema: "./prisma/schema.prisma",
    migrations: {
        path: "./prisma/migrations",
        seed: "tsx prisma/seed.ts",
    },
    datasource: {
        url: process.env.DATABASE_URL ?? "postgresql://localhost:5432/placeholder",
    },
});
```

Because of this, `prisma migrate deploy` (and any other Prisma CLI invocation) must run from **inside** the deployed `common/database` package directory so the CLI auto-discovers `prisma.config.ts` and its relative paths resolve — do not pass `--schema=...` to point at the schema file directly, since that bypasses the config and Prisma raises *"The datasource property url is no longer supported in schema files."*

## What must ship in the production package

`pnpm deploy --prod` copies only what `package.json`'s `files` array declares — currently:

```json
"files": ["dist", "prisma"]
```

For migrations and seeding to work at runtime, the deployed package needs:

- `prisma/schema.prisma` and `prisma/migrations/` — covered by the `prisma` glob.
- The compiled client and services — covered by `dist`.
- `prisma` and `tsx` as runtime `dependencies` (not `devDependencies`) — both are already declared as such in `package.json`, so the Prisma CLI and the `.ts` config loader survive the production prune.

`prisma.config.ts` lives at the package root, outside both `dist/` and `prisma/`. If a future change to the `files` array drops it, `prisma migrate deploy` will fail to find the datasource URL inside the deployed container (symptom: `find /app -name prisma.config.ts` returns nothing, or a "datasource url is no longer supported" error) — verify it's still reachable in the deployed image whenever this package's `files` array or build output changes.

## Seeding

`prisma:seed` runs `tsx prisma/seed.ts` directly — no separate compile step, since `tsx` is a runtime dependency. See `.claude/agents/deployment-coolify.md`'s "Seeding (manual)" section for how this is invoked against a live deployment.
