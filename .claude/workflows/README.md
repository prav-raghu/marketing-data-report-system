# Workflows

Dynamic workflow scripts live here. Each `.js` file becomes a `/<name>` slash command that Claude Code executes to orchestrate multiple subagents for complex multi-step tasks.

## Convention

- Workflows are written in JavaScript and saved here from `/workflows` in a Claude Code session
- Each file is a `/<name>` command: `deploy.js` → `/deploy`
- Project workflows take precedence over any global `~/.claude/workflows/` file with the same name
- These are NOT hand-authored — run `/workflows` in a Claude Code session to create one

## When to create a workflow vs a skill

| Use a skill | Use a workflow |
|---|---|
| Single focused task with a clear prompt | Orchestrates many subagents in parallel |
| Needs supporting reference files | Complex conditional branching |
| Invoked by you or Claude | Always user-invoked (too risky to auto-invoke) |

## Planned workflows

Add workflow descriptions here as they are created:

| Workflow | Description |
|---|---|
| (none yet) | |
