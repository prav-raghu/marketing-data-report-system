# customer-web (Phase 3 proof-of-concept)

Blazor **Web App** proof-of-concept for the Next.js → Blazor migration
(the approved plan's Part C, Phase 3). Unlike `admin-web` (Phase 1/2,
Blazor WebAssembly *Standalone*), this app uses **hybrid render modes** —
pages are static server-rendered by default, with individual components
opting into `InteractiveWebAssembly` — because customer-web needs to
preserve Next.js's real SSR/SEO behavior (server-rendered `<head>` content
that a crawler sees without executing JS), which plain WASM-standalone
cannot do.

Built side-by-side with the existing Next.js `customer-web` — nothing here
replaces it yet, and nothing is wired into `docker-compose.yaml` or
`pnpm-workspace.yaml`'s build graph (it is in the CI `docker-build` matrix,
so it gets a real `dotnet build`/`dotnet publish` pass on merge).

## Why two projects (`src/` and `src/Client/`)

`InteractiveWebAssembly` render mode requires the interactive components to
compile into a **separate WebAssembly-targeted project** — the WASM runtime
loads its own assembly bundle, distinct from the server's. This is a hard
technical requirement of Blazor Web App, not a style choice:

- `src/CustomerWeb.csproj` (`Microsoft.NET.Sdk.Web`) — the ASP.NET
  Core host. `Program.cs`, `App.razor`, `Routes.razor`, the layout, and both
  pages (`Home.razor`, `About.razor`) live here and are **static SSR** —
  server-rendered HTML, zero JS required to see the content or the
  `<head>` metadata.
- `src/Client/CustomerWeb.Client.csproj`
  (`Microsoft.NET.Sdk.BlazorWebAssembly`) — only the components that need
  real interactivity. Currently just `CounterCard.razor`, embedded into
  `Home.razor` via `<CounterCard @rendermode="InteractiveWebAssembly" />`.

## What this PoC proves — and what it found

The core bet Phase 3 needed to validate: **`<PageTitle>`/`<HeadContent>`
inside a static-SSR page render into the initial server response**, the
same way Next.js's per-route `Metadata` export does — this is what makes
Blazor Web App SEO-viable where plain WASM standalone isn't. `Home.razor`
and `About.razor` reproduce `customer-web`'s actual live `layout.tsx`/
`about/layout.tsx` metadata (title, description, OpenGraph, Twitter card,
robots) using this mechanism.

**A real architectural finding, not a guess**: in a Blazor Web App, static
SSR pages are genuine page navigations — going from `Home` to `About` is
not a client-side SPA route change the way `admin-web` (WASM
standalone) or the current React apps behave. A `InteractiveWebAssembly`
component's in-memory state does **not** survive across that navigation;
each page gets a fresh WASM instantiation for its own interactive islands.
The source React app's "counter persists across Home ↔ About routes" demo
has no simple equivalent here — reproducing it would need the WASM island
to bridge state through `sessionStorage`/JS interop, which is real,
legitimate extra work, not a two-line fix. This PoC does **not** attempt
that: `About.razor` has no counter reference at all, and `CounterCard` is
self-contained, local-only state. Don't assume this is solved — see
`.claude/rules/frontend-blazor.md` for the rule this became.

## What's ported vs. what's deliberately skipped

**Ported** (these are `customer-web`'s actual, live surface):
- Root + `/about` metadata (title, description, OG, Twitter, robots) —
  copied from `layout.tsx`/`about/layout.tsx` exactly
- `robots.txt`, `sitemap.xml`, `site.webmanifest` — static files, copied
  verbatim into `wwwroot/`
- One interactive WASM island (`CounterCard`), proving the hybrid-render
  mechanism

**Deliberately not ported** — confirmed **dead code** in the source app,
not oversights:
- `app/utils/structured-data.ts` (JSON-LD schema builders) — grepped the
  whole app, nothing imports or calls these. No page emits JSON-LD today.
  Don't build a Blazor equivalent for output that doesn't exist yet.
- `app/hooks/use-seo.ts` — same story, unused, grepped confirmed no callers.

**Deliberately deferred** (real gaps, but out of this PoC's scope — same
"prove the architecture first" reasoning Phase 1 used for admin-web):
- `ApiTestCard`/`TailwindShowcase` components (straightforward ports once
  the pattern is proven — not attempted here to keep this PoC's surface
  area small)
- Cookie consent — `frontend-nextjs.md` mandates it for customer-facing
  pages; the current React app doesn't have it built either
- A Sentry-equivalent — Next.js needed three separate runtime configs
  (client/server/edge); a single ASP.NET Core host likely simplifies this
  to one server-side + one WASM-side config, but that's unverified
- Binary assets (`favicon.ico`, `og-image.jpg`, `og-about.jpg`,
  `android-chrome-*.png`, `apple-touch-icon.png`) — referenced in the
  metadata/manifest exactly as the source does, but the actual image files
  need copying from `customer-web/public/` separately

## Running locally (once a real `dotnet build` pass has validated this scaffold)

```bash
pnpm --filter customer-web build:css
cd apps/frontend/customer-web/src
dotnet run
```

## Verification status

**Not yet verified** — same standing caveat as the rest of this migration
work: no `dotnet` SDK was available in the sandbox that wrote this
scaffold. This is a genuinely higher-risk, less-proven pattern than
`admin-web` (this session's first exercise of Blazor Web App's
two-project split, vs. WASM Standalone which was already exercised twice).
Before trusting it: `dotnet build` all three projects
(`CustomerWeb.csproj`, `CustomerWeb.Client.csproj`,
`CustomerWeb.Tests.csproj`), `dotnet test`,
`pnpm --filter customer-web build:css`, run it and confirm the
counter island actually hydrates and is interactive, and — the point of
this whole phase — `curl`/`view-source` both routes and confirm the
`<head>` metadata is present in the raw HTML **without executing JS**,
then compare against `customer-web`'s current SSR output and run a
Lighthouse SEO pass on both for a real before/after.
