---
applyTo: "apps/frontend/**/*.ts,apps/frontend/**/*.tsx,apps/mobile/**/*.ts,apps/backend/**/*.cs,common/DotNetMonoRepoTemplate.*/**/*.cs"
description: "Date/time convention across DB, service/API, and UI layers — one storage format, one wire format, one display format"
---

# Date & Time Handling — DB → Service → UI

Three layers, three different jobs, three different formats. Never let a display format leak into storage or the wire, and never let storage format leak into the UI.

## 1. Database layer — EF Core `DateTime`, stored UTC

Every timestamp property is C#'s `DateTime` type (mapped to Postgres `timestamptz` via Npgsql), always written and read in UTC. Never store a formatted string (`"25/12/2025"`) in a date column, and never add a parallel `string` property to hold a display-formatted copy of a date — format at render time, not at rest.

```csharp
public DateTime CreatedAt { get; set; }
public DateTime EventDate { get; set; }
```

Postgres `timestamptz` normalizes to UTC internally regardless of session timezone — this is already correct by default and needs no extra configuration. `AppDbContext.SaveChanges()`/`SaveChangesAsync()` already auto-stamp `CreatedAt`/`UpdatedAt` on every `AuditableEntity`/`TimestampedEntity` with `DateTime.UtcNow` — never set those two by hand, and never use `DateTime.Now` (local time) anywhere in backend code, only `DateTime.UtcNow`.

## 2. Service / API layer — ISO 8601, always, both directions

- **Outbound** (API response): a `DateTime` serializes to ISO 8601 automatically via `System.Text.Json` (`2025-12-25T14:30:00.000Z` for UTC values) — this is ASP.NET Core Minimal APIs' default behavior for any `DateTime` in a response body (`Results.Json(...)`, or an implicit JSON result). Never manually call a `.ToString("...")` format before sending it — that's a UI concern, not a service concern.
- **Inbound** (request body): FluentValidation validates incoming date fields once bound — `System.Text.Json`'s default `DateTime` deserialization already requires ISO 8601 shape (a non-ISO string fails model binding before the validator even runs), so there's no separate `format: 'date-time'` declaration to write the way AJV needed one; add a FluentValidation rule only for constraints beyond "is this parseable as a date" (e.g. `.Must(d => d > DateTime.UtcNow)` for a future-only field).
- Minimal API parameter binding converts the validated ISO string to a `DateTime` automatically before your endpoint delegate runs — never accept a raw `string` date parameter and hand-parse it yourself unless the field is genuinely free-text.
- Never accept `dd/mm/yyyy` on a backend request body. The `dd/MM/yyyy [HH:mm:ss]` format is a UI presentation concern only — the frontend converts it to ISO 8601 before the request ever leaves the browser (see §3). If a backend DTO is accepting `dd/mm/yyyy`, that's a bug — fix the frontend's outbound mapping instead of loosening the backend's date binding.

## 3. UI layer — display and input as `dd/MM/yyyy`, optional `HH:mm:ss` (unchanged by the backend migration)

Both frontend apps already depend on `date-fns` — use it, not `Date.prototype.toLocaleDateString()` (locale-dependent, not guaranteed `dd/mm/yyyy` across browsers/OS locales) and not a second date library.

```typescript
// src/utilities/format-date.ts
import { format, parse, isValid } from 'date-fns';

const DATE_FORMAT = 'dd/MM/yyyy';
const DATE_TIME_FORMAT = 'dd/MM/yyyy HH:mm:ss';

export function formatDate(value: string | Date): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  return format(date, DATE_FORMAT);
}

export function formatDateTime(value: string | Date): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  return format(date, DATE_TIME_FORMAT);
}

export function parseDateInput(value: string, withTime = false): Date | null {
  const parsed = parse(value, withTime ? DATE_TIME_FORMAT : DATE_FORMAT, new Date());
  return isValid(parsed) ? parsed : null;
}

export function toApiDate(value: Date): string {
  return value.toISOString();
}
```

- **Display**: every place a date/timestamp is rendered — tables, detail views, PDFs, exports — goes through `formatDate`/`formatDateTime`. Time is appended (`HH:mm:ss`) only when the field is genuinely a timestamp the user cares about to the second (audit trails, activity logs); a plain business date (order date, birthdate) shows date-only.
- **Input**: a native date picker (`<input type="date">` wrapped by a component, or a calendar widget) already returns a real `Date`/ISO value — no parsing needed, and this is the default choice. Only when a field genuinely requires free-text entry does `parseDateInput` come into play, and that field's Zod schema validates the `dd/MM/yyyy` shape before parsing:

```typescript
date: z.string().regex(/^\d{2}\/\d{2}\/\d{4}$/, 'Use dd/mm/yyyy').refine((v) => parseDateInput(v) !== null, 'Invalid date')
```

- **Outbound**: before the value reaches `apiClient`, convert with `toApiDate` (or just send the picker's native ISO value straight through) — the wire format is always ISO 8601, never `dd/mm/yyyy`, matching §2.

## Summary

| Layer | Format | Never |
|---|---|---|
| Database | `DateTime` / `timestamptz`, UTC (`DateTime.UtcNow`, never `.Now`) | A `string` property holding a formatted date |
| Service / API (request + response) | ISO 8601, via `System.Text.Json`'s default `DateTime` handling | `dd/mm/yyyy` on the wire in either direction |
| UI display | `dd/MM/yyyy`, optional ` HH:mm:ss` via `date-fns` | `toLocaleDateString()`, a second date library, hand-rolled string splitting |
