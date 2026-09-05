# ingestion-api

Control plane and extraction host for the marketing data ingestion platform. Port **4007**.

Design background: [Marketing Data Platform](../../../documentation/architecture/marketing-data-platform.md),
[Canonical Data Model](../../../documentation/architecture/marketing-canonical-data-model.md),
[Delivery Roadmap](../../../documentation/architecture/marketing-ingestion-roadmap.md).

## Status

Phase 0 (control plane, envelope machinery, connector abstraction) is complete
and replayed onto the post-Blazor-migration codebase. Two connectors are in:
**TikTok** and **Meta**.

`GET /internal/v1/connectors` now reports `tiktok_ads` and `meta_ads` in
`registeredConnectorKeys`.

**Compiler-verified.** Built and tested on .NET SDK 10.0.400:
`dotnet build DotNetMonoRepoTemplate.sln` reports 0 errors, and all 71 tests in
`IngestionApi.Tests` pass.

```bash
dotnet build DotNetMonoRepoTemplate.sln
dotnet test apps/backend/ingestion-api/tests/IngestionApi.Tests.csproj
```

What that does and does not establish: the code compiles under
`WarningsAsErrors=Nullable` with `EnforceCodeStyleInBuild`, and the service
layer behaves correctly against the EF Core in-memory provider. It has **not**
been run against real Postgres, real Redis, or real Azure Blob storage, so the
Npgsql-specific parts — the `jsonb` mapping, the enum-to-string conversions and
the partial unique index `ix_ingestion_runs_single_in_flight` — are unproven
until a migration is generated and applied. The in-memory provider ignores index
filters entirely, so the database-level guard against two concurrent runs is
currently enforced only by the service check the tests do cover.

## What the service does

| Concern | Where |
|---|---|
| Run lifecycle (start, poll, complete, fail) | `Services/IngestionRunService.cs` |
| Connector resolution by source key | `Services/ConnectorRegistry.cs` |
| Account tier recalculation by trailing spend | `Services/AccountTierService.cs`, `Services/TierAssignment.cs` |
| Envelope construction and raw zone writes | `common/DotNetMonoRepoTemplate.Ingestion` |
| Control plane persistence | `common/DotNetMonoRepoTemplate.Database` entities |

## How Data Factory drives it

Data Factory owns scheduling and dependencies; this service owns the awkward
parts of talking to vendor APIs. They meet over four endpoints:

```text
POST   /internal/v1/runs               start a run, returns 201 with the run id
GET    /internal/v1/runs/{runId}       poll until the status is terminal
POST   /internal/v1/runs/{runId}/complete
POST   /internal/v1/runs/{runId}/fail
```

A Data Factory pipeline calls `POST /internal/v1/runs`, then polls the `GET`
until `status` is `Succeeded`, `Failed` or `Cancelled`.

Starting a run when one is already in flight for the same connector returns
**409 Conflict** rather than starting a second one. This is enforced twice on
purpose: the service checks before inserting, and a partial unique index
(`ix_ingestion_runs_single_in_flight`) rejects the row if two callers race. A
double-firing Data Factory trigger is a normal operational event, not an
exception.

### Window resolution

- Supply `windowStart` and `windowEnd` for a backfill and they are used verbatim.
- Supply neither and the window is derived as the connector's restatement window
  ending on today's date **in the reporting timezone**, not in UTC. At UTC+2 a
  run starting at 22:30 UTC is already tomorrow locally, and getting this wrong
  silently shifts a day of spend.

## Configuration

All configuration is environment-sourced through `IngestionApiOptions` and
validated at startup — the service refuses to boot on invalid configuration
rather than failing later. See `.env.example`.

| Variable | Notes |
|---|---|
| `INGESTION_API_KEY` | Minimum 32 characters. Compared in fixed time |
| `DATABASE_URL` | Control plane only. No marketing data is stored here |
| `REDIS_URL` | Shared rate limiting for connectors |
| `RAW_ZONE_CONNECTION_STRING` | ADLS Gen2 storage account |
| `RAW_ZONE_CONTAINER` | Defaults to `raw` |
| `REPORTING_TIMEZONE` | Defaults to `Africa/Johannesburg` |
| `REPORTING_CURRENCY` | Defaults to `ZAR` |
| `MAX_CONCURRENT_EXTRACTIONS` | Extraction fan-out ceiling |

