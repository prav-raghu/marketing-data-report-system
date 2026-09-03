---
name: typescript-standards
description: "Retired — customer-mobile (Ionic + Capacitor React) was replaced by a .NET MAUI Blazor Hybrid app in Phase 5 of the migration. There is no TypeScript app left anywhere in this repo. Do not select this agent for new work; use csharp-standards instead."
tools: Read, Edit, Grep, Glob
model: inherit
---

# typescript-standards (retired)

`customer-mobile` was this repo's one remaining TypeScript app (Ionic +
Capacitor, React) — `admin-web`/`customer-web` had already moved to Blazor
in Phase 4. In Phase 5, `customer-mobile` was itself replaced by a .NET
MAUI Blazor Hybrid app at the same path (`apps/mobile/customer-mobile`),
removing the last TypeScript app from the repo. Everything this agent used
to cover — `any`/`unknown` discipline, access modifiers, naming
conventions, wire-shape interfaces, `tsc --noEmit` as the completion
gate — no longer applies; there is no TypeScript to review anywhere in this
monorepo.

For type-safety review on `apps/mobile/customer-mobile` now (or
`apps/frontend/*`, or the backend), use `csharp-standards` instead — same
nullable-reference-types/no-`dynamic`/no-unjustified-`object` discipline,
applied to C#.

This file is kept only so the `typescript-standards` agent name doesn't
dangle if something still references it by name — it is not a working
guide for anything currently in this repo.
