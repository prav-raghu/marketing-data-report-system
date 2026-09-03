# Batch Operations & Reporting Documentation

## Overview

The batch operations and reporting system provides tools for administrators to perform bulk actions and generate system-wide reports. These features are implemented in **`admin-api`** (`Services/BatchOperationService.cs`, `Services/ReportingService.cs`, `Endpoints/BatchEndpoints.cs`, `Endpoints/ReportingEndpoints.cs`). **Every JSON example below uses `isSuccessful`, not `success`** — the actual response envelope this codebase uses everywhere (`DotNetMonoRepoTemplate.Types.ResponseDto`); a previous version of this doc used `success` throughout, which does not match the real wire contract.

## Batch Operations

### Features
- **Transaction Support**: `BulkUpdateUserStatus`/`BulkDeleteUsers` use `AppDbContext.Database.BeginTransactionAsync()`; `BulkCreateUsers` does not (each create commits independently, matching the Node original — see `webhook-events.md`/`api-builder.md` for the general "which batches are transactional" pattern)
- **Partial Failure Handling**: `ContinueOnError` mode for resilient operations
- **Progress Tracking**: Detailed results for each operation
- **Validation**: `ValidateBeforeExecute` pre-execution validation to catch errors early
- **Configurable Limits**: Max batch size controls

### API Endpoints

All batch endpoints require `PermissionName.BatchWrite` via `RequirePermissionsAttribute` — see `rbac.md`.

#### Bulk Create Users
```http
POST /api/v1/batch/users/create
Content-Type: application/json

{
  "users": [
    {
      "email": "user1@example.com",
      "username": "johndoe",
      "password": "SecureP@ss123",
      "ipAddress": "0.0.0.0",
      "roleId": "role-uuid",
      "userStatusId": "status-uuid"
    }
  ]
}
```

**Response:**
```json
{
  "isSuccessful": true,
  "data": {
    "total": 2,
    "successful": 2,
    "failed": 0,
    "results": [
      { "id": "user-0", "success": true, "data": { "id": "uuid", "email": "user1@example.com", "username": "johndoe" } },
      { "id": "user-1", "success": true, "data": { "id": "uuid", "email": "user2@example.com", "username": "janesmith" } }
    ]
  }
}
```

Note the two different `success` fields at different nesting levels: the top-level envelope uses `isSuccessful` (the project-wide convention), but each individual result item inside `results[]` uses `success` (matching `BatchOperationResult<T>.Success` in `DotNetMonoRepoTemplate.Types`) — this inconsistency is a real, deliberate detail of the actual DTO shape, not a typo to "fix."

#### Bulk Update User Status
```http
POST /api/v1/batch/users/update-status
Content-Type: application/json

{
  "updates": [
    { "userId": "uuid-1", "userStatusId": "status-uuid-active" },
    { "userId": "uuid-2", "userStatusId": "status-uuid-suspended" }
  ]
}
```

#### Bulk Delete Users
```http
POST /api/v1/batch/users/delete
Content-Type: application/json

{
  "userIds": ["uuid-1", "uuid-2", "uuid-3"]
}
```

### Batch Operation Options

```csharp
public sealed record BatchOperationOptions
{
    public bool? ContinueOnError { get; init; }
    public bool? ValidateBeforeExecute { get; init; }
    public int? MaxBatchSize { get; init; }  // default: 1000
}
```

### Status Codes

- **200 OK**: All operations successful
- **207 Multi-Status**: Partial success (some operations failed)
- **400 Bad Request**: Invalid request format (e.g. empty `users`/`updates`/`userIds` array)
- **500 Internal Server Error**: Unhandled exception (caught by `AppExceptionHandler` — see `backend-service.md`)

### Best Practices

