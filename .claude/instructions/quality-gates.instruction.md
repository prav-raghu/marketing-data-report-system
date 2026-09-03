# Quality Gates Configuration

This document outlines the quality gates and standards enforced in this project. The ESLint/SonarJS/coverage sections below apply to the frontend/mobile TypeScript apps only — the backend moved to C#/.NET and enforces its equivalent gates differently: nullable-reference warnings as build errors (`Directory.Build.props`), not an ESLint config, and there's no backend equivalent of SonarCloud/coverage-threshold enforcement configured yet (see `csharp-standards.md` and `rules/backend.md`'s "Before marking complete" section for what actually gates a backend change today).

## ESLint Quality Rules (frontend/mobile only)

### TypeScript Rules

| Rule                            | Severity | Description                              |
| ------------------------------- | -------- | ---------------------------------------- |
| `no-explicit-any`               | Error    | Prevents usage of `any` type             |
| `explicit-function-return-type` | Warn     | Requires explicit return types (backend) |
| `no-unused-vars`                | Error    | Flags unused variables                   |
| `consistent-type-imports`       | Error    | Enforces `type` imports                  |
| `no-floating-promises`          | Error    | Requires handling promises               |

### Code Quality (SonarJS)

| Rule                    | Threshold | Description                                  |
| ----------------------- | --------- | -------------------------------------------- |
| `cognitive-complexity`  | 15        | Maximum cognitive complexity per function    |
| `no-duplicate-string`   | 3         | Maximum duplicate strings before extraction  |
| `no-identical-functions`| Warn      | Flags identical function bodies              |

### Import Organization

- Groups: builtin → external → internal → parent/sibling → index → type
- Alphabetical ordering within groups
- No duplicate imports

## Test Coverage Thresholds

| Metric     | Minimum |
| ---------- | ------- |
| Branches   | 80%     |
| Functions  | 80%     |
| Lines      | 80%     |
| Statements | 80%     |

## SonarCloud Quality Gate

Default "Sonar way" quality gate requires:

### On New Code (PRs)

| Metric                      | Condition |
| --------------------------- | --------- |
| Coverage                    | ≥ 80%     |
| Duplicated Lines            | ≤ 3%      |
| Maintainability Rating      | A         |
| Reliability Rating          | A         |
| Security Rating             | A         |
| Security Hotspots Reviewed  | 100%      |

### Overall Code

| Metric                 | Condition |
| ---------------------- | --------- |
| Coverage               | ≥ 80%     |
| Duplicated Lines       | ≤ 3%      |
| Maintainability Rating | A         |
| Reliability Rating     | A         |
| Security Rating        | A         |

## Commit Message Convention

Format: `type(scope): description`

### Types

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Code style (formatting)
- `refactor`: Code refactoring
- `perf`: Performance improvement
- `test`: Adding/updating tests
- `build`: Build system changes
- `ci`: CI configuration
- `chore`: Maintenance tasks
- `revert`: Reverting changes

### Scopes

All package/service/library names are valid scopes:

- Backend: `api-gateway`, `customer-api`, `admin-api`, `schedule-api`
- Frontend: `customer-web`, `admin-web`, `customer-mobile`
- Common (C# libraries): `database`, `cache`, `email`, `sms`, `storage`, `export`, `logging`, `metrics`, `observability`, `queue`, `types`, `utilities` (lowercase scope name for the commit convention even though the actual library is `DotNetMonoRepoTemplate.Database` etc. — the scope names below the `DotNetMonoRepoTemplate.` prefix)
- Meta: `deps`, `ci`, `docs`, `release`

## Pre-commit Checks

Automated via Husky + lint-staged:

1. **Staged TypeScript/JavaScript files:**
   - ESLint with auto-fix
   - Prettier formatting
   - Must pass with zero warnings

2. **Commit message:**
   - Must follow conventional commit format
   - Type must be from allowed list
   - Subject must be lowercase

3. **Pre-push:**
   - Changeset status check (warns if no changeset)

Husky/lint-staged only run against staged TypeScript/JavaScript files — there's no equivalent pre-commit hook wired up for backend C# changes yet (no `dotnet format`/analyzer check runs automatically on commit). Until that exists, backend changes rely on the developer (or Claude, per `rules/backend.md`) running `dotnet build` manually before considering a task done.
