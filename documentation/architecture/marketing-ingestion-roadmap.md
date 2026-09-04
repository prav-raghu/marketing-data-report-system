# Marketing Data Platform — Delivery Roadmap and Decisions

Status: proposed
Last reviewed: 2026-09-03
Related: [Platform Architecture](./marketing-data-platform.md), [Canonical Data Model](./marketing-canonical-data-model.md)

## 1. Sequencing principle

Build one source end to end before building the second. A thin vertical slice
that reaches the marketing manager's screen exposes every integration problem in
the stack — auth, landing, normalisation, quality gates, serving, embedding —
while there is still only one connector to change. Building five connectors
first and only then discovering that the attribution model is wrong means
rewriting five connectors.

TikTok is the recommended first slice: a single modern JSON API, no
`publisher_platform` splitting, and a reporting model simple enough that the
canonical schema gets tested without the Meta `actions[]` array complicating the
first pass.

## 2. Phases

### Phase 0 — Foundations

| Deliverable | Notes |
|---|---|
| Terraform for storage account, Key Vault, Data Factory, Databricks workspace | `infrastructure/terraform/`, environment-scoped tfvars |
| ADLS Gen2 container and zone layout | Hierarchical namespace on, lifecycle rules configured |
| Unity Catalog, catalogs and access groups | Raw and bronze restricted, silver for analysts, gold for business |
| `ingestion-api` service scaffold | Port 4007, health checks, Serilog, OpenTelemetry, Dockerfile |
| `DotNetMonoRepoTemplate.Ingestion` shared library | Envelope types, `ISourceConnector`, contract validation |
| ADLS Gen2 provider in the Storage library | Alongside the existing Azure Blob provider |
| Control-plane entities and migrations | Per Section 4.5 of the architecture document |

Exit criteria: a hand-written test envelope can be landed in `raw/` by the
service and read back by a Databricks notebook.

**Implementation status.** The application half of Phase 0 is now in the
repository: the `DotNetMonoRepoTemplate.Ingestion` library (envelope, canonical
payload hashing, idempotency keys, raw-zone paths and the gzipped NDJSON writer),
the ten control-plane entities and their EF Core configuration, and the
`ingestion-api` service with its run lifecycle, connector registry and account
tiering, plus a unit test suite. See
[`apps/backend/ingestion-api/README.md`](../../apps/backend/ingestion-api/README.md).

The build pass is done: `dotnet build DotNetMonoRepoTemplate.sln` succeeds with
0 errors on .NET SDK 10.0.400 and all 49 `IngestionApi.Tests` pass, alongside the
rest of the solution's suites.

One thing is outstanding before this phase can be called done: the Terraform for
the Azure resources. Beyond that, compiling is not running — the EF Core mapping
has only been exercised against the in-memory provider, so a migration against
real Postgres is the next thing that can actually invalidate this code.

### Phase 1 — First vertical slice (TikTok to dashboard)

| Deliverable | Notes |
|---|---|
| TikTok connector | OAuth refresh, async report jobs, cursor pagination, Redis rate limiting |
| Auto Loader raw-to-bronze stream | File notification mode |
| Bronze-to-silver normalisation for TikTok | Currency, timezone, attribution, breakdown hashing |
| `dim_date`, `dim_platform`, `dim_account`, `dim_campaign` | Type 2 loader for campaign |
| Silver-to-gold aggregation | Both the full fact and the small rollup |
| Reconciliation check against TikTok's own summary endpoint | The first real quality gate |
| Data Factory pipeline | Trigger, poll, dependency chain, failure alerting |
| `admin-web` landing dashboard | Razor components in Blazor WASM Standalone, reading the rollup through the `/analytics` endpoint |
| Cold-load measurement for `admin-web` | Measure first-visit time to paint with the runtime uncached. Decides whether D6 holds or the screen moves to a server-rendered host |

Exit criteria: yesterday's TikTok spend on the dashboard matches the TikTok Ads
Manager UI to within 0.5%, a deliberate replay of the last seven days produces
byte-identical gold output, and actual daily row counts and API call volumes are
reported back to validate the Section 9 volume model against reality.