## The TikTok connector — what is proven and what is not

**Proven by tests** (9 of them, in `tests/Connectors/`): pagination follows
`total_page` to the end, breakdown dimensions land in the idempotency key in
sorted order, the resolved access token is sent on every request, a rate-limit
permit is taken per page, a non-zero `code` in the response body raises
`TikTokApiException`, HTTP 429 is retried with exponential backoff, rows missing
the natural key are skipped rather than failing the batch, and both dimensions
and metrics survive into the payload.

**Not proven: the vendor API specifics.** TikTok's developer documentation is
unreachable from the environment that wrote this (egress-blocked), so the
endpoint path, the response envelope shape, the metric names and the breakdown
dimension names come from prior knowledge, not from a freshly-read spec. Every
one of those details is deliberately confined to a single file,
`Connectors/TikTok/TikTokApiContract.cs`, so correcting them against the real
documentation is a one-file change plus a fixture update — not a rewrite. The
connector logic above is independent of whether those names are right.

Verify against current TikTok docs before the first live run:

| Constant | What to confirm |
|---|---|
| `ReportPath` | The reporting endpoint path and API version segment |
| `AccessTokenHeader` | The header name carrying the token |
| `SuccessCode`, envelope fields | That success is `code: 0` and the `data.list` / `data.page_info` shape holds |
| `Metrics` | Every metric name, and that `video_watched_2s` is still the Meta-comparable view definition |
| `BreakdownDimensions` | The dimension name for each canonical breakdown |
| `ReportType`, `DataLevel`, `ServiceType` | That these enum values are current |

Credentials resolve through `IConnectorSecretResolver` under the name
`TIKTOK_ACCESS_TOKEN_<advertiser_id>`. The only implementation today reads
configuration; a Key Vault implementation drops in behind the same interface
without touching the connector.

## The Meta connector — three phases, not one request

Meta's insights endpoint is asynchronous, so the connector runs submit, poll,
download rather than a single call:

1. **Submit** a `POST` to `{version}/{account}/insights` with the field list,
   breakdowns and time range. Meta returns a `report_run_id`.
2. **Poll** `{version}/{report_run_id}` until `async_status` reaches
   `Job Completed`. `Job Failed` and `Job Skipped` throw immediately rather than
   spinning; exceeding `MaxPollAttempts` throws with the run id attached.
3. **Download** `{version}/{report_run_id}/insights`, following
   `paging.cursors.after` until a page returns no rows.

Pagination deliberately rebuilds the next URL from the `after` cursor rather than
following the absolute `paging.next` URL Meta supplies. Following a
response-supplied absolute URL means issuing requests at a host the response
chose, which is a needless server-side request forgery shape for no benefit.

### Two decisions specific to Meta

**`publisher_platform` is always requested**, whether or not the account's tier
asks for breakdowns. It is the only thing separating Facebook rows from Instagram
rows, and it is why Instagram is not a separate connector — one pull yields both
platforms and costs one account's rate-limit budget instead of two. It lands in
the idempotency key, so the two platforms never collide on the same key.

**`actions[]` and `action_values[]` are preserved verbatim.** Filtering them to
the configured conversion action types and summing is a semantic decision that
belongs in the bronze-to-silver job, where it can be changed and replayed against
history without redeploying an extractor. The connector does no semantic mapping,
per the thin-edge-transform principle in the architecture document. The same
reasoning is why `inline_link_clicks` is requested alongside `clicks` rather than
one being chosen here.

