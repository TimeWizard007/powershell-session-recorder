#!/bin/sh
# Build a self-contained linux-x64 tar.gz for per-user installation.
# This does not install the archive.
set -eu

root=$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)
cd "$root"
csproj="$root/src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj"
informational=$(sed -n 's:.*<InformationalVersion>\([^<]*\)</InformationalVersion>.*:\1:p' "$csproj" | head -n 1)
informational=${informational%%+*}
if [ -z "$informational" ]; then
  echo "InformationalVersion was not found in the application project." >&2
  exit 1
fi

name="powershell-session-recorder-${informational}-linux-x64"
filename="${name}.tar.gz"
stage=$(mktemp -d)
cleanup() {
  rm -rf "$stage"
}
trap cleanup EXIT

publish="$stage/publish"
payload="$stage/$name"
mkdir -p "$publish" "$payload/app" "$payload/icons"

revision_args=""
if [ -n "${SOURCE_REVISION_ID:-}" ]; then
  revision_args="-p:SourceRevisionId=${SOURCE_REVISION_ID} -p:ContinuousIntegrationBuild=true"
fi
# shellcheck disable=SC2086
dotnet publish "$csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o "$publish" \
  $revision_args

cp -a "$publish/." "$payload/app/"
rm -f "$payload/app/"*.pdb
. "$root/packaging/reject-user-data.sh"
reject_user_data "$payload/app"
cp -a "$root/packaging/ubuntu/icons/hicolor" "$payload/icons/hicolor"
cp "$root/packaging/linux/install-user.sh" \
  "$root/packaging/linux/uninstall-user.sh" \
  "$root/packaging/linux/user-install-lib.sh" \
  "$root/packaging/linux/INSTALL.txt" \
  "$payload/"
printf '%s\n' "$informational" > "$payload/VERSION"
chmod 755 "$payload/install-user.sh" "$payload/uninstall-user.sh" "$payload/user-install-lib.sh"
chmod 755 "$payload/app/PowerShellSessionRecorder"

if find "$payload" -type l -print -quit | grep -q .; then
  echo "The payload contains a symbolic link." >&2
  exit 1
fi

mkdir -p "$root/artifacts"
tar --sort=name --owner=0 --group=0 --numeric-owner -C "$stage" -czf "$root/artifacts/$filename" "$name"
(
  cd "$root/artifacts"
  sha256sum "$filename" > "$filename.sha256"
  sha256sum -c "$filename.sha256"
)
echo "$root/artifacts/$filename"
echo "$root/artifacts/$filename.sha256"
