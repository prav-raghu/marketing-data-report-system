---
description: Build a complete system from a high-level description — generates database, backend APIs, and frontend UI
argument-hint: <describe the system, e.g. "ecommerce system for burgers with menu, cart, and checkout">
---

Use the full-stack-orchestrator subagent to build: $ARGUMENTS

1. Analyze the description and identify domain entities, relationships, and features
2. Present a plan with tables, endpoints, and pages — wait for confirmation before starting
3. Generate all layers in order: Database → Backend → Frontend
4. Wire everything together (endpoints, services, middleware/DI registration, React Query hooks)
5. Include seed data for lookup and sample data
6. Ensure all CRUD operations work end to end

After completion, list every command needed to run the system.
