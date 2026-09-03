# admin-api

ASP.NET Core (.NET 10) service — administrative operations: JWT auth with MFA/TOTP (Otp.NET + QRCoder), refresh-token rotation, per-user "logout everywhere" via a Redis `minIat` marker, RBAC-gated user management, batch operations, and CSV/Excel reporting. Ported from the original Fastify/Node implementation; see `documentation/dotnet-migration-plan.md` for the migration record.

## Run locally

```bash
cd apps/backend/admin-api/src
cp ../.env.example ../.env   # fill in real values
dotnet run
```

## Build

```bash
dotnet build apps/backend/admin-api/src/AdminApi.csproj
```

## Test

No test project yet — add one under `apps/backend/admin-api/tests/` following the solution's xUnit convention once the service has stabilized post-migration.
