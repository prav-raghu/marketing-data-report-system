# api-gateway

ASP.NET Core (.NET 10) service — YARP reverse proxy in front of `customer-api`/`admin-api`/`schedule-api`, plus an optional HotChocolate GraphQL layer (disabled by default, `GRAPHQL_ENABLED`). Ported from the original Fastify/Node implementation; see `documentation/dotnet-migration-plan.md` for the migration record, including why this never ran real Apollo Federation despite the old `package.json`.

## Run locally

```bash
cd apps/backend/api-gateway/src
cp ../.env.example ../.env   # fill in real values
dotnet run
```

## Build

```bash
dotnet build apps/backend/api-gateway/src/ApiGateway.csproj
```

## Test

No test project yet — add one under `apps/backend/api-gateway/tests/` following the solution's xUnit convention once the service has stabilized post-migration.
