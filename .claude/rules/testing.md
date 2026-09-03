---
paths:
  - "apps/backend/**/tests/**/*.cs"
  - "**/*.test.ts"
  - "**/*.test.tsx"
  - "apps/frontend/**/tests/**/*.ts"
  - "apps/mobile/**/tests/**/*.ts"
---

# Testing Rules

Backend tests are xUnit (C#). Frontend/mobile tests are still Jest/Vitest (TypeScript) — that stack is unchanged by the migration. Which section applies depends on which kind of file is in context.

## Backend (xUnit) — apps/backend/*/tests/

No test projects exist yet for any of the four ported services — this section describes the target convention (per `CLAUDE.md`'s non-negotiable rule that every service layer needs automated unit tests), not something already built. Treat writing the first test project for a service as scaffolding work, not a gap to silently work around.

### Project layout

```
apps/backend/<service>/tests/
├── <Service>.Tests.csproj
├── Services/
│   └── <ServiceName>Tests.cs        # mirrors src/Services/<ServiceName>.cs 1:1
└── Fixtures/
    └── <Something>Fixture.cs        # shared test setup (EF Core in-memory provider, WebApplicationFactory, etc.)
```

One test class per service class, one `[Fact]`/`[Theory]` method per behavior. Test file location mirrors the `Services/` folder structure exactly — `Services/AuthServiceTests.cs` tests `Services/AuthService.cs`, nothing else.

### Unit tests — always mock/fake externals

Mock `IEmailService`, `IConnectionMultiplexer`/`RedisCacheService`, and any `HttpClient`-backed service (`Moq` or hand-written fakes — check `Directory.Packages.props` for what's already pulled in before adding a new mocking library). For `AppDbContext`, use EF Core's **in-memory provider** (`Microsoft.EntityFrameworkCore.InMemory`) or **Testcontainers** (a real ephemeral Postgres) depending on what the test needs to prove:

- In-memory provider: fast, good for service-logic tests that don't depend on Postgres-specific behavior (case-sensitivity, `ILike`, JSON column semantics, cascade-delete ordering)
- Testcontainers: required whenever the behavior under test depends on real Postgres semantics — e.g. `EF.Functions.ILike` case-insensitive search, `jsonb` column round-tripping, unique-constraint violations surfacing as the right exception type

Never point a unit test at the real dev/prod `DATABASE_URL`.

```csharp
public sealed class AuthServiceTests
{
    private static AppDbContext CreateInMemoryDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task LoginAsync_ReturnsInvalidMessage_WhenUserNotFound()
    {
        await using var db = CreateInMemoryDb();
        var service = new AuthService(db, /* fakes for the rest */);

        var result = await service.LoginAsync(new LoginRequestDto { Email = "nobody@example.com", Password = "irrelevant123", RememberMe = false });

        Assert.False(result.IsSuccessful);
        Assert.Equal("Invalid username or password", result.Message);
    }
}
```

### Integration tests — real Postgres via Testcontainers, real Redis where relevant

Full-pipeline tests (`AuthGuardMiddleware` → endpoint → service → DB) use `WebApplicationFactory<Program>` with the DI container's `AppDbContext` registration swapped for a Testcontainers Postgres instance. Clean up only what your test created — never truncate entire tables, never share mutable state across test classes (each gets its own container or its own uniquely-named in-memory database).

### Required coverage per service method

| Method shape | Must test |
|---|---|
| A lookup (`GetXByIdAsync`) | found, not found, (if role/status-filtered) filtered-out case |
| A list/paged query | returns paginated data, applies filters, empty-result case |
| A create | creates + persists, returns the right shape, duplicate/uniqueness rejection |
| An update | updates + persists, not-found case |
| A soft delete | sets `IsActive = false`, not-found case |
| Auth-adjacent (`LoginAsync`, `VerifyLoginMfaAsync`, etc.) | happy path, wrong password (constant-time — no timing/behavior difference between "user not found" and "wrong password"), locked-out case, MFA-required branch where applicable |
| Optimistic lock (where a `Version`/concurrency token exists) | throws/returns conflict when the token doesn't match |

### Required coverage per endpoint

| Scenario | Expected |
|---|---|
| Missing auth token | 401 |
| Wrong role/permission (`RequirePermissionsAttribute` present) | 403 |
| Invalid body (FluentValidation failure) | 400 with `{ isSuccessful: false, message: "Validation failed", errors: [...] }` |
| Not found | 404 |
| Happy path GET | 200 + `{ isSuccessful: true, data: {...} }` |
| Happy path POST (create) | 201 + `{ isSuccessful: true, data: {...} }` |
| Partial batch failure | 207 |

### Naming pattern

```csharp
public sealed class UserServiceTests
{
    [Fact]
    public async Task GetUserProfileAsync_ReturnsProfile_WhenUserIsAdminTier() { }

    [Fact]
    public async Task GetUserProfileAsync_ReturnsNull_WhenUserIsNotAdminTier() { }
}
```

Pattern: `MethodName_ExpectedOutcome_WhenCondition` — xUnit's idiom in place of Jest's nested `describe`/`it` English-sentence style, same intent (a failing test's name alone should explain the regression).

### Before marking a test task complete

Run `dotnet test apps/backend/<service>/tests/<Service>.Tests.csproj` — zero failures required. No coverage threshold is enforced yet in CI for the C# services (the Node era's `jest.config.ts` 75–80% thresholds don't have a .NET equivalent configured); don't invent a specific percentage gate, just make sure the required-coverage tables above are actually satisfied.

## Frontend/mobile (Jest/Vitest, unchanged) — apps/frontend/*, apps/mobile/*

This stack is unaffected by the backend migration.

### Test Types and Where They Live

- `tests/unit/` (or colocated `*.test.tsx`) — component/hook tests, API calls mocked
- `tests/integration/` — flows spanning multiple components against a mocked API layer (MSW or similar)

### Unit tests — always mock externals

Mock the API client (`axios`/`fetch` wrapper), never hit a real backend from a frontend unit test.

### Naming pattern

```typescript
describe('UserList', () => {
  describe('rendering', () => {
    it('shows a loading skeleton while the query is pending')
    it('renders a row per user once data resolves')
    it('shows an empty state when the list is empty')
  })
})
```

Pattern: `it('{does action} when {condition}')` in plain English.

### Test Environment Variables

Add to each app's `.env.example` as needed — frontend test env vars follow the same `VITE_<SCOPE>_*`/`NEXT_PUBLIC_<SCOPE>_*` scoping as runtime env vars (see `rules/frontend.md`).