That reconciliation is the real acceptance test for the whole phase. Until the
number matches the vendor's own screen, nothing downstream is trustworthy.

### Phase 2 — Meta (Facebook and Instagram)

| Deliverable | Notes |
|---|---|
| Meta connector | Async insights jobs, `publisher_platform` breakdown, `actions[]` normalisation |
| Conversion action type configuration | Which action types count as a conversion, per account, business-owned |
| Attribution window alignment across TikTok and Meta | The first genuine cross-platform comparability work |
| Cross-platform comparability rules in the semantic layer | Metric definition table populated and enforced |
| Vendor rate limit spike | Verify current published limits per vendor and compute the throughput budget. Must precede connector sizing, not follow it |
| Account tiering implementation | Trailing 90-day spend tiers on `SourceConnector`, monthly recalculation. Phase 2 is where account volume makes this necessary |
| Dashboard extended to multi-platform ranking | Dual ROAS and CPA ranking with per-row metric labelling, spend-weighted, minimum-spend floors |

Exit criteria: a cross-platform "best performing" ranking that a marketing
analyst can defend line by line, and a measured throughput budget showing at
least 3x headroom against every vendor limit.

The throughput spike is deliberately placed before the connector is built rather
than after it disappoints. At 400 accounts a Meta connector sized on optimism
rather than arithmetic will not finish inside the extraction window, and that is
discovered at 06:00 on a Monday.

### Phase 3 — Partner JSON APIs and file drops

| Deliverable | Notes |
|---|---|
| Generic configurable REST connector | Auth strategy, pagination strategy and field mapping as configuration rather than code |
| SFTP and blob file-drop ingestion | Data Factory owns this path; CSV and XLSX into the same envelope |
| Admin upload surface for offline spend | Agency workbooks, print and radio bookings |

The generic connector is worth building once there are three or more similar
JSON sources, and not before. Building a configuration-driven framework for a
single source is overengineering; building a fourth bespoke connector by hand is
duplication. Three is the threshold.

### Phase 4 — Legacy XML and SOAP

| Deliverable | Notes |
|---|---|
| Streaming XML connector | `XmlReader`, XSD validation where available, raw document retention |
| Contract definitions for each legacy source | Derived from observed documents where no XSD exists |
| Explicit null-versus-zero handling | Per the canonical model, section 7.3 |
| Backfill of historical legacy data | Separate pipeline and concurrency budget |

Legacy sources come after the modern ones deliberately. They are the lowest
volume and the highest per-source effort, and by this phase the canonical model
has been proven against three source families, so their many exceptions land
against a stable target rather than a moving one.

### Phase 5 — Depth and hardening

| Deliverable | Notes |
|---|---|
| Embedded Power BI surface in `admin-web` | Service principal, server-minted embed tokens |
| Creative-level and placement-level analysis | The gold weekly creative aggregate |
| Anomaly detection on spend and performance | Alerting into the existing observability stack |
| Budget pacing intake path | `dim_budget_plan` plus an `admin-web` plan-workbook upload. Built regardless, so the model has somewhere to read from |
| Budget pacing model | Conditional on the business supplying plan data. If it has not arrived, this drops from scope and nothing else is affected |
| Cost review and cluster right-sizing | After real workload shapes are known, not before |
| Full disaster-recovery replay rehearsal | Rebuild gold from raw end to end and diff |

## 3. Decisions taken

