#!/usr/bin/env bash
# Lints the edited TypeScript file with the project's own ESLint config
# (@typescript-eslint/no-explicit-any is "error" there — see eslint.config.mjs
# — with legitimate exceptions like test/mock files already configured "off").
# Replaces a hand-rolled `any` regex that missed generic forms like
# `Promise<any>` / `Record<string, any>` and could false-positive on strings.
# Non-zero exit surfaces the violation back to Claude.

set -euo pipefail

input=$(cat)
file_path=$(echo "$input" | jq -r '.tool_input.file_path // .file_path // empty')

if [[ -z "$file_path" ]]; then
  exit 0
fi

if [[ "$file_path" != *.ts && "$file_path" != *.tsx ]]; then
  exit 0
fi

if [[ "$file_path" == *"/dist/"* || "$file_path" == *"/node_modules/"* ]]; then
  exit 0
fi

if [[ ! -f "$file_path" ]]; then
  exit 0
fi

if ! output=$(pnpm exec eslint "$file_path" 2>&1); then
  echo "ESLint failed on $file_path — fix before continuing:" >&2
  echo "$output" >&2
  exit 2
fi

exit 0
