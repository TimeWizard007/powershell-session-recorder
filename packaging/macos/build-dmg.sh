#!/bin/sh
# Build unsigned self-contained macOS bundles for osx-x64 and osx-arm64.
# The .app is a directory. The zip is that same bundle in one checksummed file.
# On macOS the disk image is an hdiutil UDZO volume. Elsewhere it is an
# uncompressed UDIF image around a Rock Ridge ISO. Neither path signs the app,
# notarizes it, or changes Gatekeeper.
set -eu

root=$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)
cd "$root"
csproj="$root/src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj"
macos="$root/packaging/macos"
target=${1:-all}

case "$target" in
  all | osx-x64 | osx-arm64) ;;
  *)
    echo "Usage: build-dmg.sh [all|osx-x64|osx-arm64]" >&2
    exit 1
    ;;
esac

informational=$(sed -n 's:.*<InformationalVersion>\([^<]*\)</InformationalVersion>.*:\1:p' "$csproj" | head -n 1)
informational=${informational%%+*}
short_version=$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$csproj" | head -n 1)
copyright=$(sed -n 's:.*<Copyright>\([^<]*\)</Copyright>.*:\1:p' "$csproj" | head -n 1)
if [ -z "$informational" ] || [ -z "$short_version" ] || [ -z "$copyright" ]; then
  echo "Version metadata was not found in the application project." >&2
  exit 1
fi

app_name="PowerShell Session Recorder"
icon="$root/assets/branding/macos/AppIcon.icns"
if [ ! -f "$icon" ]; then
  echo "Missing $icon. Run packaging/icons/generate-icons.py." >&2
  exit 1
fi

build_one() {
  rid=$1
  (
    set -eu
    stage=$(mktemp -d)
    trap 'rm -rf "$stage"' EXIT
    publish="$stage/publish"
    image="$stage/image"
    app="$image/$app_name.app"
    mkdir -p "$publish"

    revision_args=""
    if [ -n "${SOURCE_REVISION_ID:-}" ]; then
      revision_args="-p:SourceRevisionId=${SOURCE_REVISION_ID} -p:ContinuousIntegrationBuild=true"
    fi
    # shellcheck disable=SC2086
    dotnet publish "$csproj" \
      -c Release \
      -r "$rid" \
      --self-contained true \
      -p:PublishReadyToRun=false \
      -p:DebugType=none \
      -p:DebugSymbols=false \
      -o "$publish" \
      $revision_args

    mkdir -p "$app/Contents/Resources"
    mv "$publish" "$app/Contents/MacOS"
    rm -f "$app/Contents/MacOS/"*.pdb
    . "$root/packaging/reject-user-data.sh"
    reject_user_data "$app"
    find "$app" -type d -exec chmod 755 {} +
    find "$app" -type f -exec chmod 644 {} +
    chmod 755 "$app/Contents/MacOS/PowerShellSessionRecorder"
    find "$app/Contents/MacOS" -type f \( -name '*.dylib' -o -name 'createdump' \) -exec chmod 755 {} +
    cp "$icon" "$app/Contents/Resources/AppIcon.icns"
    printf 'APPL????' > "$app/Contents/PkgInfo"
    python3 "$macos/write-plist.py" \
      "$app/Contents/Info.plist" \
      "$informational" \
      "$short_version" \
      "$copyright"
    chmod 644 \
      "$app/Contents/Resources/AppIcon.icns" \
      "$app/Contents/Info.plist" \
      "$app/Contents/PkgInfo"
    python3 "$macos/validate-bundle.py" "$app" "$rid" "$informational"

    ln -s /Applications "$image/Applications"
    cp "$macos/GATEKEEPER.txt" "$image/GATEKEEPER.txt"
    links=$(find "$app" -type l -print)
    if [ -n "$links" ]; then
      echo "The application bundle contains a symbolic link." >&2
      printf '%s\n' "$links" >&2
      exit 1
    fi
    APP="$app" python3 - <<'PY'
import os
from pathlib import Path
root = Path(os.environ["APP"])
stamp = 1767225600
for path in [root, *root.rglob("*")]:
    os.utime(path, (stamp, stamp), follow_symlinks=False)
PY
    export COPYFILE_DISABLE=1

    destination="$root/artifacts/$rid"
    rm -rf "$destination"
    mkdir -p "$destination" "$root/artifacts"
    zip_name="PowerShellSessionRecorder-${informational}-${rid}.zip"
    dmg_name="PowerShellSessionRecorder-${informational}-${rid}.dmg"
    (
      cd "$image"
      zip -r -X -y "$root/artifacts/$zip_name" "$app_name.app"
    )
    if command -v hdiutil >/dev/null 2>&1; then
      hdiutil create \
        -volname "$app_name" \
        -srcfolder "$image" \
        -ov \
        -format UDZO \
        "$root/artifacts/$dmg_name"
      hdiutil verify "$root/artifacts/$dmg_name"
    else
      if ! command -v xorriso >/dev/null 2>&1; then
        echo "Creating a disk image requires hdiutil or xorriso." >&2
        exit 1
      fi
      python3 "$macos/write-dmg.py" "$image" "$root/artifacts/$dmg_name"
      python3 "$macos/write-dmg.py" --check "$root/artifacts/$dmg_name"
    fi
    python3 "$macos/checksum.py" "$root/artifacts/$zip_name"
    python3 "$macos/checksum.py" "$root/artifacts/$dmg_name"
    mv "$app" "$destination/$app_name.app"
    echo "$destination/$app_name.app"
    echo "$root/artifacts/$zip_name"
    echo "$root/artifacts/$dmg_name"
  )
}

if [ "$target" = "all" ] || [ "$target" = "osx-x64" ]; then
  build_one osx-x64
fi
if [ "$target" = "all" ] || [ "$target" = "osx-arm64" ]; then
  build_one osx-arm64
fi