| # | Decision | Rationale | Alternative rejected |
|---|---|---|---|
| D1 | Extraction in .NET, orchestration in Data Factory | Vendor APIs need OAuth refresh, async report jobs and shared rate limiting that Copy activity handles poorly. Data Factory keeps scheduling, dependencies and Databricks invocation | All-Data-Factory extraction — brittle and untestable for these sources |
| D2 | ELT with a thin edge transform | Business logic stays re-runnable against landed history; connectors stay small | Full ETL at the edge, which makes every logic fix a redeploy and a re-pull |
| D3 | Medallion layout on ADLS Gen2 | Raw immutability gives unlimited replay; established pattern with first-class Databricks support | Loading vendor payloads straight into the warehouse, which loses replay |
| D4 | Delta MERGE on a rolling 28-day window as the standard load | Platforms restate. Append-only drifts from vendor reporting within a week | Append-only with periodic full reload, which is expensive and still wrong between reloads |
| D5 | Lakehouse serving over a dedicated SQL pool | At 20–30 GB the second copy buys nothing and costs a class of consistency incidents | Synapse dedicated pool, kept as Option B if licensing mandates it |
| D6 | In-app landing screen plus embedded Power BI for depth | On-brand, licence-free daily screen; full exploration where it is genuinely needed | Power BI only, which is slow and off-brand for the daily screen; in-app only, which blocks analysts |
| D7 | Attribution window as a fact dimension | Makes mismatched windows visible instead of silently blending them | Ignoring attribution, the most common source of confidently wrong cross-platform comparisons |
| D8 | Instagram as a Meta breakdown, not a separate connector | One request returns both; a separate connector doubles the rate-limit cost | Separate Instagram connector |
| D9 | Batch and micro-batch only, no Event Hubs initially | Every in-scope source is batch by nature and provisional for days | Streaming ingestion built ahead of a source that needs it |
| D10 | Azure South Africa North | POPIA residency and local latency | West Europe, retained as a fallback if a required SKU is unavailable in-region |
| D11 | Derived rates computed at query time | Stored averages cannot be re-aggregated correctly | Storing CTR, CPC and ROAS per row |

## 4. Questions resolved

All ten questions raised in the first draft are now closed. Four were answered by
the business; six were resolved by engineering judgement against the confirmed
context. The reasoning is recorded here so a future reader can tell which
decisions were mandated and which were chosen, and reopen the right ones if
circumstances change.

### Answered by the business

| # | Question | Answer | Consequence |
|---|---|---|---|
| Q1 | Existing Synapse or Fabric capacity? | **None held** | D5 confirmed. Lakehouse serving from gold Delta via Databricks SQL, with Synapse Serverless available for T-SQL consumers at no second copy. Later Fabric acquisition is a serving-layer swap, not a re-platform |
| Q4 | ROAS or CPA as the ranking metric? | **ROAS where conversion value exists, CPA elsewhere** | Dashboard ranks on both, with every row labelled by which metric ranked it. The two are never interleaved unlabelled — see D12 |
| Q7 | Lead ads or custom audiences in scope? | **Out of scope** | POPIA posture stays light. No restricted PII zone, no lawful-basis machinery, no processor agreement review in Phase 0. Treated as a boundary to defend, not an accident of sequencing |
| Q9 | How many ad accounts? | **Over 200** | The largest single change to the plan. Volume model revised to 400 accounts, 292M rows per year untiered, 876M over retention. Extraction throughput becomes the binding constraint and account tiering becomes a first-class design element rather than an optimisation — see D13 and D14 |

### Resolved by engineering judgement

| # | Question | Resolution | Rationale |
|---|---|---|---|
| Q2 | Which action types count as a conversion? | Purchase, lead, complete registration. Initiate checkout, add-to-cart and view events excluded. App install configurable per account. Offline and store-visit conversions excluded entirely | Intent signals are not outcomes; including them roughly triples conversion counts and flatters CPA. Offline conversions restate far beyond the 28-day window and would make it meaningless. Stored in `ConversionActionMapping` with audited per-account overrides |
| Q3 | What attribution window is standard? | 7-day click, 1-day view | Matches both major platforms' current defaults, so the dashboard reconciles against the UI the marketing team already uses. Requestable from both without an extra report pull, so alignment costs no additional API quota — which matters at 400 accounts |
| Q5 | Retention per zone? | Raw 3 years tiered to archive, bronze 13 months, silver 3 years, gold 5 years, quarantine 1 year | Raw bounds recoverability and is the only true system of record. Bronze is derived and rebuildable, so short retention there is nearly free. Gold is small enough that 5 years costs almost nothing |
| Q6 | Where do Databricks notebooks live? | A separate repository | Different CI (pytest and a job runner, not `dotnet build`), much faster release cadence, and Databricks Asset Bundles expect to own a repository root. Coupled to this repository only through the versioned envelope contract, validated in the transform repo's CI |
| Q8 | Is plan or budget data available for pacing? | Not yet. Pacing deferred to Phase 5, but the intake path is designed now | `dim_budget_plan` and an `admin-web` plan-workbook upload are specified in Phase 5. Designing the intake now costs little and avoids a schema change later; building the pacing model against data that does not exist would be speculative |
| Q10 | Is one reporting timezone correct? | `Africa/Johannesburg` is canonical, with an `account_local_date` view alongside | A single timezone is right for consolidated reporting. At 200-plus accounts the portfolio is likely multi-market, so per-account operational views need the local day boundary. Both are exposed, named distinctly, and never mixed in one visual |

