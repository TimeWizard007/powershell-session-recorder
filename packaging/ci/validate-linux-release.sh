#!/bin/sh
# Check the Ubuntu .deb and Linux .tar.gz built in this checkout.
set -eu

root=$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)
cd "$root"
versions=$(packaging/ci/assert-version-files.sh)
version=$(printf '%s\n' "$versions" | sed -n '1p')
debian=$(printf '%s\n' "$versions" | sed -n '2p')
sha=$(git rev-parse HEAD)

deb="artifacts/powershell-session-recorder_${debian}_amd64.deb"
archive="artifacts/powershell-session-recorder-${version}-linux-x64.tar.gz"
for file in "$deb" "$deb.sha256" "$archive" "$archive.sha256"; do
  if [ ! -f "$file" ]; then
    echo "Missing $file." >&2
    exit 1
  fi
done

(
  cd artifacts
  sha256sum -c "$(basename "$deb").sha256"
  sha256sum -c "$(basename "$archive").sha256"
)

package_version=$(dpkg-deb -f "$deb" Version)
if [ "$package_version" != "$debian" ]; then
  echo "Debian package version is $package_version, expected $debian." >&2
  exit 1
fi

tmp=$(mktemp -d)
cleanup() {
  rm -rf "$tmp"
}
trap cleanup EXIT

dpkg-deb --fsys-tarfile "$deb" | tar -x -C "$tmp" ./opt/powershell-session-recorder/PowerShellSessionRecorder.dll
python3 packaging/ci/assert-informational-version.py \
  "$tmp/opt/powershell-session-recorder/PowerShellSessionRecorder.dll" \
  "$version" \
  "$sha"

archive_dll="$tmp/archive.dll"
tar -xOzf "$archive" "powershell-session-recorder-${version}-linux-x64/app/PowerShellSessionRecorder.dll" > "$archive_dll"
python3 packaging/ci/assert-informational-version.py "$archive_dll" "$version" "$sha"

python3 packaging/ci/assert-packaged-files.py "$deb" "$archive"
echo "Linux release packages match $version ($sha)."
