---
paths:
  - "apps/mobile/customer-mobile/**/*.razor"
  - "apps/mobile/customer-mobile/**/*.cs"
  - "apps/mobile/customer-mobile/**/*.xaml"
---

# Mobile Rules

You are working on `apps/mobile/customer-mobile` — .NET MAUI Blazor Hybrid
(C#), the Phase 5 replacement for the Ionic + Capacitor React app that used
to live at this path. This is genuinely the **least-verified part of the
whole migration** — no MAUI workload, no Android/iOS toolchain, no macOS
host were available to the session that wrote it, and none of it has been
compiler-verified end-to-end. Read `apps/mobile/customer-mobile/README.md`
in full before trusting or extending anything under `src/`; treat every file
under `src/Platforms/` as the least-trustworthy code in this whole repo
until a real build on real tooling proves otherwise.

None of the old Ionic/Capacitor/React conventions apply here anymore — no
`@capacitor/preferences`, no `IonPage`/`IonHeader`, no Vite/`VITE_<SCOPE>_*`
env vars, no React Query. This is C#/Razor now.

## What's actually here

A minimal shell: `MauiProgram.cs` (DI setup — currently just
`AddMauiBlazorWebView()` plus dev-only Blazor WebView developer tools and
debug logging, nothing else registered yet), `App.xaml(.cs)`,
`MainPage.xaml(.cs)` hosting a `BlazorWebView` that mounts a single
`Home.razor` component directly — no `Router`/`NavigationManager` yet.
Platform folders (`Platforms/Android`, `Platforms/iOS`,
`Platforms/MacCatalyst`) carry the standard MAUI template boilerplate
(`MainActivity`/`MainApplication`/`AppDelegate`/`Program.cs`) each platform
needs to build at all — none of it has been exercised on real tooling.

Root namespace is `CustomerMobile` — every `.cs`/`.razor`/`.xaml` file uses
it (`namespace CustomerMobile;` / `x:Class="CustomerMobile.App"` /
`xmlns:local="clr-namespace:CustomerMobile"`), matching the `.csproj`
(`src/CustomerMobile.csproj`).

## Conventions — apply by analogy from `.claude/rules/frontend-blazor.md` where they translate

This app shares the C#/Blazor stack with `admin-web`/`customer-web`, so the
same non-negotiables apply where they make sense for a MAUI Blazor Hybrid
shell rather than a browser-hosted app:

- Nullable reference types, no comments in code, `sealed record` for
  response DTOs (mutable classes only where two-way `@bind` genuinely needs
  a settable property) — same rules as the backend and the two web apps,
  since this is the same language and the same `Directory.Build.props`.
- FluentValidation for any form validation once real forms exist here —
  never Data Annotations. Nothing in the current shell has a form yet, so
  there's no established call-site pattern to copy; when one is needed,
  follow `frontend-blazor.md`'s "called manually on submit" pattern.
- DI-registered services via `builder.Services` in `MauiProgram.cs` for
  anything that needs to survive across components — no third-party state
  library, same reasoning as the web apps.
- For anything native-plugin-shaped (camera, geolocation, haptics, secure
  storage), reach for MAUI's own APIs (`Microsoft.Maui.Devices`,
  `Microsoft.Maui.ApplicationModel`, `Microsoft.Maui.Storage`) — not
  Capacitor, which no longer exists in this repo.
- bUnit is the pattern used for `admin-web`/`customer-web` component tests,
  but this app has **no test project yet** — MAUI Blazor Hybrid's bUnit
  setup differs from the WASM Standalone/Web App setups those use, and
  hasn't been scaffolded or verified here. Don't assume one exists.

## Real, open gaps — don't silently paper over these

- **Not in `DotNetMonoRepoTemplate.sln`.** Every other project in that
  solution builds with the base .NET SDK alone; this one needs the `maui`
  workload (`dotnet workload install maui`), which isn't part of the base
  SDK. Adding it to the shared `.sln` would break `dotnet build
  DotNetMonoRepoTemplate.sln` for anyone without that workload — including
  CI, which currently builds the whole solution unconditionally. Build this
  project directly (`dotnet build src/CustomerMobile.csproj -f
  net10.0-android`, etc.) once the workload is installed, not through the
  shared solution file.
- **No Dockerfile, no CI wiring.** MAUI apps ship through app stores, not
  Coolify — a real CI path needs a macOS runner (iOS/MacCatalyst) and
  Android SDK/workload setup, which is separate infrastructure work, not
  something to bolt onto the existing web-app pipeline.
- **App icon/splash are placeholder SVGs** and **`ApplicationId` is a
  placeholder** (`com.nodemonorepotemplate.customermobile`) — replace both
  as part of this template's own project-name-substitution step
  (`CLAUDE.md`'s "Using this as a template" section).

## Before trusting any of this

In order of how much confidence each step actually buys:

1. Install the MAUI workload (`dotnet workload install maui`) somewhere
   with the base .NET 10 SDK and try `dotnet build src/CustomerMobile.csproj
   -f net10.0-android` first — Android has the lowest platform-tooling bar
   (no macOS/Xcode requirement).
2. Only after that succeeds, attempt `-f net10.0-maccatalyst`/`-f
   net10.0-ios` on a macOS host with Xcode installed.
3. Run it on an actual emulator/simulator or device and confirm the
   `BlazorWebView` actually renders `Home.razor` — this is the one thing
   that would validate the whole `MainPage.xaml`/`RootComponent`/
   `HostPage` wiring end-to-end, and nothing short of running it can.

## Before marking complete

Run `dotnet build apps/mobile/customer-mobile/src/CustomerMobile.csproj -f
<target>` for whichever platform target you can actually build against —
zero errors required, same bar as every other C# project in this repo. If
no MAUI workload/platform tooling is available in your environment, say so
explicitly rather than marking the task done unverified.
