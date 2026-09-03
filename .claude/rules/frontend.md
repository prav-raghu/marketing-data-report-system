---
paths:
  - "apps/frontend/**/*.ts"
  - "apps/frontend/**/*.tsx"
---

# Frontend Rules (retired — React/Next.js apps decommissioned)

`admin-web` (React + Vite) and `customer-web` (Next.js) were fully retired
in Phase 4 of the React → Blazor migration
(`documentation/dotnet-migration-plan.md`'s sibling migration plan) —
replaced by `apps/frontend/admin-web` (Blazor WebAssembly Standalone)
and `apps/frontend/customer-web` (Blazor Web App, hybrid render
modes). Everything this file used to cover — the `VITE_`/`NEXT_PUBLIC_`
env var convention, react-hook-form + Zod validation, Zustand state,
`sonner` toasts, React Query — no longer applies: there is no TypeScript
frontend left under `apps/frontend/` to configure (this file's own path
glob above, `apps/frontend/**/*.ts(x)`, now matches nothing).

For frontend work now, see `.claude/rules/frontend-blazor.md` — it covers
both Blazor patterns (WebAssembly Standalone for `admin-web`, Web
App hybrid render modes for `customer-web`), the FluentValidation/
DI-state/HttpClient+Polly conventions that replaced react-hook-form+Zod/
Zustand/Axios-interceptors, and the real architectural findings from
building both proof-of-concepts (state doesn't persist across page
navigations in a Blazor Web App the way it does in a WASM-standalone SPA;
`AuthorizeRouteView` doesn't gate a page by default; etc).

`apps/mobile/customer-mobile` is now .NET MAUI Blazor Hybrid (C#), replacing
the Ionic + Capacitor React app that used to live at that path in Phase 5 of
the migration — see `.claude/rules/mobile.md`.

This file is kept only so a stale reference to `frontend.md` doesn't
dangle — it is not a working guide for anything currently in this repo.
