#!/usr/bin/env bash
# SHA-256 sums for every artifact in a release directory, named per platform
# so the publish job can merge them without collisions.
#
# Portable across GNU and BSD userlands: macOS `find` has no `-printf` and may
# have no `sha256sum`, which is how the first macOS release build failed after
# the app itself had built fine.
set -euo pipefail
dir="${1:?usage: release-checksums.sh <dir> <suffix>}"
suffix="${2:?usage: release-checksums.sh <dir> <suffix>}"
cd "$dir"
files=$(find . -maxdepth 1 -type f ! -name 'SHA256SUMS-*.txt' | sed 's|^\./||' | sort)
[ -n "$files" ] || { echo "no artifacts in $dir"; exit 1; }
if command -v sha256sum >/dev/null 2>&1; then
  hasher=(sha256sum)
else
  hasher=(shasum -a 256)
fi
# shellcheck disable=SC2086
"${hasher[@]}" $files > "SHA256SUMS-$suffix.txt"
cat "SHA256SUMS-$suffix.txt"
