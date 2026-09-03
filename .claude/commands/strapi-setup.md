# Strapi CMS Setup (retired)

`apps/cms` is Piranha CMS (ASP.NET Core / C#, .NET 10) now — Strapi was fully decommissioned in Phase 8 of `documentation/dotnet-migration-plan.md`. Everything this command used to cover (pnpm-workspace exclusion, npm-vs-pnpm package management, native module build issues) no longer applies: `apps/cms` was never a pnpm workspace member to begin with post-migration, and there is no Node tooling left under `apps/cms` to configure.

For CMS work now, see `apps/cms/README.md` — content types are C# classes under `apps/cms/src/Models/` decorated with Piranha's `[PageType]`/`[PostType]` attributes, registered in `Program.cs`'s `PageTypeBuilder`, following the same options-pattern/no-hardcoded-secrets conventions as every other backend service.

This file is kept only so `/strapi-setup` doesn't dangle as an unresolvable slash command — it is not a working setup guide.
