---
description: Run a full code review on recent changes — checks type safety, naming, security, and project standards
argument-hint: <scope, e.g. "all backend services" or "customer-api auth module">
---

Use the code-review subagent to review: $ARGUMENTS

Report findings as Blockers (must fix), Warnings (should fix), and Suggestions (nice to have). Do not silently fix anything — report first and wait for direction.
