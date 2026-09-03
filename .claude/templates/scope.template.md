# Project Scope: [PROJECT NAME]

> **Claude Code Entry Point**
> Hand this document to the **full-stack-orchestrator** agent:
> `.claude/agents/full-stack-orchestrator.md`
>
> Before writing a single line of code, read:
> 1. `CLAUDE.md`
> 2. `.claude/agents/full-stack-orchestrator.md`
>
> This document is the single source of truth for Phase 1 scope.
> Do **not** build beyond what is described here.

---

## 1. Project Overview

| Field | Value |
| --- | --- |
| **Project Name** | `[project-name]` _(lowercase, hyphenated)_ |
| **Package Scope** | `@[project-name]/` |
| **Phase** | Phase 1 — MVP |
| **Target Env** | Development → QA → Production |
| **Repo Base** | `node-mono-repo-template` |

### 1.1 Business Summary

> _2–4 sentences. What does this system do? Who uses it? What problem does it solve?_
>
> Example: "A fleet tracking platform for logistics companies. Drivers submit telemetry via mobile.
> Dispatchers monitor vehicles on a real-time dashboard. Admins manage routes, drivers, and reports."

---

## 2. Design Screens

> Attach or link design files below. Claude Code will use these as the UI contract
> for all frontend pages listed in Section 7.

| Screen Name | File / Link | Notes |
| --- | --- | --- |
| Login / Auth | `designs/auth.png` or [Figma link] | |
| Dashboard | `designs/dashboard.png` | |
| [Entity] List View | `designs/[entity]-list.png` | |
| [Entity] Detail / Form | `designs/[entity]-detail.png` | |
| _(add more as needed)_ | | |

> If a design screen exists for a page, Claude Code must implement it
> faithfully using the existing component library. No creative divergence.

---

## 3. Monorepo Name Substitution

> Run this before writing any code. Replace everywhere in the repo:

| Find | Replace With |
| --- | --- |
| `node-mono-repo-template` | `[project-name]` |
| `@node-mono-repo-template/` | `@[project-name]/` |

Locations to update: all `package.json` files, `tsconfig.base.json`, `pnpm-workspace.yaml`, `docker-compose.yaml`, `CLAUDE.md`, all `.claude/agents/*.md`, all `.claude/commands/*.md`, all `.claude/instructions/*.md`.

---

## 4. Domain Entities

> Define every entity the system manages. Claude Code will use this to drive
> the `domain-modeler` agent and Prisma schema design.

### 4.1 Entity: `[EntityName]`

| Property | Type | Required | Notes |
| --- | --- | --- | --- |
| `id` | `String` (uuid) | Yes | PK, auto-generated |
| `name` | `String` | Yes | |
| `status` | `Enum` | Yes | Values: `ACTIVE`, `INACTIVE` |
| `tenant_id` | `String` | Yes | FK → `Tenant` |
| `created_at` | `DateTime` | Yes | Auto |
| `updated_at` | `DateTime` | Yes | Auto |
| _(add fields)_ | | | |

**Relationships:**
- `[EntityName]` belongs to `[OtherEntity]` (many-to-one)
- `[EntityName]` has many `[ChildEntity]` (one-to-many)

**Indexes:** `[tenant_id, status]`, `[created_at DESC, id]`

**Caching:** TTL `[X]` minutes | Cache key: `[entity]:[id]` | Invalidate on: write

### 4.2 Entity: `[EntityName2]`

> _(repeat block per entity)_

---

## 5. Enums

```
[EnumName]:
  - VALUE_ONE    → description
  - VALUE_TWO    → description
  - VALUE_THREE  → description
```

---

## 6. RBAC — Roles & Permissions

> Claude Code will use `.claude/agents/rbac.md` for implementation.

| Role | Description |
| --- | --- |
| `SUPER_ADMIN` | Full system access |
| `MODERATOR` | _(describe scope)_ |
| `SUPPORT` | _(describe scope)_ |
| `CHAT_USER` | _(describe scope)_ |
| _(add roles)_ | |

### Permission Matrix

| Resource | SUPER_ADMIN | MODERATOR | SUPPORT | CHAT_USER |
| --- | :---: | :---: | :---: | :---: |
| `[entity]:read` | Yes | Yes | Yes | Yes |
| `[entity]:write` | Yes | Yes | Yes | No |
| `[entity]:delete` | Yes | Yes | No | No |
| _(add more)_ | | | | |

---

## 7. Backend Services & API Endpoints

> Services live in `apps/backend/`. Claude Code will use `.claude/agents/api-builder.md`.
>
> All endpoints follow the standard response envelope: `{ isSuccessful, data?, message? }`.
> New services use `.claude/agents/new-service-scaffold.md`.

### 7.1 Service: `[service-name]-api`

