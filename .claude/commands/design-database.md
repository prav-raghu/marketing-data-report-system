---
description: Design and add EF Core entities from a business domain description
argument-hint: <domain description, e.g. "ecommerce with products, categories, orders, and payments">
---

Use the domain-modeler subagent to design and add EF Core entities for: $ARGUMENTS

1. Read `common/DotNetMonoRepoTemplate.Database/AppDbContext.cs` and `Entities/` to understand the current schema
2. Identify all entities, fields, and relationships
3. Create entity classes following conventions: PascalCase entity/property names (auto-mapped to `snake_case` via `EFCore.NamingConventions`), `AuditableEntity` inheritance for the audit fields (`Id`/`IsActive`/`CreatedAt`/`UpdatedAt`/`CreatedBy`/`ModifiedBy`), `OnModelCreating` relation/index config, `decimal` for money, string-constant classes (not native `enum`) for status fields
4. Add the new `DbSet<T>` properties to `AppDbContext`
5. Hand off `dotnet ef migrations add <Name>` to the developer — never run it yourself, and note that no EF Core migrations exist yet in this repo (see `ef-core.md`'s baseline-migration caveat)

Present the entity design for review before writing.
