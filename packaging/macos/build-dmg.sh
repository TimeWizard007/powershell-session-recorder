#!/bin/sh
# Build self-contained macOS bundles for osx-x64 and osx-arm64.
# The .app is a directory. The zip is that same bundle in one checksummed file.
# On macOS the completed bundle is ad-hoc signed and verified before the zip
# and the UDZO image are written. The image is created on a private mount.
# Copying the signed bundle through hdiutil's shared source-folder attach
# reports "Resource busy" on the macOS runner. Elsewhere the disk image is an
# uncompressed UDIF image around a Rock Ridge ISO, and codesign is unavailable.
# Neither path uses a Developer ID, notarizes the app, or changes Gatekeeper.
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

verify_zipped_bundle() {
  archive=$1
  extract=$(mktemp -d)
  unzip -q "$archive" -d "$extract"
  set +e
  sh "$macos/sign-bundle.sh" --verify "$extract/$app_name.app"
  verify_status=$?
  set -e
  rm -rf "$extract"
  if [ "$verify_status" -ne 0 ]; then
    echo "The signed bundle in $archive failed verification." >&2
    exit "$verify_status"
  fi
}

report_disks() {
  echo "Attached disk images:" >&2
  hdiutil info >&2 || true
  echo "Mount table:" >&2
  mount >&2 || true
}

detach_mounted() {
  if [ -z "${mounted:-}" ]; then
    return 0
  fi
  point=$mounted
  echo "hdiutil detach: $point" >&2
  if ! hdiutil detach "$point"; then
    echo "hdiutil detach failed for $point. Forcing detach." >&2
    report_disks
    hdiutil detach -force "$point"
  fi
  mounted=
  rmdir "$point" 2>/dev/null || true
}

verify_mounted_bundle() {
  image=$1
  mount=$(mktemp -d)
  echo "hdiutil attach: image=$image mount=$mount mode=readonly" >&2
  if ! hdiutil attach -readonly -nobrowse -noautoopen -mountpoint "$mount" "$image"; then
    echo "hdiutil attach failed for $image at $mount." >&2
    report_disks
    exit 1
  fi
  mounted=$mount
  set +e
  sh "$macos/sign-bundle.sh" --verify "$mount/$app_name.app"
  verify_status=$?
  set -e
  detach_mounted
  if [ "$verify_status" -ne 0 ]; then
    echo "The signed bundle in $image failed verification." >&2
    exit "$verify_status"
  fi
}
if [ ! -f "$icon" ]; then
  echo "Missing $icon. Run packaging/icons/generate-icons.py." >&2
  exit 1
fi

build_one() {
  rid=$1
  (
    set -eu
    stage=$(mktemp -d)
    mounted=
    cleanup_stage() {
      set +e
      if [ -n "$mounted" ]; then
        echo "hdiutil detach: $mounted (cleanup)" >&2
        hdiutil detach "$mounted" >/dev/null 2>&1 || hdiutil detach -force "$mounted" >&2
      fi
      rm -rf "$stage"
    }
    trap cleanup_stage EXIT
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
    python3 "$macos/layout-bundle.py" "$app"
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

    if [ "$(uname -s)" = "Darwin" ]; then
      sh "$macos/sign-bundle.sh" "$app"
      if [ "$rid" = "osx-arm64" ]; then
        sh "$macos/smoke-bundle.sh" "$app"
      fi
    else
      echo "Skipping ad-hoc signing on $(uname -s). codesign binds Info.plist only on macOS."
    fi

    destination="$root/artifacts/$rid"
    rm -rf "$destination"
    mkdir -p "$destination" "$root/artifacts"
    zip_name="PowerShellSessionRecorder-${informational}-${rid}.zip"
    dmg_name="PowerShellSessionRecorder-${informational}-${rid}.dmg"
    (
      cd "$image"
      zip -r -X -y "$root/artifacts/$zip_name" "$app_name.app"
    )
    if [ "$(uname -s)" = "Darwin" ]; then
      verify_zipped_bundle "$root/artifacts/$zip_name"
    fi
    if command -v hdiutil >/dev/null 2>&1; then
      # Copying straight from the source folder makes hdiutil attach a shared
      # volume name. On the macos-15 runner that attach fails with
      # "Resource busy" once the signed bundle is in the source folder.
      # Build a private read-write image for this architecture, copy onto
      # its own mount, then compress.
      scratch="$stage/$rid-rw.dmg"
      mount="$stage/mnt"
      kb=$(du -sk "$image" | awk '{print $1}')
      mb=$(( (kb * 2 + 65536 + 1023) / 1024 ))
      mkdir -p "$mount"
      echo "hdiutil create: scratch=$scratch size=${mb}m volume=$app_name source=$image output=$root/artifacts/$dmg_name" >&2
      if ! hdiutil create -size "${mb}m" -fs HFS+ -volname "$app_name" "$scratch"; then
        echo "hdiutil create failed for $scratch." >&2
        report_disks
        exit 1
      fi
      echo "hdiutil attach: image=$scratch mount=$mount mode=read-write" >&2
      if ! hdiutil attach -nobrowse -noverify -noautoopen -mountpoint "$mount" "$scratch"; then
        echo "hdiutil attach failed for $scratch at $mount." >&2
        report_disks
        exit 1
      fi
      mounted=$mount
      echo "Copying $app and the disk image extras onto $mount." >&2
      ditto "$app" "$mount/$app_name.app"
      cp "$image/GATEKEEPER.txt" "$mount/GATEKEEPER.txt"
      ln -s /Applications "$mount/Applications"
      sync
      detach_mounted
      echo "hdiutil convert: source=$scratch format=UDZO output=$root/artifacts/$dmg_name" >&2
      if ! hdiutil convert "$scratch" -format UDZO -ov -o "$stage/$rid-compressed"; then
        echo "hdiutil convert failed for $scratch." >&2
        report_disks
        exit 1
      fi
      if [ ! -f "$stage/$rid-compressed.dmg" ]; then
        echo "hdiutil convert did not write $stage/$rid-compressed.dmg." >&2
        ls -l "$stage" >&2
        exit 1
      fi
      mv "$stage/$rid-compressed.dmg" "$root/artifacts/$dmg_name"
      echo "hdiutil verify: $root/artifacts/$dmg_name" >&2
      if ! hdiutil verify "$root/artifacts/$dmg_name"; then
        echo "hdiutil verify failed for $root/artifacts/$dmg_name." >&2
        report_disks
        exit 1
      fi
      verify_mounted_bundle "$root/artifacts/$dmg_name"
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
