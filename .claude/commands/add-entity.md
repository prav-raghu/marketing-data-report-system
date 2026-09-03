---
description: Add a new domain entity with full CRUD across database, backend API, and frontend
argument-hint: <entity name and fields, e.g. "product with name, price, category, description, image">
---

Use the full-stack-orchestrator subagent to add this domain entity end to end: $ARGUMENTS

1. EF Core entity in `common/DotNetMonoRepoTemplate.Database/Entities/` with proper types, relations, and indexes, plus `OnModelCreating` configuration
2. `DbSet<T>` added to `AppDbContext`; hand off `dotnet ef migrations add <Name>` to the developer — never run it yourself
3. Backend API (DTOs, FluentValidation validator, service, Minimal API endpoints) in the appropriate service
4. Frontend pages (list, detail, create form, edit form) in admin-web
5. React Query hooks and Zod validation schemas (mirroring the backend's FluentValidation rules)
6. Register the service/validators in `Program.cs` and map the new endpoints

Present the plan and wait for confirmation before writing code.
