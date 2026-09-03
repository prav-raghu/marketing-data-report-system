# customer-mobile (.NET MAUI Blazor Hybrid)

.NET MAUI Blazor Hybrid app at `apps/mobile/customer-mobile`, replacing the
Ionic + Capacitor React app that used to live at this same path — that app
has been removed. This is genuinely the **least-verified part of the
entire migration**: it was built without a working MAUI workload, without
an Android/iOS toolchain, and without a macOS host. Read this whole file
before trusting anything under `src/`.

## Why this is riskier than Phases 1–4

Everything through Phase 4 (the two `apps/frontend/*` Blazor web apps) is
pure ASP.NET Core / Blazor — a stack this session could reason about
carefully, verify NuGet package names and versions against the live
NuGet API, and self-review for real logic bugs (and did — Phases 1 and 3
each caught genuine bugs that way). MAUI is different in kind, not just
degree:

- It needs the **`maui` workload** (`dotnet workload install maui`) —
  not part of the base .NET SDK, and this sandbox has neither the base
  SDK nor the workload.
- Building for real requires **platform-specific native tooling** this
  sandbox cannot have under any circumstance: the Android SDK/NDK for
  `net10.0-android`, and a macOS host with Xcode for `net10.0-ios`/
  `net10.0-maccatalyst`. There is no version of "run a build here" for
  this project the way there was for the web Blazor apps.
- The platform boilerplate (`AndroidManifest.xml`, `MainActivity.cs`,
  `Info.plist`, `AppDelegate.cs`, entitlements) follows a stable,
  long-established MAUI template shape this session has solid training
  knowledge of — but unlike the ASP.NET Core/Blazor APIs used in
  Phases 1–4, **none of it could be cross-checked against a live package
  index or reasoned through with the same confidence**. Treat every file
  under `Platforms/` as the least-trustworthy code in this whole
  migration effort.

## What's actually here

A minimal shell: `MauiProgram.cs`, `App.xaml(.cs)`, `MainPage.xaml(.cs)`
hosting a `BlazorWebView` that mounts a single `Home.razor` component
directly (no `Router`/`NavigationManager` — deliberately, to avoid one
more MAUI-specific subtlety this session had no way to verify). Platform
folders for Android, iOS, and MacCatalyst with the standard
`MainActivity`/`MainApplication`/`AppDelegate`/`Info.plist`/entitlements
files each platform needs to build at all.

## What's real vs. what's already dead code in the source

Checked before porting anything (same discipline Phase 3 used for
`customer-web`'s unused SEO code): the Ionic app this replaced was an
**unmodified Ionic starter template** — `appId` was still literally
`io.ionic.starter` (the Ionic CLI's own default, never customized), the one
real page was the literal "Blank" starter with an `ExploreContainer`
placeholder, and the installed Capacitor plugins (`@capacitor/haptics`,
`@capacitor/keyboard`, `@capacitor/status-bar`, `@capacitor/app`) were
**grepped, confirmed unused** — dependencies in `package.json`, never
imported or called anywhere in its source. So there was no real
native-plugin-parity work to do, and `Home.razor` here reproduces that
actual blank starter content, not an aspirational one.

## Deliberately not done

- **Not added to `DotNetMonoRepoTemplate.sln`.** Every other project in
  that solution builds with the base .NET SDK alone; this one needs the
  `maui` workload. Adding it would make `dotnet build DotNetMonoRepoTemplate.sln`
  fail for anyone without that workload installed — including the CI
  `backend-build` job, which currently builds the whole solution
  unconditionally on every push/PR. Keep this project's `.csproj` opened
  and built directly (`dotnet build src/CustomerMobile.csproj`) once
  the workload exists, not through the shared solution file.
- **No Dockerfile, no CI wiring.** MAUI apps aren't containers — they
  ship through app stores, not Coolify. A real CI path for this needs a
  macOS runner (for iOS/MacCatalyst) and Android SDK/workload setup
  (for Android) — genuinely separate infrastructure work, not something
  to bolt onto the existing web-app pipeline.
- **No test project.** bUnit's MAUI Blazor Hybrid test setup differs from
  the WASM Standalone/Web App setups used in Phases 1–3, and this
  scaffold is already carrying enough unverified surface — adding a test
  harness this session couldn't run either would just be more code with
  the same "trust but can't verify" problem, not real coverage.
- **No real native plugin work** (haptics, keyboard, status bar) — see
  above, nothing in the source actually uses these yet. MAUI's own
  equivalents (`Microsoft.Maui.Devices`, `Microsoft.Maui.ApplicationModel`)
  are the right place to look once there's a real feature that needs them.
- **App icon/splash are placeholder SVGs** (a solid teal square/circle) —
  real brand assets need to replace `Resources/AppIcon/*.svg` and
  `Resources/Splash/splash.svg`.
- **`ApplicationId` is a placeholder** (`com.nodemonorepotemplate.customermobile`)
  — replace it as part of the template's own project-name-substitution
  step (`CLAUDE.md`'s "Using this as a template" section), same as every
  other `node-mono-repo-template`-branded identifier in this repo.

## Before trusting any of this

In order of how much confidence each step would actually buy:

1. Install the MAUI workload (`dotnet workload install maui`) somewhere
   with the base .NET 10 SDK and try `dotnet build src/CustomerMobile.csproj
   -f net10.0-android` first — Android has the lowest platform-tooling
   bar (no macOS/Xcode requirement).
2. Only after that succeeds, attempt `-f net10.0-maccatalyst`/
   `-f net10.0-ios` on a macOS host with Xcode installed.
3. Run it on an actual emulator/simulator or device and confirm the
   `BlazorWebView` actually renders `Home.razor` — this is the one thing
   that would validate the whole `MainPage.xaml`/`RootComponent`/
   `HostPage` wiring end-to-end, and nothing short of running it can.
