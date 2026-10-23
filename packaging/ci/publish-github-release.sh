#!/bin/sh
# Create a GitHub Release for an existing tag after checksums and approval checks.
# This does not move a tag, and it does not replace a release that already exists.
set -eu

if [ "$#" -ne 3 ]; then
  echo "Usage: publish-github-release.sh <tag> <sha> <asset-directory>" >&2
  exit 1
fi

tag=$1
expected_sha=$2
assets=$3
version=${tag#v}
root=$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)
cd "$root"

if [ "${RELEASE_APPROVED:-}" != "yes" ]; then
  echo "Set the release environment variable RELEASE_APPROVED to yes before publishing." >&2
  exit 1
fi
if ! printf '%s' "$expected_sha" | grep -Eq '^[0-9a-f]{40}$'; then
  echo "Expected SHA must be 40 lowercase hex characters." >&2
  exit 1
fi

head=$(git rev-parse HEAD)
if [ "$head" != "$expected_sha" ]; then
  echo "Checkout $head is not the approved commit $expected_sha." >&2
  exit 1
fi
git fetch --force origin "refs/tags/$tag:refs/tags/$tag"
tagged=$(git rev-parse "refs/tags/$tag^{commit}")
if [ "$tagged" != "$expected_sha" ]; then
  echo "Tag $tag points at $tagged, not $expected_sha." >&2
  exit 1
fi

packaging/ci/assert-version-files.sh "$version" >/dev/null
packaging/ci/verify-checksum-files.sh "$assets" 3

if gh release view "$tag" >/dev/null 2>&1; then
  echo "Release $tag already exists. Refusing to overwrite it." >&2
  exit 1
fi

notes="docs/en/release-notes-$version.md"
if [ ! -f "$notes" ]; then
  echo "Missing release notes $notes." >&2
  exit 1
fi

prerelease=""
case "$version" in
  *-*) prerelease="--prerelease" ;;
esac

files=$(find "$assets" -type f \( \
  -name '*.deb' -o -name '*.deb.sha256' -o \
  -name '*.tar.gz' -o -name '*.tar.gz.sha256' -o \
  -name '*setup.exe' -o -name '*setup.exe.sha256' \
  \) | sort)
file_count=$(printf '%s\n' "$files" | wc -l | tr -d ' ')
if [ "$file_count" -lt 6 ]; then
  echo "Expected the Debian package, Linux archive, Windows installer, and their checksums." >&2
  exit 1
fi

# shellcheck disable=SC2086
gh release create "$tag" \
  --title "PowerShell Session Recorder $version" \
  --notes-file "$notes" \
  --verify-tag \
  $prerelease \
  $files

echo "Created GitHub Release $tag from $expected_sha."
