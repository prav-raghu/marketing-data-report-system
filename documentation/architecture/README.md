# Architecture

Architecture decisions, diagrams and design records for the monorepo.

## Marketing Data Platform

A proposed ingestion and analytics platform for multi-stream marketing data
(TikTok, Meta, partner JSON APIs, legacy XML/SOAP APIs, file drops) landing in
Azure Data Lake Storage Gen2, orchestrated by Azure Data Factory, transformed in
Azure Databricks and served to a marketing manager dashboard in `admin-web`
(Blazor WebAssembly Standalone, since the repository's move off React).

Read in this order:

1. **[Marketing Data Platform](./marketing-data-platform.md)** — the end-to-end
   architecture: extraction, the ingestion envelope, storage zones, the
   medallion processing layers, serving options, scale engineering, security and
   failure handling.
2. **[Marketing Canonical Data Model](./marketing-canonical-data-model.md)** —
   the target schema every source normalises into: grain, dimensions, the
   normalisation rules for timezone, currency and attribution, metric
   comparability, additivity, and source-to-canonical mappings.
3. **[Delivery Roadmap and Decisions](./marketing-ingestion-roadmap.md)** —
   phased delivery, the seventeen decisions taken with their rationale, how each
   of the ten original open questions was resolved, the remaining external
   dependencies, and explicit non-goals.

Status of all three: **proposed, with scope questions closed**. All ten open
questions from the first draft are resolved in Section 4 of the roadmap — four
answered by the business (no existing warehouse capacity, dual ROAS/CPA ranking,
over 200 ad accounts, personal data out of scope) and six by engineering
judgement.

The confirmed portfolio size of 200-plus accounts is the answer that shaped the
rest: it raises the untiered volume ceiling to roughly 292M rows per year, makes
vendor API throughput rather than storage the binding constraint, and promotes
account tiering from an optimisation to a first-class part of the design.

No implementation exists in the repository yet; Section 12 of the architecture
document lists what would be added and where.
