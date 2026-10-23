#!/bin/sh
# Print the application version and Debian version when the project files agree.
# An optional argument must match the informational version, without build metadata.
set -eu

root=$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)
. "$root/packaging/ubuntu/debian-version.sh"

csproj="$root/src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj"
iss="$root/packaging/windows/PowerShellSessionRecorder.iss"
info=$(sed -n 's:.*<InformationalVersion>\([^<]*\)</InformationalVersion>.*:\1:p' "$csproj" | head -n 1)
info=${info%%+*}
iss_version=$(sed -n 's/^#define MyAppVersion "\(.*\)"/\1/p' "$iss" | head -n 1)
if [ -z "$info" ] || [ -z "$iss_version" ]; then
  echo "Application version or Inno Setup version was not found." >&2
  exit 1
fi
if [ "$info" != "$iss_version" ]; then
  echo "InformationalVersion $info does not match Inno Setup MyAppVersion $iss_version." >&2
  exit 1
fi
if [ "$#" -ge 1 ] && [ "$1" != "$info" ]; then
  echo "Expected version $1 but the project says $info." >&2
  exit 1
fi
debian=$(debian_version_from_informational "$info")
printf '%s\n' "$info"
printf '%s\n' "$debian"