1. **Batch Size**: Keep batches under 500 items for optimal performance
2. **Validation**: Enable `ValidateBeforeExecute` for critical operations
3. **Error Handling**: Use `ContinueOnError: true` for non-critical operations
4. **Monitoring**: Log all batch operations for audit trails (see `audit-log.md` — not yet wired into `BatchOperationService`)
5. **Rate Limiting**: See "Rate Limiting" below — batch endpoints currently only get the service-wide global tier, not a dedicated one

---

## Reporting System

### Features
- **Multiple Report Types**: User activity, webhook delivery, system metrics (`ReportType.AuditLog` exists as a constant but `FetchReportDataAsync` returns an empty list for it — no audit log data source exists yet, see `audit-log.md`)
- **Export Formats**: CSV, Excel (XLSX). JSON and PDF are **not implemented** — `ReportFormat.Json`/`ReportFormat.Pdf` exist as constants in `DotNetMonoRepoTemplate.Types` but `ReportingService.MapReportFormat` falls back to CSV for anything that isn't `EXCEL`, so requesting `"format": "JSON"` or `"format": "PDF"` today silently produces a CSV, not an error and not the requested format — this is a real gap, not a documentation nicety
- **"Streaming" Support**: Builds from an `IAsyncEnumerable` data source, but **buffers the whole result server-side** before responding — not true chunked HTTP streaming. See `export.instruction.md`'s "Known limitation" section before assuming this is memory-efficient for very large datasets
- **Flexible Filtering**: Date ranges, user IDs, status filters
- **Scheduled Reports**: Not implemented (see "Future Enhancements" below — still aspirational)

### Report Types

#### 1. User Activity Report
Tracks user registrations, updates, and status changes.

```http
GET /api/v1/reports/user-activity?startDate=2025-01-01&endDate=2025-12-31
```

**Query Parameters:**
- `startDate`: ISO 8601 date (default: 30 days ago)
- `endDate`: ISO 8601 date (default: today)
- `userId`: Filter by specific user
- `status`: Filter by user status ID

**Response:**
```json
{
  "isSuccessful": true,
  "data": {
    "recordCount": 150,
    "records": [
      {
        "userId": "uuid",
        "email": "user@example.com",
        "username": "johndoe",
        "status": "status-uuid",
        "createdAt": "2025-01-15T10:30:00.000Z",
        "lastUpdated": "2025-12-01T14:20:00.000Z"
      }
    ]
  }
}
```

Requires `PermissionName.ReportView`.

#### 2. Webhook Delivery Report
Monitors webhook delivery status and performance.

```http
GET /api/v1/reports/webhook-delivery?startDate=2025-12-01&status=pending
```

