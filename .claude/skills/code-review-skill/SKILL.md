---
description: Full code review — type safety, naming, security, project standards, and form validation coverage. Use after significant changes before considering a task done.
disable-model-invocation: true
argument-hint: <scope, e.g. "apps/backend/customer-api/src/routes" or "recent changes">
---

# Code Review: $ARGUMENTS

## Files to review

!`git diff --name-only HEAD~1 2>/dev/null | grep -E '\.(ts|tsx)$' || find $ARGUMENTS -name "*.ts" -o -name "*.tsx" 2>/dev/null | head -30`

## Blockers (must fix before merge)

- `any` type used anywhere
- Hardcoded secret, API key, token, or connection string
- Auth bypass via query param, header, or env flag
- Empty `catch` block
- Unused variables or imports
- Direct Axios call inside a component or `useEffect`
- `localStorage` used for a token
- `alert()` or `confirm()` in frontend code
- Native HTML form validation attributes (`required`, `pattern`, `type="email"` relied on for validation)
- Zod used on the backend
- `as` cast used to silence a type error
- `@ts-ignore` or `@ts-expect-error`
- Frontend form showing server 400/409 error as an inline field error instead of toast
- Frontend form showing Zod client error as a toast instead of inline field error
- AJV schema missing `additionalProperties: false` on a request body
- AJV schema missing `minLength: 1` on a required string field
- AJV max lengths not matching `@db.VarChar(N)` in the Prisma model
- `@unique` field returning 400 on duplicate instead of 409
- TypeScript errors present in the package

## Warnings (should fix)

- Class method missing explicit access modifier
- Constructor dependency not `private readonly`
- DTO defined as a class instead of an interface
- Async function missing error handling
- `unknown` narrowed without a type guard
- Hardcoded API URL or port number
- Missing loading, error, or empty state on a frontend data page
- Backend service missing `/health` or `/ready` endpoint
- List query missing Prisma `select`

## Naming check

| Element | Expected convention |
|---|---|
| Variables / functions / methods | `camelCase` |
| Classes | `PascalCase` |
| Interfaces | `PascalCase`, no `I` prefix |
| Route / schema / DTO files | `kebab-case` |
| DB tables / columns | `snake_case` |
| Frontend components | `PascalCase` |
| Frontend hooks | `use` prefix + `camelCase` |
| Frontend constants | `UPPER_SNAKE_CASE` |

## Security spot-check

- JWT validation in `api-gateway` only
- Rate limiting on all non-health routes
- Helmet registered on every Fastify service
- AJV validates before controller logic
- No PII in logs

## TypeCheck command

```bash
pnpm --filter <package-name> typecheck
```

A review is not complete if type errors are present.

## Output format

**Blockers:** [list with file + line]
**Warnings:** [list with file + line]
**Suggestions:** [list]
**Verdict:** Pass / Needs fixes
