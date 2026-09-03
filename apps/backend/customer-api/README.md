# customer-api

ASP.NET Core (.NET 10) service — customer-facing auth (register/login/refresh/logout/email verification), users, webhook subscriptions/deliveries, and CSV/Excel export. Ported from the original Fastify/Node implementation; see `documentation/dotnet-migration-plan.md` for the migration record.

## Run locally

```bash
cd apps/backend/customer-api/src
cp ../.env.example ../.env   # fill in real values
dotnet run
```

## Build

```bash
dotnet build apps/backend/customer-api/src/CustomerApi.csproj
```

## Test

No test project yet — add one under `apps/backend/customer-api/tests/` following the solution's xUnit convention once the service has stabilized post-migration.
