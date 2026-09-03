#!/usr/bin/env bash
# PreToolUse guard on Bash: blocks `git commit` / `git push` while the
# current branch is main/master. `git pull`, `git fetch`, `git status`,
# `git diff`, etc. are unaffected — only commit and push are gated.
# Non-zero exit blocks the tool call and surfaces the message to Claude.

set -euo pipefail

input=$(cat)
command=$(echo "$input" | jq -r '.tool_input.command // .command // empty')

if [[ -z "$command" ]]; then
  exit 0
fi

if [[ ! "$command" =~ git[[:space:]]+(commit|push) ]]; then
  exit 0
fi

branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "")

if [[ "$branch" == "main" || "$branch" == "master" ]]; then
  echo "Blocked: '$command' targets branch '$branch'. Commit and push only on a feature branch — create one first (git checkout -b <branch-name>)." >&2
  exit 2
fi

exit 0
