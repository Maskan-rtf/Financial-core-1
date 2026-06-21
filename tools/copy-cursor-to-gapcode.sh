#!/usr/bin/env bash
# Copy .cursor skills/workflows/memory into .gapcode/ (one-time or after .cursor edits)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

SKILLS=(
  validation mapping logging persistence security repository
  ef cqrs refactor organize-git-commits frontend-developer-docs backend-developer-docs
)

for skill in "${SKILLS[@]}"; do
  src="$REPO_ROOT/.cursor/skills/$skill/SKILL.md"
  dest_dir="$REPO_ROOT/.gapcode/skills/$skill"
  if [[ -f "$src" ]]; then
    mkdir -p "$dest_dir"
    cp "$src" "$dest_dir/SKILL.md"
    echo "copied: $skill"
  fi
done

mkdir -p "$REPO_ROOT/.gapcode/workflows" "$REPO_ROOT/.gapcode/memory"
cp "$REPO_ROOT/.cursor/workflows/"*.md "$REPO_ROOT/.gapcode/workflows/"
cp "$REPO_ROOT/.cursor/memory/engineering-memory.md" "$REPO_ROOT/.gapcode/memory/"

# Mirror new frontend skills back to .cursor
for skill in frontend-api frontend-ui frontend-workflow; do
  src="$REPO_ROOT/.gapcode/skills/$skill/SKILL.md"
  dest_dir="$REPO_ROOT/.cursor/skills/$skill"
  if [[ -f "$src" ]]; then
    mkdir -p "$dest_dir"
    cp "$src" "$dest_dir/SKILL.md"
    echo "mirrored to .cursor: $skill"
  fi
done

echo "Done. Run: bash tools/sync-gapcode.sh"
