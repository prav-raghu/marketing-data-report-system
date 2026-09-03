# schedule-api

ASP.NET Core (.NET 10) service — cron-driven webhook delivery processor and health endpoints. Ported from the original Fastify/Node implementation; see `documentation/dotnet-migration-plan.md` for the migration record.

## Run locally

```bash
cd apps/backend/schedule-api/src
cp ../.env.example ../.env   # fill in real values
dotnet run
```

## Build

```bash
dotnet build apps/backend/schedule-api/src/ScheduleApi.csproj
```

## Test

No test project yet — add one under `apps/backend/schedule-api/tests/` following the solution's xUnit convention once the service has stabilized post-migration.
