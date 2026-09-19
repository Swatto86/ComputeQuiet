#!/usr/bin/env bash
# Install the pre-push hook that runs the full gate. A person remembering to
# read an exit code is not a control; a hook is.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
hook="$(git rev-parse --git-path hooks/pre-push)"
mkdir -p "$(dirname "$hook")"

cat > "$hook" <<'HOOK'
#!/usr/bin/env bash
# Runs the full gate before anything reaches the remote. Installed by
# scripts/install-hooks.sh.
set -euo pipefail
root="$(git rev-parse --show-toplevel)"
echo "pre-push: running the full gate"
if ! "$root/scripts/verify.sh"; then
  echo ""
  echo "pre-push: the gate failed, so nothing was pushed."
  exit 1
fi
HOOK

chmod +x "$hook"
echo "installed $hook"
