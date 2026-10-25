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
packaging/ci/verify-checksum-files.sh "$assets" 7
. packaging/ubuntu/debian-version.sh
debian=$(debian_version_from_informational "$version")

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

required="
powershell-session-recorder_${debian}_amd64.deb
powershell-session-recorder_${debian}_amd64.deb.sha256
powershell-session-recorder-${version}-linux-x64.tar.gz
powershell-session-recorder-${version}-linux-x64.tar.gz.sha256
PowerShellSessionRecorder-${version}-win-x64-setup.exe
PowerShellSessionRecorder-${version}-win-x64-setup.exe.sha256
PowerShellSessionRecorder-${version}-osx-x64.dmg
PowerShellSessionRecorder-${version}-osx-x64.dmg.sha256
PowerShellSessionRecorder-${version}-osx-x64.zip
PowerShellSessionRecorder-${version}-osx-x64.zip.sha256
PowerShellSessionRecorder-${version}-osx-arm64.dmg
PowerShellSessionRecorder-${version}-osx-arm64.dmg.sha256
PowerShellSessionRecorder-${version}-osx-arm64.zip
PowerShellSessionRecorder-${version}-osx-arm64.zip.sha256
"
files=
for name in $required; do
  found=$(find "$assets" -type f -name "$name")
  found_count=$(printf '%s\n' "$found" | grep -c . || true)
  if [ "$found_count" -ne 1 ]; then
    echo "Expected exactly one $name in $assets, found $found_count." >&2
    exit 1
  fi
  files="$files $found"
done

# shellcheck disable=SC2086
gh release create "$tag" \
  --title "PowerShell Session Recorder $version" \
  --notes-file "$notes" \
  --verify-tag \
  $prerelease \
  $files

echo "Created GitHub Release $tag from $expected_sha."