**Base path:** `/api/v1/[resource]`

| Method | Path | Auth | Role Required | Description |
| --- | --- | --- | --- | --- |
| `POST` | `/[resource]` | JWT | `SUPER_ADMIN` | Create [entity] |
| `GET` | `/[resource]` | JWT | `CHAT_USER` | List [entities] (paginated) |
| `GET` | `/[resource]/:id` | JWT | `CHAT_USER` | Get single [entity] |
| `PUT` | `/[resource]/:id` | JWT | `SUPER_ADMIN` | Update [entity] |
| `DELETE` | `/[resource]/:id` | JWT | `SUPER_ADMIN` | Soft-delete [entity] |
| _(add)_ | | | | |

**Pagination:** Cursor-based (default) unless dataset is known-small

#### Caching Notes

- `GET /[resource]` → Cache list with key `[resource]:list:[tenantId]`, TTL `5m`
- `GET /[resource]/:id` → Cache item with key `[resource]:[id]`, TTL `10m`
- Invalidate list cache on any write

### 7.2 Service: `[service-name-2]-api`

> _(repeat block per service)_

---

## 8. Queue Jobs (BullMQ)

> Claude Code will use `common/queue` package.
> Reference: `.claude/agents/backend-service.md`

| Queue Name | Trigger | Job Description | Priority |
| --- | --- | --- | --- |
| `[queue-name]` | POST `/[resource]` | Send confirmation email | Normal |
| `[queue-name-2]` | CRON `0 8 * * *` | Generate daily report | Low |
| _(add jobs)_ | | | |

---

## 9. Frontend Applications

### 9.1 Admin Web (React + Vite)

> Claude Code will use `.claude/agents/frontend-react.md` and `.claude/agents/frontend-page-builder.md`.

| Page | Route | Description |
| --- | --- | --- |
| Dashboard | `/dashboard` | Summary metrics |
| [Entity] List | `/[entities]` | Table with pagination, search, filters |
| [Entity] Create | `/[entities]/new` | Form with validation |
| [Entity] Edit | `/[entities]/:id/edit` | Pre-populated form |
| _(add pages)_ | | |

### 9.2 Customer Web (Next.js)

> Claude Code will use `.claude/agents/frontend-nextjs.md`.

| Page | Route | SSR | Description |
| --- | --- | --- | --- |
| Home | `/` | Yes | Landing page with SEO |
| [Entity] Catalog | `/[entities]` | Yes | Public listing |
| [Entity] Detail | `/[entities]/[slug]` | Yes | Detail with SEO metadata |
| _(add pages)_ | | | |

### 9.3 Mobile (Ionic + Capacitor)

> Claude Code will use `.claude/agents/mobile.md`.

| Screen | Path | Description |
| --- | --- | --- |
| Home | `/home` | |
| _(add screens)_ | | |

---

## 10. Webhooks & Events

> If events are needed, use `.claude/agents/webhook-events.md`.

| Event | Trigger | Subscribers |
| --- | --- | --- |
| `[entity].created` | POST /[resource] | External CRM |
| _(add events)_ | | |

---

## 11. Feature Flags

> Use `.claude/agents/feature-flags.md` for implementation.

| Flag Key | Default | Description |
| --- | --- | --- |
| `[flag-name]` | disabled | _(what it gates)_ |
| _(add flags)_ | | |

---

## 12. Infrastructure

> Use `.claude/agents/vps-bootstrap.md` first, then `.claude/agents/deployment-coolify.md`.
> For cloud-managed (AWS/Azure/GCP), use `.claude/agents/infrastructure.md`.

| Component | Provider | Notes |
| --- | --- | --- |
| VPS | Hetzner CX32 | Ubuntu 24.04 |
| PaaS | Coolify | Self-hosted |
| DNS | Cloudflare | Grey-cloud for Traefik |
| Database | Coolify-managed Postgres | |
| Cache | Coolify-managed Redis | |
| Storage | Azure Blob / Cloudflare R2 | |

**Domains:**
- Customer web: `[apex-domain]`
- Admin web: `admin.[apex-domain]`
- API: `api.[apex-domain]`
- Admin API (internal): Tailscale only

---

## 13. Testing

> Use `.claude/agents/testing.md`.

- Unit tests required for all service methods
- Integration tests required for all route groups
- Coverage thresholds: branches 75%, functions 80%, lines 80%
- Test DB: `TEST_DATABASE_URL` separate from dev DB

---

## 14. Environment Variables

List every env var this project needs across all services. This drives `.env.example` generation.

| Variable | Service | Description |
| --- | --- | --- |
| `DATABASE_URL` | all backend | PostgreSQL connection string |
| `REDIS_URL` | all backend | Redis connection string |
| `JWT_SECRET` | api-gateway | Min 32 chars |
| _(add vars)_ | | |
