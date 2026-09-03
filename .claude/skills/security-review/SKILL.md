---
description: Security audit for backend services and API endpoints — authentication gaps, injection risks, hardcoded secrets, and permission bypass vectors.
disable-model-invocation: true
argument-hint: <branch, path, or service name, e.g. "apps/backend/customer-api/src/routes">
---

# Security Review

Audit the changes or files at: $ARGUMENTS

Run this diff first if reviewing a branch:

!`git diff origin/main..HEAD -- $ARGUMENTS 2>/dev/null || find $ARGUMENTS -name "*.ts" | head -20`

## Audit checklist

### Authentication & Authorization

- [ ] JWT validation happens ONLY in `api-gateway`, never in downstream services
- [ ] No route skips auth via query param, header trick, or env flag
- [ ] `isPublic: true` is used sparingly and only for genuinely public endpoints
- [ ] Permission guard fires before route handlers
- [ ] `req.user` is never taken from the request body or query string

### Input Validation

- [ ] Every request body goes through AJV schema validation
- [ ] `additionalProperties: false` on all AJV body schemas
- [ ] No SQL injection vectors (raw queries validated or parameterized)
- [ ] File uploads validate MIME type and size before processing

### Secrets & Credentials

- [ ] No hardcoded secrets, API keys, tokens, or connection strings anywhere
- [ ] No sensitive values in log output (check `SENSITIVE_KEYS` list is complete)
- [ ] `.env` is gitignored; only `.env.example` with placeholders is committed
- [ ] Passwords stored as bcrypt hashes, never plaintext

### Output & Data Exposure

- [ ] Prisma queries use `select` to exclude sensitive fields from responses
- [ ] Audit log `redact()` covers password, token, secret, hash, two_factor_secret
- [ ] Webhook payloads contain no PII, passwords, or internal system IDs
- [ ] No stack traces or internal error messages in production API responses

### Infrastructure

- [ ] No wildcard `*` in CORS `origin` config in production
- [ ] Helmet is registered on every Fastify service
- [ ] Rate limiting is applied per route tier (public: 30/min, auth: 120/min, admin: 300/min)
- [ ] SSRF prevention in place on any service making outbound HTTP calls

## Report format

Group findings as:

**Blockers** (must fix before merge) → **Warnings** (should fix) → **Suggestions** (nice to have)

Each finding: location, issue, concrete fix.
