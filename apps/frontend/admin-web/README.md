# admin-web (Phase 1 proof-of-concept)

Blazor WebAssembly Standalone proof-of-concept for the React → Blazor migration
described in the approved plan (`documentation/dotnet-migration-plan.md`'s
sibling migration doc, or ask the session that built this for the plan file).
This app is built **side-by-side** with the existing `apps/frontend/admin-web`
(React + Vite) — it does not replace it yet, and nothing here is wired into
`docker-compose.yaml`, CI, or `pnpm-workspace.yaml`'s build graph.

## Scope (Phase 1 only)

Login → protected routing → Home, including the two-step MFA challenge flow
`admin-web`'s own rules document but that hasn't been built in the React app
yet. Nothing beyond that — no `/about`, `/users`, `/settings` pages, no
MFA *enrollment* screen (only the post-login *challenge* screen).

## Architecture decisions this PoC settles (see the plan's Part C2)

| Concern | Decision | Where |
|---|---|---|
| Validation | FluentValidation, called manually on submit — mirrors the backend's `await validator.ValidateAsync(body)` pattern exactly, one rule set that could eventually be shared via a `<ProjectReference>` instead of hand-duplicated | `Validators/LoginRequestValidator.cs` |
| State | Plain DI-registered C# classes (`AuthTokenStore`), not a state-management library — Blazor's own `AuthenticationStateProvider`/`<AuthorizeView>`/`<AuthorizeRouteView>` replace React Router's `ProtectedRoute` wrapper entirely; no custom guard component needed | `Auth/` |
| API client | `HttpClient` (named `"AdminApi"`) + a `DelegatingHandler` for Bearer injection and 401 handling, `Microsoft.Extensions.Http.Polly` for the retry policy (408/429/5xx, exponential backoff — same shape as `admin-web`'s `api-client.ts`) | `Auth/AuthorizationMessageHandler.cs`, `Program.cs` |
| Token storage | In-memory only, same as today — a DI singleton field, never written to any browser storage | `Auth/AuthTokenStore.cs` |
| Styling | Tailwind v4, same theme tokens/CSS variables as `admin-web/src/index.css`, built via the Tailwind CLI (`@tailwindcss/cli`) as a `pnpm` step alongside `dotnet build` — this app keeps a small `package.json` for exactly that, nothing else | `package.json`, `src/wwwroot/css/input.css` |
| Config | Blazor WASM has no build-time env var injection like Vite/Next — `wwwroot/appsettings.json` + `appsettings.Development.json`, loaded automatically by `WebAssemblyHostBuilder`, replace the `VITE_ADMIN_*` convention | `src/wwwroot/appsettings*.json` |
| Testing | bUnit, mirroring xUnit conventions already used on the backend | `tests/` |

## Not yet decided / explicitly deferred to Phase 2+

- **Shared DTOs**: `Models/AuthDtos.cs` hand-mirrors `AdminApi.Dtos.AuthDtos` — the
  same duplication the current TypeScript apps have (`types/auth.ts`). The plan
  flags promoting shared wire DTOs into `common/DotNetMonoRepoTemplate.Types`
  as a real simplification once more of the app is built — not done here
  because it's a backend architecture change (moving `AuthDtos.cs` out of
  `AdminApi` itself), out of scope for a frontend PoC.
- **Toast notifications**: `sonner` has no Blazor equivalent yet. This PoC
  mirrors `Login.tsx`'s *actual* behavior (an inline banner for server
  errors) rather than `frontend.md`'s documented-but-not-yet-followed
  toast rule — a real toast component is Phase 2 work.
- **DTO mutability**: request DTOs here are mutable classes (`LoginRequest`,
  `VerifyLoginMfaRequest`) because Blazor's `@bind` needs settable
  properties — this is a deliberate, necessary deviation from the backend's
  `sealed record`-only rule, not an oversight.

## Running locally (once a real `dotnet build` pass has validated this scaffold)

```bash
pnpm --filter admin-web build:css   # builds src/wwwroot/css/app.css
cd apps/frontend/admin-web/src
dotnet run                                 # serves against http://localhost:4001 (admin-api dev port)
```

## Verification status

**Not yet verified** — no `dotnet` SDK was available in the sandbox that wrote
this scaffold. Before trusting any of it: run `dotnet build` on both
`AdminWeb.csproj` and `AdminWeb.Tests.csproj`, run
`dotnet test`, run `pnpm --filter admin-web build:css` and confirm the
Tailwind classes actually resolve, and manually run it against a live
`admin-api` to confirm the full login → (optional MFA) → protected-Home →
logout round trip.
