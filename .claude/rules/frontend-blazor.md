---
paths:
  - "apps/frontend/admin-web/**/*.razor"
  - "apps/frontend/admin-web/**/*.cs"
  - "apps/frontend/customer-web/**/*.razor"
  - "apps/frontend/customer-web/**/*.cs"
---

# Frontend Blazor Rules

Two distinct patterns exist under this rule, not one — check which app
you're in before applying a convention from the wrong section:

- `admin-web` — **Blazor WebAssembly Standalone**. No SSR/SEO need.
  Everything runs client-side after one static `wwwroot` payload loads,
  same deployment shape as the React SPA it replaces (nginx serving static
  files). Rules below marked **(Standalone)** apply here.
- `customer-web` — **Blazor Web App**, hybrid render modes. Needs
  real SSR for SEO, so pages are static-server-rendered by default with
  individual components opting into `InteractiveWebAssembly`. This is a
  hosted ASP.NET Core process (like the Node SSR server it replaces), not
  static files. Rules marked **(Web App)** apply here.

You are working on a Blazor frontend application — the React/Next.js → Blazor
migration tracked in the approved security-audit-and-migration plan. These
rules apply to `apps/frontend/admin-web`, `apps/frontend/customer-web`,
and any later Blazor app that follows one of these two patterns. They were
settled by working proof-of-concepts, not written speculatively — if a
later phase finds one of these wrong, fix the rule and the code together,
don't silently diverge.

## Stack

- `admin-web`: Blazor WebAssembly **Standalone** + Tailwind v4 (replaces `admin-web`, no SSR/SEO need)
- `customer-web`: Blazor **Web App** with hybrid render modes (static SSR + `InteractiveWebAssembly`), not standalone WASM — see the migration plan's Part C1 for why plain WASM can't replace Next.js's real SSR/SEO output, and the "Blazor Web App" section below for what that means in practice

## Project layout (Standalone — `admin-web`)

```
apps/frontend/<app>/
├── package.json          # Tailwind CLI only — no framework deps, this is not an npm package
├── README.md
└── src/
    ├── <App>.csproj      # Sdk="Microsoft.NET.Sdk.BlazorWebAssembly"
    ├── Program.cs
    ├── App.razor         # <Router>/<AuthorizeRouteView> lives here — standalone WASM, not the newer Routes.razor split
    ├── _Imports.razor
    ├── RedirectToLogin.razor
    ├── wwwroot/
    │   ├── index.html
    │   ├── appsettings.json             # placeholder values, "REPLACE_WITH_..." convention
    │   ├── appsettings.Development.json # real localhost dev defaults
    │   └── css/
    │       ├── input.css   # source — committed
    │       └── app.css     # Tailwind CLI output — gitignored, never commit
    ├── Layout/
    ├── Pages/
    ├── Auth/
    ├── Services/
    ├── Models/
    └── Validators/
tests/
├── <App>.Tests.csproj    # Sdk="Microsoft.NET.Sdk.Razor", references bunit
└── Pages/
```

`apps/frontend/<app>` still matches the `apps/frontend/*` pnpm-workspace glob
— give every Blazor app its own minimal `package.json` (Tailwind CLI devDeps
only, no `dependencies`) so the workspace resolves it like any other member.
It is not a pnpm-managed app; `pnpm --filter <app> build:css` is the one
command that touches it.

## Blazor Web App (`customer-web`) — project layout and differences from Standalone

```
apps/frontend/customer-web/
├── package.json
├── README.md
└── src/
    ├── CustomerWeb.csproj        # Sdk="Microsoft.NET.Sdk.Web" — the ASP.NET Core host
    ├── Program.cs                       # AddRazorComponents().AddInteractiveWebAssemblyComponents()
    ├── App.razor                        # full HTML doc, <HeadOutlet/>, <Routes/> with no @rendermode
    ├── Routes.razor                     # <Router>, AdditionalAssemblies pointing at the Client assembly
    ├── _Imports.razor
    ├── Layout/
    ├── Pages/                           # static-SSR pages — @page, PageTitle/HeadContent for per-route metadata
    ├── wwwroot/
    │   ├── robots.txt, sitemap.xml, site.webmanifest   # static files, served as-is
    │   └── css/{input.css, app.css}
    └── Client/
        ├── CustomerWeb.Client.csproj   # Sdk="Microsoft.NET.Sdk.BlazorWebAssembly", OutputType Exe
        ├── Program.cs                         # just WebAssemblyHostBuilder.CreateDefault(args).Build().RunAsync() — no RootComponents.Add, those are registered server-side
        ├── _Imports.razor
        └── Pages/                             # ONLY components that need @rendermode="InteractiveWebAssembly" live here
tests/
└── CustomerWeb.Tests.csproj      # references src/, not Client/ — test the static-SSR pages directly with bUnit
```