**Not proven: the vendor API specifics.** Meta's developer documentation is
egress-blocked from the environment that wrote this, exactly as TikTok's was. The
API version, path shape, `async_status` string values, field names and breakdown
names live in `Connectors/Meta/MetaApiContract.cs` and `MetaOptions.ApiVersion`.
Verify those against current docs before a live run. The three-phase flow,
cursor pagination, retry behaviour and key construction are covered by 13 tests
and do not depend on those names being right.

## Writing a connector

Implement `ISourceConnector` from `DotNetMonoRepoTemplate.Ingestion.Connectors`
and register it in DI. The registry picks it up automatically and rejects
duplicate source keys at startup.

```csharp
public sealed class TikTokAdsConnector : ISourceConnector
{
    public string SourceKey => "tiktok_ads";

    public SourceCapabilities Capabilities { get; } = new()
    {
        NativeFormat = PayloadFormat.Json,
        SupportsIncremental = true,
        SupportsBreakdowns = true,
        SupportsAttributionWindows = true,
        MaxRestatementDays = 28,
    };

    public async IAsyncEnumerable<SourceRecord> ExtractAsync(
        ExtractionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // yield one SourceRecord per row, streaming
    }
}
```

`IAsyncEnumerable` is not decoration. A single account-month at full breakdown
grain can be millions of rows; materialising a `List<T>` is how the pod runs out
of memory. The envelope writer consumes the stream and flushes to blob storage
in fixed-size parts.

### Rules a connector must honour

1. **Build the idempotency key with `IdempotencyKey.Create`.** It sorts
   breakdowns so the same logical record always produces the same key regardless
   of dictionary ordering. Hand-building the key breaks the downstream MERGE.
2. **Report the original payload format.** A connector that converts XML to JSON
   sets `PayloadFormat.Xml` and points `RawArtifactPath` at the retained original.
   The converted form is a convenience; the original is the system of record.
3. **Stream XML with `XmlReader`.** Never `XDocument` or `XmlDocument` over a
   full response.
4. **Never default a missing metric to zero.** Null is an absence, zero is a
   measurement, and charting them identically understates the channel.

## Raw zone layout

```text
source={system}/entity={entity}/ingest_date={yyyy-MM-dd}/run_id={runId}/part-00000.json.gz
source={system}/entity={entity}/ingest_date={yyyy-MM-dd}/run_id={runId}/original/artifact-00000.xml
```

Parts are gzipped newline-delimited JSON, one envelope per line, newline forced
to `\n` so output is byte-identical across platforms. Blobs are written with
`overwrite: false`; a 409 from storage is logged and treated as already-written,
which keeps raw immutable while leaving connector retries safe.

`payloadHash` is computed over a **canonically ordered** serialisation of the
payload (object keys sorted, array order preserved), so a vendor returning the
same data with different property ordering hashes identically. Without that, the
restatement skip described in the architecture document would never trigger and
every re-pulled row would look changed.

## Account tiering

`POST /internal/v1/connectors/retier` ranks active connectors by trailing 90-day
spend and assigns tiers: top 10% Tier 1, next 30% Tier 2, remainder Tier 3.
Connectors with zero or negative trailing spend are always Tier 3 regardless of
rank, so a dormant account never occupies a Tier 1 slot in a small portfolio.

Tier drives the breakdown set and the restatement window
(`DotNetMonoRepoTemplate.Ingestion.Tiering.TierPolicy`), which is how extraction
cost is kept proportional to where the spend actually is. Intended to run
monthly.

## Security parity

This service follows the same hardening the other backend services got in the
security audit: forwarded-headers handling with `KnownProxies`/`KnownIPNetworks`
cleared (correct behind Traefik/Coolify), HTTPS redirection, the shared security
headers middleware, and a fixed-time API key comparison. If a later audit changes
that baseline elsewhere, change it here in the same pass — a service that drifts
off the common pipeline is how a gap gets missed.

## Convention note

The control-plane entities use C# enums persisted through
`HasConversion<string>()`, rather than the bare `string` status columns used by
the older webhook entities. The database representation is identical — readable
strings in Postgres — while the C# side stays strongly typed. Flagged here
because it is a deliberate divergence from the older entities, not an oversight.
