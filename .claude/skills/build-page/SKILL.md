---
description: Build a complete frontend page end-to-end — design system lookup, component scaffold, React Query hooks, Zod validation, loading/error/empty states, and route registration. Use for "build me a page for X" or "add the Y management screen".
argument-hint: <page name and app, e.g. "product list page in admin-web" or "checkout page in customer-web">
---

# Build Page: $ARGUMENTS

## Step 1 — Design system lookup

Before writing a line of code, search for design guidance:

```bash
python3 ~/.claude/skills/ui-ux-pro-max/scripts/search.py "$ARGUMENTS" --design-system --stack react
```

If the global ui-ux-pro-max skill is not installed, use the principles from `.claude/skills/ui-ux-pro-max/SKILL.md` directly.

## Step 2 — Identify the target app

- admin-web (React + Vite) → pages in `src/pages/`, components in `src/components/{feature}/`, hooks in `src/hooks/`
- customer-web (Next.js) → routes in `app/{route}/page.tsx`, `loading.tsx`, `error.tsx`
- customer-mobile (.NET MAUI Blazor Hybrid, C#) → this app is a minimal shell (see `.claude/rules/mobile.md`) with no established page-scaffolding pattern yet; this skill's Zod/React conventions below don't apply — follow `.claude/rules/frontend-blazor.md`'s Razor/FluentValidation conventions instead where they translate

## Step 3 — Read the Prisma model

Find the Prisma model that backs this domain. Before writing the Zod schema, map every field constraint using the table in `validation-chain.instructions.md`:
- Required fields → `z.string().min(1, 'Required')`
- `@db.VarChar(N)` → `.max(N, 'Too long')`
- Email fields → `.email('Invalid email address')`
- Phone fields → `.regex(/^(\+27|0)[6-8][0-9]{8}$/, 'Invalid phone number')`
- Enums → `z.enum([...values])`
- `@unique` fields → handled by server 409, not client-side Zod

The Zod schema must mirror the AJV schema on the backend exactly.

## Step 4 — Generate in this order

1. **API service function** (`services/{domain}Service.ts`) — typed request + response interfaces
2. **React Query hooks** (`hooks/use{Domain}.ts`) — `useQuery` for reads, `useMutation` for writes with `onSuccess` invalidation
3. **Zod validation schema** (`utils/{domain}Validation.ts`) — required fields, email, phone, UUID, etc.
4. **Page component** — data table or content layout
5. **Form component** — Zod + react-hook-form, loading state on submit, per-field error display
6. **Route registration** — `App.tsx` (admin-web) or file-based (Next.js)

## Non-negotiable for every page

- Loading skeleton while data fetches
- Error state with a retry button
- Empty state with a clear call-to-action
- **Client Zod failure → inline error below the field, no toast**
- **Server 400/409/500 → toast notification, no inline error**
- All forms: required validation, email format, phone format, max lengths matching `@db.VarChar(N)` from Prisma
- No `alert()`, `confirm()`, or native HTML form validation attributes
- Every list page: search input + filter controls + pagination
- Tailwind utility classes only — no inline styles, no CSS modules
- No `any` type anywhere

## Step 5 — Typecheck

```bash
pnpm --filter <app-name> typecheck
```

Zero errors before marking complete.
