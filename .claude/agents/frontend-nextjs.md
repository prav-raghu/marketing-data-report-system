---
name: frontend-nextjs
description: "Retired — customer-web (Next.js) was decommissioned in Phase 4 of the Blazor migration. Do not select this agent for new work; use the Blazor Web App conventions in .claude/rules/frontend-blazor.md instead."
tools: Read, Grep, Glob
model: inherit
---

# frontend-nextjs (retired)

`customer-web` was Next.js (App Router, SSR/SEO) — fully decommissioned in
Phase 4 of the React/Next.js → Blazor migration, replaced by
`apps/frontend/customer-web` (Blazor **Web App** with hybrid render
modes — not a plain WASM standalone app, precisely because this app needs
real SSR/SEO the way `customer-web` had). Everything this agent used to
cover — App Router structure, the `Metadata` export, cookie consent, Zod
validation, Axios client setup, React Query — no longer applies in its
original form; the Blazor equivalents are documented in
`.claude/rules/frontend-blazor.md`'s Web App section
(`<PageTitle>`/`<HeadContent>` replaces `Metadata`, FluentValidation
replaces Zod, `HttpClient`+Polly replaces the Axios interceptor pattern).

For customer-web work now, see `.claude/rules/frontend-blazor.md` and
`apps/frontend/customer-web/README.md` — the README also documents
a real architectural finding from building the Phase 3 proof-of-concept:
static-SSR pages in a Blazor Web App are genuine page navigations, so
client-side WASM component state does not persist across routes the way
it did in the React/Next.js app or does in `admin-web`'s WASM
Standalone model.

This file is kept only so the `frontend-nextjs` agent name doesn't dangle
if something still references it by name — it is not a working guide for
anything currently in this repo.
