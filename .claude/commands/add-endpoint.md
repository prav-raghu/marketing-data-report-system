---
description: Add backend API endpoints for an existing EF Core entity — generates DTOs, validator, service, and Minimal API endpoints
argument-hint: <entity name and target service, e.g. "product endpoints in customer-api">
---

Use the api-builder subagent to generate complete backend API endpoints for: $ARGUMENTS

1. Read the EF Core entity class under `common/DotNetMonoRepoTemplate.Database/Entities/` to understand its fields and relations
2. Create `sealed record` DTOs for create, update, getById, list (pagination/search/filter), and delete responses
3. Create a FluentValidation validator class per request DTO
4. Create a `sealed class` service with full CRUD (`CreateAsync`, `FindByIdAsync`, `ListAsync`, `UpdateAsync`, `SoftDeleteAsync`)
5. Create an `Endpoints/<Domain>Endpoints.cs` static class with a `Map<Domain>Endpoints` extension method
6. Register the service and validators in `Program.cs`, and call `app.Map<Domain>Endpoints()`

Follow existing patterns in the target service exactly — see the `api-builder` agent.
