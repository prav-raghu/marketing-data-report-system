---
applyTo: "**/*.ts,**/*.tsx"
description: "TypeScript build validation — must pass before any task is considered complete"
---

# TypeScript Build Validation

Every TypeScript project in this monorepo must pass a strict build before any task is considered complete. This is non-negotiable. Scope: `apps/frontend/*` and `apps/mobile/*` — the backend moved to C#/.NET and has its own build-validation rule (`dotnet build`, zero warnings — see `rules/backend.md` and the `csharp-standards` agent), so nothing here applies to `apps/backend/*`/`common/*` anymore.

## Required checks before marking any task as done

Run the following from the monorepo root before completing any task that touches TypeScript files:

```bash
pnpm --filter <app-name> typecheck
```

Or for a full monorepo check:

```bash
pnpm typecheck
```

If either command produces errors, the task is NOT complete. Fix all errors before finishing.

## Rules

- Never assume `vite dev` or `ts-node` passing means the code is type-safe. They do not run full type checking.
- Never use `any` unless explicitly instructed by the user.
- Never cast with `as` to silence a type error — fix the underlying type.
- Never add `// @ts-ignore` or `// @ts-expect-error` unless explicitly instructed by the user.
- All shared types (`IconType`, query types, etc.) must be defined correctly at the source. Do not patch call sites to work around a broken type definition.
- When introducing a new shared type or utility function, verify all existing usages still compile after the change.
- When adding a new component prop, verify all existing usages of that component still compile.
- `typecheck` must pass with zero errors before any task is marked complete.

## Icon types

Icons in this codebase use a custom `IconProps` type. `IconType` must be defined as:

```typescript
type IconType = (props: IconProps) => JSX.Element;
```

Never type `IconType` as `FunctionComponent<SVGProps<SVGSVGElement>>` — this causes `stroke` type conflicts across all icon usages.

## Query function generics

Utility functions that accept query objects must use a generic constraint, not `Record<string, unknown>`:

```typescript
function buildQuery<T extends object>(query?: T): string
```

Never type query parameters as `Record<string, unknown>` — this breaks all typed query interfaces.

## Badge and UI component variants

Only use variant values that are explicitly declared in the component's prop types. Never pass a variant string that is not in the union — fix the component or the call site, do not cast.
