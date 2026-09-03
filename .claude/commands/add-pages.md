---
description: Add frontend pages for a domain — list page, detail page, form, hooks, and service functions
argument-hint: <domain and target app, e.g. "product management pages in admin-web">
---

Use the frontend subagent to generate complete frontend pages for: $ARGUMENTS

1. API service functions using the existing Axios client
2. React Query hooks for all operations (list, getById, create, update, delete)
3. Zod validation schemas for forms
4. List page with table, pagination, search, filters, loading/error/empty states
5. Detail page showing entity information
6. Form component for create and edit with Zod validation
7. Route registration in App.tsx (admin-web) or app router (customer-web)

Every component must have a loading skeleton, error state with retry, and empty state. Never use `alert()`, `confirm()`, or native HTML validation.
