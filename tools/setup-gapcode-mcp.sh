#!/usr/bin/env bash
# Install MCP servers for GapCode CLI (WSL).
# Secrets live in ~/.gapcode/secrets.env — never committed.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SECRETS_FILE="${GAPCODE_SECRETS:-$HOME/.gapcode/secrets.env}"
EXAMPLE_FILE="$REPO_ROOT/tools/gapcode-secrets.env.example"

echo "==> GapCode MCP setup"
echo "    Secrets: $SECRETS_FILE"

mkdir -p "$HOME/.gapcode"

if [[ ! -f "$SECRETS_FILE" ]]; then
  if [[ -f "$EXAMPLE_FILE" ]]; then
    cp "$EXAMPLE_FILE" "$SECRETS_FILE"
    chmod 600 "$SECRETS_FILE"
    echo "    + created $SECRETS_FILE from example — edit DB password and GitHub token"
  else
    echo "    ! Missing $SECRETS_FILE — create it first"
    exit 1
  fi
fi

# shellcheck disable=SC1090
source "$SECRETS_FILE"

remove_mcp() {
  gapcode mcp remove "$1" 2>/dev/null || true
}

# --- PostgreSQL (read-only schema + SELECT queries) ---
if [[ -n "${FINANCIAL_CORE_DATABASE_URL:-}" && "$FINANCIAL_CORE_DATABASE_URL" != *"YOUR_PASSWORD"* ]]; then
  remove_mcp postgres
  gapcode mcp add postgres -- \
    npx -y @modelcontextprotocol/server-postgres "$FINANCIAL_CORE_DATABASE_URL"
  echo "    + postgres MCP (FinancialCore on Liara)"
else
  echo "    ! postgres skipped — set FINANCIAL_CORE_DATABASE_URL in $SECRETS_FILE"
fi

# --- GitHub (PRs, issues, repos) ---
if [[ -n "${GITHUB_PERSONAL_ACCESS_TOKEN:-}" ]]; then
  remove_mcp github
  gapcode mcp add github \
    --env "GITHUB_PERSONAL_ACCESS_TOKEN=$GITHUB_PERSONAL_ACCESS_TOKEN" -- \
    npx -y @modelcontextprotocol/server-github
  echo "    + github MCP"
else
  echo "    ! github skipped — set GITHUB_PERSONAL_ACCESS_TOKEN in $SECRETS_FILE"
fi

# --- Playwright browser (test Frontend/, browse pages) ---
remove_mcp playwright
gapcode mcp add playwright -- npx -y @playwright/mcp@latest
echo "    + playwright MCP (browser)"

echo ""
echo "Installed MCP servers:"
gapcode mcp list
