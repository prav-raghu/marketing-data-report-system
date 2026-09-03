#!/usr/bin/env bash
# Reads the tool input JSON from stdin, finds the nearest package.json
# to the edited file, and runs its typecheck script if one exists.
# Non-zero exit + stderr output surfaces the error back to Claude.

set -euo pipefail

input=$(cat)
file_path=$(echo "$input" | jq -r '.tool_input.file_path // .file_path // empty')

if [[ -z "$file_path" ]]; then
  exit 0
fi

if [[ "$file_path" != *.ts && "$file_path" != *.tsx ]]; then
  exit 0
fi

# Skip generated files and test config
if [[ "$file_path" == *"/dist/"* || "$file_path" == *"/node_modules/"* || "$file_path" == *"jest.config.ts" ]]; then
  exit 0
fi

dir=$(dirname "$file_path")
package_dir=""
while [[ "$dir" != "/" && "$dir" != "." ]]; do
  if [[ -f "$dir/package.json" ]]; then
    package_dir="$dir"
    break
  fi
  dir=$(dirname "$dir")
done

if [[ -z "$package_dir" ]]; then
  exit 0
fi

if ! grep -q '"typecheck"' "$package_dir/package.json" 2>/dev/null; then
  exit 0
fi

cd "$package_dir"
if ! output=$(pnpm typecheck 2>&1); then
  echo "Typecheck failed in $package_dir:" >&2
  echo "$output" >&2
  exit 2
fi

exit 0
