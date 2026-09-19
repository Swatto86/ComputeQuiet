#!/usr/bin/env bash
# A release tag must name the version the manifests carry: `v1.2.3` for 1.2.3.
set -euo pipefail
tag="${1:?usage: check-release-tag.sh <tag>}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version=$(sed -n 's/^version = "\(.*\)"/\1/p' "$root/Cargo.toml" | head -1)
if [ "$tag" != "v$version" ]; then
  echo "tag $tag does not match the workspace version $version (expected v$version)"
  exit 1
fi
echo "tag $tag matches version $version"
