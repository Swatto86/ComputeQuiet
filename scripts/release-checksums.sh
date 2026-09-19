#!/usr/bin/env bash
# SHA-256 sums for every artifact in a release directory, named per platform
# so the publish job can merge them without collisions.
set -euo pipefail
dir="${1:?usage: release-checksums.sh <dir> <suffix>}"
suffix="${2:?usage: release-checksums.sh <dir> <suffix>}"
cd "$dir"
files=$(find . -maxdepth 1 -type f ! -name 'SHA256SUMS-*.txt' -printf '%P\n' | sort)
[ -n "$files" ] || { echo "no artifacts in $dir"; exit 1; }
# shellcheck disable=SC2086
sha256sum $files > "SHA256SUMS-$suffix.txt"
cat "SHA256SUMS-$suffix.txt"
