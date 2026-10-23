#!/bin/sh
# Publish a self-contained Windows x64 build.
# This does not create an installer. Compile packaging/windows/PowerShellSessionRecorder.iss
# on Windows with Inno Setup when an installer is required.
set -eu

root=$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)
cd "$root"
out="$root/artifacts/win-x64"

revision_args=""
if [ -n "${SOURCE_REVISION_ID:-}" ]; then
  revision_args="-p:SourceRevisionId=${SOURCE_REVISION_ID} -p:ContinuousIntegrationBuild=true"
fi
# shellcheck disable=SC2086
dotnet publish "$root/src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o "$out" \
  $revision_args

find "$out" -name '*.pdb' -delete
. "$root/packaging/reject-user-data.sh"
reject_user_data "$out"
echo "$out"
