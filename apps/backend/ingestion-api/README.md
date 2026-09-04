# ingestion-api

Control plane and extraction host for the marketing data ingestion platform. Port **4007**.

Design background: [Marketing Data Platform](../../../documentation/architecture/marketing-data-platform.md),
[Canonical Data Model](../../../documentation/architecture/marketing-canonical-data-model.md),
[Delivery Roadmap](../../../documentation/architecture/marketing-ingestion-roadmap.md).

## Status

This is the **Phase 0 foundation**, replayed onto the post-Blazor-migration
codebase. It contains the control plane, the ingestion
envelope machinery and the connector abstraction. It contains **no source
connectors yet** — TikTok is Phase 1, Meta is Phase 2.

`GET /internal/v1/connectors` will therefore return an empty
`registeredConnectorKeys` array until the first connector is implemented. That is
expected, not a misconfiguration.

**Not yet compiled.** No .NET SDK was reachable in the environment that wrote
this code (the SDK download host is blocked by network policy), so
`dotnet build` has not been run against it. Per the repository rules this code
is not trusted until it has been. Before relying on it, run:

```bash
dotnet build DotNetMonoRepoTemplate.sln
dotnet test apps/backend/ingestion-api/tests/IngestionApi.Tests.csproj
```

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