### Decisions added as a result

| # | Decision | Rationale |
|---|---|---|
| D12 | Dual ranking metric with mandatory per-row labelling | A campaign ranked on CPA and one ranked on ROAS are not comparable. Presenting them in one unlabelled sorted list implies they are, which is precisely the confidently-wrong output this platform exists to avoid |
| D13 | Account tiering by trailing 90-day spend, recalculated monthly | Spend distribution is long-tailed. Treating a R2,000-per-month account like a R2,000,000-per-month one misallocates API quota. Tiering cuts steady-state volume roughly 4.5x while retaining full depth where the spend is |
| D14 | Size the platform for the untiered ceiling, operate at the tiered figure | Sizing for the tiered number guarantees a re-architecture the first time someone enables full breakdowns portfolio-wide for a quarterly review |
| D15 | Minimum 3x headroom against every vendor rate limit | A pipeline at 90% of its limit cannot absorb a retry storm, a backfill, or the vendor tightening the limit without notice |
| D16 | Conversion action set governed centrally, overrides audited | A silent change to what counts as a conversion moves every efficiency metric on the dashboard with no visible cause |
| D17 | Transform code in a separate repository, coupled by a versioned envelope contract | Contract validation in the transform repository's CI means a breaking envelope change fails a build rather than failing silently at 02:00 |
| D18 | The landing screen is Blazor WebAssembly in `admin-web`, with the three-second target stated as warm-load | The repository moved off React to Blazor WASM Standalone, which has no server prerender, so first paint carries a runtime download. That cost belongs to the hosting model, not the dashboard. A manager opening this daily runs warm, so the target holds for the real usage pattern — but it must be measured in Phase 1 rather than assumed |
| D19 | Charts via JS interop rather than a native Blazor charting primitive | Blazor has none. A thin interop wrapper over a JavaScript chart library, or server-rendered SVG for static tiles, is the realistic option. Budgeted in Phase 1 rather than discovered when the first chart is due |

## 5. Remaining dependencies

Nothing is blocked, but three items sit outside engineering's control and should
be tracked as dependencies rather than forgotten:

| Dependency | Needed by | Owner | If it does not arrive |
|---|---|---|---|
| Business confirmation of the conversion action set | Phase 2 exit | Marketing | The governed default in Q2 stands. It is a sensible set, but it should be confirmed rather than assumed, since every efficiency metric depends on it |
| Plan and budget data for pacing | Phase 5 | Marketing / Finance | Pacing is dropped from Phase 5 scope. Nothing else is affected; the intake path stays built and unused |
| Vendor rate limits verified against current documentation | Phase 2 | Data engineering | Throughput budget cannot be validated. This is a spike, not a blocker, and must happen before the Meta connector is sized |

## 6. What this plan does not cover

Stated explicitly so the gaps are choices rather than oversights:

- **Marketing mix modelling and incrementality.** This platform delivers
  platform-reported attribution. It does not measure true incremental lift,
  which needs holdout experiments and a different modelling discipline. The
  dashboard should not be described as measuring causal impact.
- **Cross-device and cross-platform user deduplication.** Not possible from
  platform-reported aggregates. Any "total unique reach" across platforms would
  be an invention.
- **Real-time bidding or campaign activation.** Read-only platform. Writing
  budgets or bids back to vendor APIs is a materially different risk profile and
  needs its own design.
- **Web and app analytics integration.** UTM parameters are parsed into
  `dim_creative`, which leaves the join to a site-analytics source available, but
  that source is not in scope here.
