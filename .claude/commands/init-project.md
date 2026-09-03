---
description: Initialize this monorepo template for a new project — rename all namespace references, generate secrets, and create .env files
argument-hint: <project slug, e.g. "burger-shop"> [org scope, e.g. "zynkosi"]
---

Initialize this monorepo for a new project: $ARGUMENTS

Parse the arguments:
- First argument = project slug (lowercase, hyphenated, e.g. `burger-shop`)
- Second argument (optional) = org prefix (e.g. `zynkosi` → scope becomes `@zynkosi/burger-shop`). If not provided, scope = `@{slug}` (e.g. `@burger-shop`)

If arguments are missing, ask the user for the project slug before proceeding.

## Step 1 — Confirm before touching anything

Show the user exactly what will change:
- Old npm scope (frontend/mobile only): `node-mono-repo-template` / `@node-mono-repo-template`
- Old C# namespace/library prefix (backend, separate substitution): `DotNetMonoRepoTemplate` (PascalCase — `DotNetMonoRepoTemplate.Types`, `DotNetMonoRepoTemplate.Database`, etc., and the `DotNetMonoRepoTemplate.sln` filename)
- New npm scope: `{slug}` / `{scope}`; new C# namespace prefix: the project slug in PascalCase (e.g. `burger-shop` → `BurgerShop`)
- Files to update (npm scope): all .ts, .tsx, .json, .md, .yaml, .yml, .properties, Dockerfile files containing `node-mono-repo-template`
- Files to update (C# namespace): every `.csproj`/`.sln` file, every `.cs` file's `namespace`/`using` lines, `Directory.Build.props`/`Directory.Packages.props` if they reference the prefix, and every `.claude/agents/`/`.claude/commands/`/`.claude/instructions/` file mentioning `DotNetMonoRepoTemplate`
- .env files to create: 8 files from their .env.example templates (see Step 5 — `common/database/.env` no longer exists, `common/*` C# libraries take config via DI, not their own env file)
- Post-steps: `pnpm install`, `dotnet build DotNetMonoRepoTemplate.sln`, `pnpm typecheck`

Wait for the user to confirm before proceeding.

## Step 2 — Rename namespace in all source files

Use Bash to find and replace in all relevant files in one pass. Exclude `node_modules/`, `.git/`, `dist/`, and `pnpm-lock.yaml`.

**Two substitutions, applied in this order:**

1. Replace `@node-mono-repo-template/` → `@{scope-without-@}/` (preserves the @ and /)
   - Actually: replace the literal string `node-mono-repo-template` with `{slug}` in a single pass — this handles both `@node-mono-repo-template/` → `@{slug}/` and plain `node-mono-repo-template` → `{slug}` simultaneously

```bash
find . \
  -type f \
  \( -name "*.ts" -o -name "*.tsx" -o -name "*.json" -o -name "*.md" \
     -o -name "*.yaml" -o -name "*.yml" -o -name "*.properties" \
     -o -name "*.mjs" -o -name "*.sh" -o -name "Dockerfile" \) \
  ! -path "*/node_modules/*" \
  ! -path "*/.git/*" \
  ! -path "*/dist/*" \
  ! -name "pnpm-lock.yaml" \
  -exec grep -l "node-mono-repo-template" {} \; | \
  xargs sed -i 's/node-mono-repo-template/{slug}/g'
```

If the org scope differs from the slug (e.g., `@zynkosi/burger-shop` vs just `@burger-shop`), run a second pass replacing `@{slug}/` → `@{org-prefix}/{slug}/`.

After running, verify a sample of replaced files to confirm correctness.

## Step 3 — Update CLAUDE.md project section

Replace the "Using this as a template" section at the bottom of CLAUDE.md with:

```markdown
## Project: {Display Name}

This is the {Display Name} project, initialized from node-mono-repo-template.
Package scope: `{scope}`
Initialized: {date}
```

## Step 4 — Generate secrets

Generate these values using Node.js crypto (run inline with Bash):

```bash
node -e "const c=require('crypto'); console.log('JWT_SECRET='+c.randomBytes(64).toString('hex')); console.log('JWT_REFRESH_SECRET='+c.randomBytes(64).toString('hex')); console.log('PEPPER='+c.randomBytes(32).toString('hex')); console.log('TWO_FACTOR_KEY='+c.randomBytes(32).toString('hex'));"
```

Store the generated values — use them in Step 5.

## Step 5 — Create .env files from examples

For each `.env.example` file, copy it to `.env` and apply these substitutions:

| Placeholder | Replace with |
|---|---|
| `node-mono-repo-template` (in DATABASE_URL) | `{slug}` |
| `REPLACE_WITH_64_BYTE_HEX_SECRET` | generated JWT_SECRET |
| `REPLACE_WITH_64_BYTE_HEX_SECRET` (second) | generated JWT_REFRESH_SECRET |
| `REPLACE_WITH_32_BYTE_HEX` | generated PEPPER |
| `REPLACE_WITH_32_BYTE_HEX_KEY_64_CHARS` | generated TWO_FACTOR_KEY |
| `your-secret-api-key-minimum-32-chars-here` | generated 32-byte hex |
| `your_mailtrap_api_key` | `dev_mailtrap_key_replace_me` |
| `noreply@yourdomain.com` | `noreply@{slug}.local` |
| `Your App Name` | `{Display Name}` |

Create .env files at:
- `apps/backend/api-gateway/.env`
- `apps/backend/admin-api/.env`
- `apps/backend/customer-api/.env`
- `apps/backend/schedule-api/.env`
- `apps/frontend/admin-web/.env`
- `apps/frontend/customer-web/.env`
- `apps/mobile/customer-mobile/.env`
- `devops/.env`

`common/*` no longer has its own `.env` — every backend `common/DotNetMonoRepoTemplate.*` C# library takes its configuration as constructor/DI parameters supplied by the consuming service's `<Service>Options`, it never reads environment variables directly (see `common-packages.md`).

## Step 6 — Install and build

```bash
pnpm install
dotnet build DotNetMonoRepoTemplate.sln
```

No `prisma:generate` equivalent needed — EF Core doesn't have a codegen step the way Prisma did.

## Step 7 — Verify

```bash
pnpm typecheck
dotnet build DotNetMonoRepoTemplate.sln
```

Fix any errors before marking complete — zero errors and zero warnings on the `dotnet build` side, since nullable warnings are errors.

## Step 8 — Report

Show the user:
- Files updated (count)
- .env files created (list)
- Secrets generated (names only, never values)
- Any typecheck errors that need attention
- Next steps: start dev with `pnpm dev`, open the local stack with `docker compose -f devops/docker-compose.dev.yml up -d`
