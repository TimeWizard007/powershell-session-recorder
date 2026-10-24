#!/bin/sh
# Verify every SHA-256 file below a directory.
set -eu

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "Usage: verify-checksum-files.sh <directory> [minimum-count]" >&2
  exit 1
fi

root=$1
minimum=${2:-1}
list=$(mktemp)
cleanup() {
  rm -f "$list"
}
trap cleanup EXIT
find "$root" -type f -name '*.sha256' | sort > "$list"
count=$(wc -l < "$list" | tr -d ' ')
if [ "$count" -lt "$minimum" ]; then
  echo "Expected at least $minimum checksum files in $root, found $count." >&2
  exit 1
fi

while IFS= read -r sum; do
  [ -n "$sum" ] || continue
  (
    cd "$(dirname "$sum")"
    name=$(basename "$sum")
    if command -v sha256sum >/dev/null 2>&1; then
      sha256sum -c "$name"
    else
      shasum -a 256 -c "$name"
    fi
  ) || exit 1
done < "$list"
