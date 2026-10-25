#!/bin/sh
# Ad-hoc sign one completed PowerShell Session Recorder.app for internal testing.
#
# On macOS, dotnet publish signs the host executable by itself and keeps the
# identifier "apphost". That signature is not bound to Info.plist, seals no
# bundle resources, and still tells codesign that resources must be present.
# Managed assemblies must already be in Contents/Resources. Every file left in
# Contents/MacOS is Mach-O and is signed as nested code. The outer bundle is
# signed after those libraries, with Info.plist bound.
# Nested Mach-O files are signed first. The outer bundle is signed after them
# with com.timewizard.powershellsessionrecorder.
#
# This is not a Developer ID signature and it does not notarize the app.
# It does not change Gatekeeper and it does not remove quarantine attributes.
set -eu

identifier=com.timewizard.powershellsessionrecorder
executable_name=PowerShellSessionRecorder

if [ "${1:-}" = "--verify" ]; then
  verify_only=1
  shift
else
  verify_only=0
fi

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
  echo "Usage: sign-bundle.sh [--verify] <App.app>" >&2
  exit 1
fi

app=$1

if [ "$(uname -s)" != "Darwin" ]; then
  echo "codesign is only available on macOS. Refusing to skip signing for $app." >&2
  exit 1
fi

if ! command -v codesign >/dev/null 2>&1; then
  echo "codesign was not found." >&2
  exit 1
fi

if [ ! -d "$app/Contents/MacOS" ] \
  || [ ! -f "$app/Contents/Info.plist" ] \
  || [ ! -f "$app/Contents/Resources/AppIcon.icns" ] \
  || [ ! -x "$app/Contents/MacOS/$executable_name" ]; then
  echo "Refusing to sign an incomplete bundle: $app" >&2
  exit 1
fi

found_identifier=$(plutil -extract CFBundleIdentifier raw "$app/Contents/Info.plist")
found_executable=$(plutil -extract CFBundleExecutable raw "$app/Contents/Info.plist")
if [ "$found_identifier" != "$identifier" ]; then
  echo "CFBundleIdentifier is '$found_identifier', expected '$identifier'." >&2
  exit 1
fi
if [ "$found_executable" != "$executable_name" ]; then
  echo "CFBundleExecutable is '$found_executable', expected '$executable_name'." >&2
  exit 1
fi

assert_signature() {
  details=$(codesign -dv --verbose=4 "$app" 2>&1) || true
  printf '%s\n' "$details"
  if [ -z "$details" ]; then
    echo "codesign could not describe $app." >&2
    exit 1
  fi
  if ! printf '%s\n' "$details" | grep -F -x -q "Identifier=$identifier"; then
    echo "The signature identifier is not $identifier." >&2
    exit 1
  fi
  if printf '%s\n' "$details" | grep -F -x -q "Identifier=apphost"; then
    echo "The signature still identifies the .NET apphost." >&2
    exit 1
  fi
  if printf '%s\n' "$details" | grep -F -q "not bound"; then
    echo "Info.plist is not bound to the signature." >&2
    exit 1
  fi
  if ! printf '%s\n' "$details" | grep -F -q "Info.plist entries="; then
    echo "Info.plist is not bound to the signature." >&2
    exit 1
  fi
  if printf '%s\n' "$details" | grep -F -q "Sealed Resources=none"; then
    echo "The signature does not seal bundle resources." >&2
    exit 1
  fi
  if printf '%s\n' "$details" | grep -F -q "Sealed Resources: none"; then
    echo "The signature does not seal bundle resources." >&2
    exit 1
  fi
  if ! printf '%s\n' "$details" | grep -F -q "Sealed Resources version="; then
    echo "The signature does not seal bundle resources." >&2
    exit 1
  fi
  codesign --verify --deep --strict --verbose=2 "$app"
}

if [ "$verify_only" -eq 1 ]; then
  assert_signature
  exit 0
fi

if find "$app/Contents/MacOS" -type f -name '*.dll' -print -quit | grep -q .; then
  echo "Contents/MacOS still contains a managed assembly. codesign --deep would treat it as unsigned nested code." >&2
  exit 1
fi

main="$app/Contents/MacOS/$executable_name"
list=$(mktemp)
trap 'rm -f "$list"' EXIT
find "$app" -type f -print | sort > "$list"
while IFS= read -r path; do
  [ -n "$path" ] || continue
  if [ "$path" = "$main" ]; then
    continue
  fi
  if file -b "$path" | grep -q 'Mach-O'; then
    codesign --force --sign - --timestamp=none --generate-entitlement-der "$path" || exit 1
  fi
done < "$list"

codesign --force --sign - --timestamp=none --generate-entitlement-der \
  --identifier "$identifier" \
  "$app" || exit 1
assert_signature
