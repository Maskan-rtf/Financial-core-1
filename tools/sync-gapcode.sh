#!/usr/bin/env bash
# Sync Financial-Core skills and plugin to GapCode CLI (~/.gapcode).
# Run from repo root on WSL: bash tools/sync-gapcode.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CODEX_HOME="${CODEX_HOME:-$HOME/.gapcode}"
SKILLS_SRC="$REPO_ROOT/.gapcode/skills"
PLUGIN_SRC="$REPO_ROOT/.gapcode"
MARKETPLACE_DIR="$HOME/.agents/plugins"
MARKETPLACE_FILE="$MARKETPLACE_DIR/marketplace.json"

echo "==> Financial-Core GapCode sync"
echo "    Repo:       $REPO_ROOT"
echo "    CODEX_HOME: $CODEX_HOME"

mkdir -p "$CODEX_HOME/skills"

# Copy each skill directory from .gapcode/skills to ~/.gapcode/skills
if [[ -d "$SKILLS_SRC" ]]; then
  for skill_dir in "$SKILLS_SRC"/*/; do
    [[ -d "$skill_dir" ]] || continue
    skill_name="$(basename "$skill_dir")"
    dest="$CODEX_HOME/skills/$skill_name"
    rm -rf "$dest"
    cp -r "$skill_dir" "$dest"
    echo "    + skill: $skill_name"
  done
else
  echo "    ! No skills found at $SKILLS_SRC"
fi

# Register plugin in personal marketplace (symlink for correct relative path)
mkdir -p "$MARKETPLACE_DIR"
ln -sfn "$PLUGIN_SRC" "$MARKETPLACE_DIR/financial-core"

if [[ ! -f "$MARKETPLACE_FILE" ]]; then
  cat > "$MARKETPLACE_FILE" <<'EOF'
{
  "name": "personal",
  "interface": {
    "displayName": "Personal Plugins"
  },
  "plugins": []
}
EOF
  echo "    + created marketplace: $MARKETPLACE_FILE"
fi

# Update or add financial-core plugin entry using python for JSON safety
python3 - "$MARKETPLACE_FILE" <<'PY'
import json, sys

marketplace_path = sys.argv[1]

with open(marketplace_path, "r", encoding="utf-8") as f:
    data = json.load(f)

plugins = data.setdefault("plugins", [])
entry = {
    "name": "financial-core",
    "source": {"source": "local", "path": "./financial-core"},
    "policy": {
        "installation": "INSTALLED_BY_DEFAULT",
        "authentication": "ON_USE"
    }
}

found = False
for i, p in enumerate(plugins):
    if p.get("name") == "financial-core":
        plugins[i] = entry
        found = True
        break
if not found:
    plugins.append(entry)

with open(marketplace_path, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2)
    f.write("\n")
print("    + marketplace entry: financial-core -> ./financial-core")
PY

# Try to install plugin (skills are already copied directly above)
if command -v gapcode >/dev/null 2>&1; then
  gapcode plugin add financial-core@personal 2>/dev/null && echo "    + plugin installed" || echo "    = plugin install skipped (skills already in ~/.gapcode/skills/)"
fi

# Ensure config.toml has fallback filenames for project docs
CONFIG_FILE="$CODEX_HOME/config.toml"
if [[ -f "$CONFIG_FILE" ]]; then
  if ! grep -q 'project_doc_fallback_filenames' "$CONFIG_FILE" 2>/dev/null; then
    cat >> "$CONFIG_FILE" <<'EOF'

# Financial-Core: also load .cursor and .gapcode agent docs
project_doc_fallback_filenames = [".gapcode/AGENTS.md", ".cursor/AGENTS.md"]
project_doc_max_bytes = 65536
EOF
    echo "    + updated config.toml with project_doc_fallback_filenames"
  else
    echo "    = config.toml already has project_doc_fallback_filenames"
  fi
fi

echo ""
echo "Done. Skills installed to $CODEX_HOME/skills/"
echo "Run: gapcode plugin list"
echo "Run: gapcode  (from $REPO_ROOT)"
