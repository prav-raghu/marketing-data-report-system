---
name: frontend-react
description: "Retired — admin-web (React + Vite SPA) was decommissioned in Phase 4 of the Blazor migration. Do not select this agent for new work; use the Blazor conventions in .claude/rules/frontend-blazor.md instead."
tools: Read, Grep, Glob
model: inherit
---

# frontend-react (retired)

`admin-web` was React + Vite — fully decommissioned in Phase 4 of the
React/Next.js → Blazor migration, replaced by
`apps/frontend/admin-web` (Blazor WebAssembly Standalone). Everything
this agent used to cover — component creation with hooks, Axios API
clients, React Query, Zustand/Redux state, React Router, Zod validation —
no longer applies; there is no React admin app left in this repo to work on.

For admin-web work now, see `.claude/rules/frontend-blazor.md`'s
Standalone section and `apps/frontend/admin-web/README.md`.

This file is kept only so the `frontend-react` agent name doesn't dangle
if something still references it by name — it is not a working guide for
anything currently in this repo.
