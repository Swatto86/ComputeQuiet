#!/usr/bin/env bash
# Create ComputeQuiet on Cursor Origin and push the current branch.
# Requires: origin CLI + CURSOR_API_KEY or CURSOR_AUTH_TOKEN (cloud) / `origin auth login`.
set -euo pipefail
ORIGIN_BIN="${ORIGIN_BIN:-$(command -v origin || true)}"
ORIGIN_BIN="${ORIGIN_BIN:-/exec-daemon/tools/origin}"
REPO_NAME="${1:-ComputeQuiet}"
ROOT="$(cd "$(dirname "$0")" && pwd)"

if [ -n "${CURSOR_API_KEY:-}" ]; then
  "$ORIGIN_BIN" auth login --api-key "$CURSOR_API_KEY" --local
elif [ -n "${CURSOR_AUTH_TOKEN:-}" ]; then
  export CURSOR_AUTH_TOKEN
else
  echo "Set CURSOR_API_KEY or CURSOR_AUTH_TOKEN, or run: origin auth login" >&2
  exit 1
fi

"$ORIGIN_BIN" auth status
"$ORIGIN_BIN" repo create "$REPO_NAME" --default-branch main || true

# Prefer pushing from the ComputeQuiet project directory as repo root content.
cd "$ROOT"
# If we are still nested under /agent, push the ComputeQuiet tree by using this folder.
if [ ! -d .git ]; then
  git init
  git checkout -b main
  git add -A
  git -c user.email="${GIT_AUTHOR_EMAIL:-cursor@swatto.co.uk}" \
      -c user.name="${GIT_AUTHOR_NAME:-Swatto}" \
      commit -m "Initial ComputeQuiet" || true
fi

OWNER="$("$ORIGIN_BIN" api user 2>/dev/null | python3 -c "import sys,json; print(json.load(sys.stdin).get('login') or json.load(open('/dev/stdin')).get('username',''))" 2>/dev/null || true)"
# Fallback: parse from repo list
if [ -z "${OWNER:-}" ]; then
  OWNER="$("$ORIGIN_BIN" repo list 2>/dev/null | head -1 | cut -d/ -f1 || true)"
fi
if [ -z "${OWNER:-}" ]; then
  echo "Could not resolve Origin owner namespace; create the remote manually." >&2
  exit 1
fi

REMOTE="https://origin.cursor.com/${OWNER}/${REPO_NAME}.git"
git remote remove origin 2>/dev/null || true
git remote add origin "$REMOTE"
"$ORIGIN_BIN" auth setup-git --local || true
git push -u origin HEAD:main
echo "Pushed to $REMOTE"