**Query Parameters:**
- `startDate`: ISO 8601 date (default: 7 days ago)
- `endDate`: ISO 8601 date (default: today)
- `status`: Filter by delivery status — **lowercase** (`pending`, `delivered`, `failed`, `retrying`), matching `DotNetMonoRepoTemplate.Types.WebhookDeliveryStatus`. This is not a stylistic detail — a case mismatch here was a real bug found and fixed during the migration (see `webhook-events.md`'s "case-mismatch bug" section); `"PENDING"` (uppercase) will never match any row.

**Response:**
```json
{
  "isSuccessful": true,
  "data": {
    "recordCount": 42,
    "records": [
      {
        "deliveryId": "uuid",
        "subscriptionId": "uuid",
        "eventType": "user.created",
        "status": "delivered",
        "httpStatus": 200,
        "attempts": 1,
        "createdAt": "2025-12-03T08:15:00.000Z",
        "deliveredAt": "2025-12-03T08:15:02.000Z"
      }
    ]
  }
}
```

Requires `PermissionName.ReportView`.

#### 3. System Metrics Report
Provides real-time system statistics.

```http
GET /api/v1/reports/system-metrics
```

**Response:**
```json
{
  "isSuccessful": true,
  "data": {
    "recordCount": 4,
    "metrics": [
      { "metric": "Total Users", "value": 1250, "timestamp": "2025-12-03T12:00:00.000Z" },
      { "metric": "Active Users", "value": 890, "timestamp": "2025-12-03T12:00:00.000Z" },
      { "metric": "Webhook Subscriptions", "value": 45, "timestamp": "2025-12-03T12:00:00.000Z" },
      { "metric": "Pending Webhook Deliveries", "value": 12, "timestamp": "2025-12-03T12:00:00.000Z" }
    ]
  }
}
```

Requires `PermissionName.ReportView`.

### Export Report (File Download)

#### Generate Report
```http
POST /api/v1/reports/generate
Content-Type: application/json

{
  "type": "USER_ACTIVITY",
  "format": "EXCEL",
  "filters": {
    "startDate": "2025-01-01T00:00:00.000Z",
    "endDate": "2025-12-31T23:59:59.000Z",
    "status": "status-uuid"
  },
  "includeHeaders": true
}
```

Requires `PermissionName.ReportExport`. `type` is one of `USER_ACTIVITY`/`WEBHOOK_DELIVERY`/`SYSTEM_METRICS`/`AUDIT_LOG`/`CUSTOM` (`DotNetMonoRepoTemplate.Types.ReportType`); `format` is `CSV`/`EXCEL` (`JSON`/`PDF` accepted but silently downgraded to CSV — see "Features" above).

**Response:**
```json
{
  "isSuccessful": true,
  "data": {
    "id": "report_1733240000_abc123",
    "type": "USER_ACTIVITY",
    "format": "EXCEL",
    "status": "COMPLETED",
    "recordCount": 150,
    "url": "data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,...",
    "generatedAt": "2025-12-03T12:00:00.000Z"
  }
}
```

The `url` is a `data:` URI with the base64-encoded file inline in the JSON response — there is no separate download endpoint or temporary file storage; the whole export is returned in this one response body.

#### Stream Report (built from an async source; still server-buffered — see "Features" above)
```http
GET /api/v1/reports/stream?type=USER_ACTIVITY&format=csv&startDate=2025-01-01
```

Requires `PermissionName.ReportExport`.

**Response Headers:**
```
Content-Type: text/csv
Content-Disposition: attachment; filename="report-USER_ACTIVITY-1733240000.csv"
```

The actual response body for `/reports/stream` is newline-delimited JSON records (see `ReportingEndpoints.cs` — each record from `ReportingService.StreamReportDataAsync` is serialized and written with a trailing `\n`), not a formatted CSV/Excel file the way `/reports/generate` produces — despite the `Content-Type: text/csv` header. This is a real inconsistency in the current implementation (carried over from the Node original) worth knowing before building a client that expects the streamed body to actually parse as CSV.

### Report Formats

| Format | Extension | MIME Type | Status |
|---|---|---|---|
| CSV | `.csv` | `text/csv` | Implemented |
| Excel | `.xlsx` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | Implemented (ClosedXML has no true streaming — see `export.instruction.md`) |
| JSON | `.json` | `application/json` | **Not implemented** — silently downgrades to CSV |
| PDF | `.pdf` | `application/pdf` | **Not implemented** — silently downgrades to CSV |

### Best Practices

1. **Filter Data**: Always specify date ranges to limit dataset size — there's no automatic cap beyond the webhook-delivery report's hardcoded `Take(10000)`
2. **Report Caching**: Not implemented — every `/reports/generate` call re-runs the query and re-builds the file. If a task needs caching, that's new work, not a config flag to flip
3. **Rate Limiting**: See "Rate Limiting" below
4. **Authentication**: All report endpoints require `admin-api` auth plus the relevant `ReportView`/`ReportExport` permission
5. **Audit Logging**: Not yet wired into `ReportingService` (see `audit-log.md`)

---

## Integration Examples (frontend — unaffected by the backend migration)

### React Frontend (Report Download)

```typescript
import axios from 'axios';

async function downloadReport(type: string, format: string) {
  const response = await axios.post('/api/v1/reports/generate', {
    type,
    format,
    filters: {
      startDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000),
      endDate: new Date(),
    },
    includeHeaders: true,
  });

  const { url } = response.data.data;

  const link = document.createElement('a');
  link.href = url;
  link.download = `report-${type}-${Date.now()}.${format.toLowerCase()}`;
  link.click();
}
```

### Batch Operation with Progress Tracking

```typescript
async function bulkUpdateUsers(updates: Array<{ userId: string; userStatusId: string }>) {
  const response = await axios.post('/api/v1/batch/users/update-status', { updates });

  const { total, successful, failed, results } = response.data.data;
  console.log(`Completed: ${successful}/${total} successful, ${failed} failed`);

  if (failed > 0) {
    const failedItems = results.filter((r: { success: boolean }) => !r.success);
    console.error('Failed items:', failedItems);
  }
}
```

---

## Rate Limiting — what's actually configured vs. aspirational

Batch and reporting endpoints do **not** have a dedicated rate-limit policy today — they fall under `admin-api`'s global tier (200 req/min per IP, see `rules/backend.md`), same as everything else not opted into `auth`/`sensitive`/`adminOperations`. Per-hour numbers like "batch operations: 10/hour" or "reports: 20/hour" describe a design intent, not a configured `RequireRateLimiting("...")` policy — if a task needs a real batch/reporting-specific rate limit, that's implementation work (a new named policy in `Program.cs`, per `rules/backend.md`'s pattern), not something to assume already exists.

## Security Considerations

- Access tokens are 1-hour TTL (see `jwt-security.md` — not "15 minutes," that figure was never accurate for this codebase)
- All batch/reporting endpoints require `admin-api` auth (admin-tier role) plus the relevant permission — see `rbac.md`
- Enforce maximum batch sizes — `MaxBatchSize` per operation type (500 for create/delete, 1000 for status updates — see `BatchOperationService`'s real defaults, not a single hard "1000" ceiling for everything)
- Audit logging of batch/report operations is not yet implemented (see `audit-log.md`)

## Error Handling

### Batch Operation Errors

**400 Bad Request:**
```json
{ "isSuccessful": false, "message": "No users provided" }
```

**207 Multi-Status:**
```json
{
  "isSuccessful": false,
  "data": {
    "total": 10,
    "successful": 8,
    "failed": 2,
    "results": [
      { "id": "user-7", "success": false, "error": "Email already exists" },
      { "id": "user-9", "success": false, "error": "Invalid password format" }
    ]
  }
}
```

### Report Generation Errors

A failed report generation does not throw a 500 — `ReportingService.GenerateReportAsync` catches internally and returns a `ReportResult` with `Status: "FAILED"` inside a 200 response:

```json
{
  "isSuccessful": false,
  "data": {
    "id": "report_123",
    "status": "FAILED",
    "error": "Query timeout after 30 seconds"
  }
}
```

`isSuccessful` on the outer envelope reflects `result.Status == ReportStatus.Completed` — so a failed report generation still returns HTTP 200 with `isSuccessful: false` and the error nested in `data`, not an HTTP error status. Don't assume a non-2xx status signals a failed report — check `data.status`/the outer `isSuccessful` instead.

---

## Future Enhancements (not built — aspirational only)

1. **Scheduled Reports**: Cron-based report generation with email delivery
2. **PDF Reports**: Formatted PDF generation with charts
3. **JSON report export**: Currently silently downgrades to CSV instead
4. **Report Templates**: Customizable report layouts
5. **Batch Validation Rules**: Custom validation per operation type beyond what `ValidateBatch` already does
6. **Progress Webhooks**: Real-time progress updates for long-running batches
7. **Report Caching**: Redis-based caching layer
8. **Audit Log Report**: `ReportType.AuditLog` exists but returns an empty list — needs `audit-log.md`'s pattern implemented first
9. **True streaming exports**: See `export.instruction.md`'s known limitation