**Why two projects**: `InteractiveWebAssembly` render mode requires the interactive component to compile into a separate WASM-targeted assembly — the browser loads that assembly's own bundle, distinct from the server's. This is a hard technical requirement, not a style choice; a component can't have `@rendermode="InteractiveWebAssembly"` from inside the server project.

**SSR metadata, the actual mechanism**: `<PageTitle>title</PageTitle>` and `<HeadContent>...meta tags...</HeadContent>` inside a page component render into the initial server response via `<HeadOutlet/>` in `App.razor` — this works during pure static SSR, no WASM required, which is exactly why Blazor Web App is SEO-viable where Standalone WASM isn't. This is the direct equivalent of Next.js's per-route `Metadata` export; reproduce a page's real `layout.tsx`/`page.tsx` metadata (title, description, OpenGraph, Twitter, robots) exactly, don't invent new copy.

**Referencing the Client assembly from the server project without `_Imports.razor`**: `MapRazorComponents<App>().AddAdditionalAssemblies(...)` and `Routes.razor`'s `<Router AdditionalAssemblies="...">` both need a `typeof(SomeType).Assembly` reference into the Client project. Use `typeof(SomeRealComponent).Assembly` (e.g. `typeof(CounterCard).Assembly`) — **do not** use `typeof(_Imports).Assembly`; whether `_Imports.razor` compiles to an addressable type is genuinely unclear and not worth staking a build on when any real component in that project is an unambiguous, guaranteed-correct reference.

**A real finding from Phase 3, not a guess — state does not survive page navigation the way it does in Standalone WASM or the React apps**: static-SSR pages are genuine page navigations. An `InteractiveWebAssembly` component's in-memory state does not carry from one page to the next — each page's islands get a fresh WASM instantiation. Don't build a feature (or port a React demo) that assumes client state persists across routes in this app the way it does in `admin-web`'s scoped DI services. If cross-page client state is genuinely needed, it has to go through `sessionStorage`/JS interop or a server-side mechanism — treat that as its own real piece of work, not a two-line fix.

**Middleware**: same `UseForwardedHeaders()`/`UseHttpsRedirection()` pattern as the backend services (see `backend.md`) — this is a real ASP.NET Core host behind Coolify's proxy, not static files behind nginx, so the same reasoning applies: `UseForwardedHeaders` first, before anything reads client IP/scheme.

## Non-negotiable (Standalone — `admin-web`)

