---
name: mobile
description: Use when working on the mobile app under apps/mobile/customer-mobile. Covers .NET MAUI Blazor Hybrid — Razor pages/components hosted in a BlazorWebView, MauiProgram.cs DI setup, MAUI's own native-device APIs (camera, geolocation, storage, secure storage), and platform build configuration for Android/iOS/MacCatalyst. This replaced the repo's former Ionic + Capacitor React mobile app in Phase 5 of the Blazor migration.
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

## Read this first

This app is the **least-verified part of the entire migration**. It was
built without a working MAUI workload, without an Android/iOS toolchain,
and without a macOS host — none of it has been compiler-verified end to
end. Read `apps/mobile/customer-mobile/README.md` and
`.claude/rules/mobile.md` in full before trusting or extending anything
here. If you can install the `maui` workload and actually build a target
(`dotnet build src/CustomerMobile.csproj -f net10.0-android` has the lowest
tooling bar — no macOS/Xcode needed), do that before making claims about
whether something works.

## App location

```
apps/mobile/
└── customer-mobile/     .NET MAUI Blazor Hybrid customer app (C#)
```

Not in `DotNetMonoRepoTemplate.sln` (needs the `maui` workload, which the
base SDK the rest of the solution builds with doesn't have) and not
Dockerized (MAUI apps ship through app stores, not Coolify) — see
`.claude/rules/mobile.md` for the full list of deliberate gaps.

## Tech stack

| Concern | Technology |
|---------|-----------|
| App shell | .NET MAUI (`MauiProgram.cs`, `App.xaml`, `MainPage.xaml`) |
| UI | Blazor (Razor components), hosted in a `BlazorWebView` |
| Language | C# (.NET 10), same `Directory.Build.props`/nullable/analyzer rules as the rest of the repo |
| DI | `builder.Services` in `MauiProgram.cs` — same pattern as `admin-web`/`customer-web`, no service-locator |
| Validation | FluentValidation, once real forms exist (none do yet — see below) |
| Native device APIs | `Microsoft.Maui.Devices`, `Microsoft.Maui.ApplicationModel`, `Microsoft.Maui.Storage` — not Capacitor, which no longer exists in this repo |
| Styling | Tailwind v4 via the app's own Tailwind-only `package.json` (`pnpm --filter customer-mobile build:css`) — same convention as the two web Blazor apps |

## Directory structure (as it exists today — a minimal shell, not a full app)

```
apps/mobile/customer-mobile/
├── package.json              # Tailwind CLI only, no framework deps
├── README.md                 # read this before touching anything under src/
└── src/
    ├── CustomerMobile.csproj # multi-targets net10.0-android/-ios/-maccatalyst
    ├── MauiProgram.cs        # DI setup — currently just AddMauiBlazorWebView()
    ├── App.xaml(.cs)
    ├── MainPage.xaml(.cs)    # hosts the BlazorWebView, mounts Home.razor directly
    ├── Components/
    │   ├── _Imports.razor
    │   └── Pages/
    │       └── Home.razor    # reproduces the original Ionic starter's blank page — not aspirational content
    └── Platforms/
        ├── Android/          # MainActivity.cs, MainApplication.cs
        ├── iOS/               # AppDelegate.cs, Program.cs
        └── MacCatalyst/       # AppDelegate.cs, Program.cs
```

There is no `Router`/`NavigationManager` yet — `MainPage.xaml` mounts
`Home.razor` as a single fixed root component. There is no API client, no
auth/token storage, no forms, and no native plugin usage wired up yet. Do
not assume any of these exist; check the actual source before building on
top of it.

## Namespace

Root namespace is `CustomerMobile` throughout — `.csproj`
`<RootNamespace>`, every `namespace CustomerMobile;` / `namespace
CustomerMobile.Components...;` declaration, and the XAML `x:Class`/
`xmlns:local="clr-namespace:CustomerMobile"` attributes in `App.xaml`/
`MainPage.xaml` all need to match exactly or the app won't compile.

## Adding a page

Follow the Razor component conventions in `.claude/rules/frontend-blazor.md`
where they translate to a MAUI Blazor Hybrid context (nullable reference
types, no comments in code, FluentValidation called manually on submit for
any form). There is no established "page pattern" example in this app yet
the way there is for `admin-web`/`customer-web` — build the first one
carefully rather than copying a web-app pattern that assumes a browser
(no server-side rendering, no `HttpContext`, no `AuthorizeRouteView`
equivalent has been set up here).

## Native device APIs

Use MAUI's own cross-platform abstractions, not platform-specific APIs
directly, unless a MAUI equivalent genuinely doesn't exist:

```csharp
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

if (DeviceInfo.Platform == DevicePlatform.Android) { /* ... */ }

var location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));

await SecureStorage.SetAsync("access_token", token);
var token = await SecureStorage.GetAsync("access_token");
```

`SecureStorage` (not `Preferences`) is the right choice for auth tokens —
it's backed by Android Keystore / iOS Keychain, the MAUI equivalent of why
the old Capacitor app used `@capacitor/preferences` instead of
`localStorage`. Nothing in the current source uses this yet; when auth is
added, follow the token-in-memory-plus-secure-storage pattern
`frontend-blazor.md` documents for the web apps, adapted for
`SecureStorage` instead of a browser-only in-memory store.

## Native build commands

```bash
dotnet workload install maui
dotnet build apps/mobile/customer-mobile/src/CustomerMobile.csproj -f net10.0-android
dotnet build apps/mobile/customer-mobile/src/CustomerMobile.csproj -f net10.0-ios          # macOS + Xcode required
dotnet build apps/mobile/customer-mobile/src/CustomerMobile.csproj -f net10.0-maccatalyst   # macOS + Xcode required
```

No `pnpm`/`npx cap` commands — those were the retired Capacitor app's
tooling. The only pnpm touchpoint left for this app is
`pnpm --filter customer-mobile build:css` for the Tailwind CLI, same as
`admin-web`/`customer-web`.

## Before marking a task complete

Run `dotnet build apps/mobile/customer-mobile/src/CustomerMobile.csproj -f
<target>` for whichever platform target you can actually build against in
your environment — zero errors required, same bar as every other C#
project in this repo. If no MAUI workload/platform tooling is available,
say so explicitly rather than marking the task done unverified — this is
exactly the failure mode that made this app the least-verified part of the
migration in the first place.
