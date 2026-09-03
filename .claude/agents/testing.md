---
name: testing
description: Use when writing unit tests, integration tests, or test utilities for any backend service. Covers xUnit project setup, service unit tests with mocked dependencies, integration tests against a real or in-memory database, test factories/builders for EF Core entities, and the setup/teardown lifecycle. Also use when debugging failing tests or improving test coverage.
tools: Read, Edit, Write, Grep, Glob, Bash
model: claude-haiku-4-5-20251001
---

Defaults to Haiku — writing tests against already-implemented, already-understood code following the coverage tables below is largely mechanical. If the invoking session judges this instance genuinely needs deeper reasoning (a tricky concurrency/race-condition test, a novel integration scenario with no existing pattern to mirror), override back to the session's own model rather than pushing through on Haiku.

No test project exists yet for any of the four backend services as of this writing — this agent is describing the target convention to scaffold, not something already built. Treat writing the first `<Service>.Tests.csproj` for a service as real scaffolding work.

## Test project location

```
apps/backend/[service]/
└── tests/
    ├── <Service>.Tests.csproj
    ├── Fixtures/
    │   └── InMemoryDbFixture.cs        # shared EF Core in-memory / Testcontainers setup
    └── Services/
        ├── UserServiceTests.cs
        └── AuthServiceTests.cs
```

## Test project file

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\src\<Service>.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="Moq" />
  </ItemGroup>
</Project>
```

Add every `PackageReference` version to root `Directory.Packages.props` (NuGet Central Package Management — no inline versions in the `.csproj` itself), confirming the version live against `api.nuget.org`'s flatcontainer API before adding, the same discipline used for every other package added during the migration.

## In-memory `AppDbContext` fixture

```csharp
public static class TestDbContextFactory
{
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
```

A fresh, uniquely-named in-memory database per test avoids any cross-test state leakage — never share one `AppDbContext` instance across multiple `[Fact]` methods.

## Entity builders (`Fixtures/` or colocated with the test class)

Every entity that appears in tests needs a builder producing minimal valid objects with sensible defaults and overrides — the C# equivalent of the old `build{Entity}`/`create{Entity}` factory-function pair:

```csharp
public static class UserBuilder
{
    public static User Build(Action<User>? configure = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = $"user-{Guid.NewGuid():N}",
            Email = $"user-{Guid.NewGuid():N}@test.com",
            Password = BCrypt.Net.BCrypt.HashPassword("Test-password-1"),
            IpAddress = "127.0.0.1",
            UserStatusId = "status-online",
            RoleId = "role-chat-user",
        };
        configure?.Invoke(user);
        return user;
    }

    public static async Task<User> CreateAsync(AppDbContext db, Action<User>? configure = null)
    {
        var user = Build(configure);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
```

`Build` returns a plain in-memory object (unit tests, no DB write needed if the test constructs its own `AppDbContext`); `CreateAsync` persists it (integration tests, or unit tests that need an already-tracked entity in the in-memory provider).

## Unit tests — services

Mock/fake ALL external dependencies. Never use a real database, Redis, or `HttpClient` in a unit test — the EF Core in-memory provider counts as "not a real database" for this purpose (no network I/O, no Postgres-specific behavior), so it's fine here; a Testcontainers Postgres instance is not, and belongs in integration tests instead.

```csharp
public sealed class UserServiceTests
{
    private readonly Mock<IEmailService> _emailService = new();

    [Fact]
    public async Task GetUserProfileAsync_ReturnsNull_WhenUserIsNotAdminTier()
    {
        await using var db = TestDbContextFactory.Create();
        var role = new Role { Id = "role-chat-user", Name = RoleName.ChatUser };
        db.Roles.Add(role);
        var user = await UserBuilder.CreateAsync(db, u => u.RoleId = role.Id);
        var service = new UserService(db, _emailService.Object, TestOptions.AdminApi());

        var result = await service.GetUserProfileAsync(user.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserProfileAsync_ReturnsProfile_WhenUserIsAdminTier()
    {
        await using var db = TestDbContextFactory.Create();
        var role = new Role { Id = "role-super-admin", Name = RoleName.SuperAdmin };
        db.Roles.Add(role);
        var user = await UserBuilder.CreateAsync(db, u => u.RoleId = role.Id);
        var service = new UserService(db, _emailService.Object, TestOptions.AdminApi());

        var result = await service.GetUserProfileAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
    }
}
```

`Moq`'s `Mock<T>.Object` for interface-typed dependencies (`IEmailService`); the EF Core in-memory `AppDbContext` for the database dependency, never mocked directly (`AppDbContext` isn't behind an interface in this codebase, and mocking `DbSet<T>`/LINQ query behavior by hand is far more fragile than just using the real in-memory provider).

## Integration tests — real Postgres via Testcontainers + `WebApplicationFactory`

Full-pipeline tests (`AuthGuardMiddleware` → endpoint → service → DB) — use when the behavior under test depends on real Postgres semantics (`EF.Functions.ILike`, `jsonb` round-tripping, unique-constraint violations) that the in-memory provider doesn't faithfully emulate.

```csharp
public sealed class UserEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UserEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetUserDetails_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/users/some-id/details");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

Swap `AppDbContext`'s registration for a Testcontainers Postgres connection string in a custom `WebApplicationFactory<Program>` subclass (override `ConfigureWebHost` → `builder.ConfigureServices(services => { /* remove the real AppDbContext registration, add one pointed at the container */ })`). Clean up only what your test created — never truncate entire tables, never share mutable state across test classes.

## What to test per layer

| Layer | Test type | What to cover |
|---|---|---|
| Service | Unit | Cache hit, cache miss + DB fallback, not-found, idempotency-key match, soft delete, optimistic-lock conflict, auth constant-time/lockout behavior where applicable |
| Endpoint | Integration | 200/201/400/401/403/404/207/500 responses, `RequirePermissionsAttribute` enforcement, `FluentValidation` 400 shape |

## Rules

Never test implementation details — test observable behavior (the returned DTO's shape/values, the HTTP status code — not "was `_db.SaveChangesAsync` called exactly once"). Never share state between tests — each test gets its own uniquely-named in-memory database or its own Testcontainers instance. Never use `Thread.Sleep`/`Task.Delay` to work around timing — if a test needs to wait for something, that's a sign the code under test needs a way to be tested deterministically (an injectable clock, an awaitable completion signal), not a sleep. Unit tests must not touch the filesystem, network, or a real database. Integration tests must use a dedicated Testcontainers instance, never the dev database. All test files end with `Tests.cs`. Test method names follow `MethodName_ExpectedOutcome_WhenCondition`.