- Blazor WebAssembly Standalone for any app with no SEO/SSR requirement (internal/admin tools); Blazor Web App with hybrid render modes for anything customer-facing/SEO-critical — same split `frontend-react.md`/`frontend-nextjs.md` draw for React vs. Next.js, now drawn between WASM-standalone and Web-App-hybrid instead
- FluentValidation for form validation, called manually on submit (`await validator.ValidateAsync(model)`) — mirrors the backend's own `Endpoints/*.cs` pattern exactly rather than using `<DataAnnotationsValidator />`. Never Data Annotations, never a client-only validation library
- Request/form-bound models are mutable classes with `{ get; set; }`, never `sealed record` with `init` — `@bind` requires a settable property. Response-only models (deserialized, never bound to a form) stay `sealed record`, matching the backend rule as closely as C#/Blazor's constraints allow
- No third-party state-management library (no Fluxor). Plain DI-registered C# classes for anything that needs to survive across components (`AuthTokenStore`), plus Blazor's built-in `AuthenticationStateProvider`/`<AuthorizeView>`/`@attribute [Authorize]`/`<AuthorizeRouteView>` for anything auth-gated — **do not** hand-roll a `ProtectedRoute`-style wrapper component, Blazor's own mechanism replaces it entirely
- **`AuthorizeRouteView` does not gate a page by default** — a routed component with no `[Authorize]` attribute is public. Every page that must require authentication needs an explicit `@attribute [Authorize]`; every genuinely public page (login, the MFA challenge screen) should still carry `@attribute [AllowAnonymous]` even though it's the default, so the intent is explicit and survives a future `FallbackPolicy` change
- Tokens stay in memory only — a DI-registered store (`AuthTokenStore`), never `localStorage`/`sessionStorage`/cookies set from Blazor code. Same rule as the React/Next.js apps, unchanged by the framework switch
- `HttpClient` via a named client (`AddHttpClient("<Name>", ...)`) + a `DelegatingHandler` for Bearer-token injection and 401 handling, `Microsoft.Extensions.Http.Polly` (`HttpPolicyExtensions.HandleTransientHttpError()` plus an explicit 429 check) for retry — 3 attempts, exponential backoff. This is the direct Blazor equivalent of `api-client.ts`'s Axios interceptor pattern; keep the retry conditions (408/429/5xx) and attempt count in sync with it until the React app is retired
- **A plain `<form @onsubmit="Handler">` does not auto-prevent the browser's native submit** — always pair it with `@onsubmit:preventDefault="true"`, or use `<EditForm OnValidSubmit="...">` instead (which handles this internally). Forgetting this causes a page reload/navigation on submit in a real browser, invisible in bUnit since bUnit never exercises browser navigation
- Blazor WASM has no Vite/Next-style build-time env var injection — configuration goes through `wwwroot/appsettings.json` + `appsettings.{Environment}.json`, loaded automatically by `WebAssemblyHostBuilder`. `appsettings.json` (the file shipped in the production build) gets a `REPLACE_WITH_...` placeholder, same spirit as this repo's `.env.example` convention; `appsettings.Development.json` gets a real `http://localhost:<port>` default matching the target backend service's dev port
- Tailwind v4, same CSS-variable theme tokens as the React apps' `index.css`/`globals.css` (copy them verbatim, don't reinvent a parallel palette) — built via `@tailwindcss/cli` as a `pnpm` step alongside `dotnet build`, not `postcss.config.js` (no PostCSS pipeline needed for the CLI-only setup). Add an explicit `@source "<path>/**/*.razor";` directive in the input CSS if `.razor` files sit outside the CLI's default auto-detected content root — they usually do, since `input.css` lives under `wwwroot/css/` and the components don't
- bUnit for component tests, xUnit conventions (`MethodName_ExpectedOutcome_WhenCondition` doesn't map 1:1 to UI components — name bUnit tests for the user-visible behavior instead, e.g. `Submit_ShowsInlineErrors_WhenFormIsEmpty`)
- No comments in code — same rule as the React/Next.js apps

## Open / deferred (do not assume solved)

- **Shared wire DTOs**: request/response shapes are still hand-mirrored per app (`Models/AuthDtos.cs` duplicating the backend's `Dtos/AuthDtos.cs`) — promoting them into `common/DotNetMonoRepoTemplate.Types` so a Blazor app can `<ProjectReference>` the real DTOs instead of hand-copying is a real simplification once more than one Blazor app exists, but it's a backend architecture change (moving DTOs out of `AdminApi`/`CustomerApi` themselves) and needs its own review — don't do it silently as part of an unrelated frontend change
- **Toast notifications**: no Blazor equivalent to `sonner` exists yet. Until one is built, mirror whatever the equivalent React page actually does today (which is not always what `frontend.md` documents — e.g. `admin-web`'s `Login.tsx` already shows server errors inline, not via toast, despite the documented rule) rather than inventing a new pattern the React app doesn't have either
- **`customer-web` component parity**: only `CounterCard` has been ported as a WASM island so far — `ApiTestCard`/`TailwindShowcase` are straightforward but not yet done. Cookie consent (`frontend-nextjs.md` mandates it, the React app doesn't have it built either) and a Sentry-equivalent for the single-host model are real gaps, not yet even scoped in detail
- **Cross-page client state in `customer-web`**: unsolved — see the "real finding" note above. Don't assume a `CounterState`-style scoped service (the `admin-web` pattern) carries over here; it doesn't, because pages are real navigations
