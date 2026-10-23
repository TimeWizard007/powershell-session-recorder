#!/usr/bin/env bash
# Install PowerShell Session Recorder for the current user.
# No sudo. User settings, customers, history, and transcripts are not touched.
set -euo pipefail

script_dir=$(dirname -- "${BASH_SOURCE[0]}")
case "$script_dir" in
  /*) ;;
  *) script_dir="${PWD}/${script_dir}" ;;
esac
# shellcheck source=user-install-lib.sh
source "${script_dir}/user-install-lib.sh"

if ! find --version 2>/dev/null | head -n 1 | grep -q 'GNU findutils'; then
  psr_fail "GNU find is required."
fi

payload=$script_dir
for required in \
  "app/PowerShellSessionRecorder" \
  "app/PowerShellSessionRecorder.dll" \
  "icons/hicolor/256x256/apps/powershell-session-recorder.png" \
  "uninstall-user.sh" \
  "user-install-lib.sh"
do
  if [[ ! -f "${payload}/${required}" || -L "${payload}/${required}" ]]; then
    psr_fail "The archive is missing ${required}."
  fi
done
psr_reject_payload_links "$payload/app"
psr_reject_payload_links "$payload/icons"

psr_require_home
parent="$HOME/.local/opt"
psr_walk "$parent" create
dest=$(psr_install_root)
if [[ -L "$dest" ]]; then
  psr_fail "Refusing symbolic link at ${dest}."
fi
if [[ -e "$dest" && ! -d "$dest" ]]; then
  psr_fail "Refusing existing non-directory at ${dest}."
fi
if [[ -d "$dest" && ! -f "$dest/user-install-manifest.txt" ]]; then
  psr_fail "Refusing to replace ${dest} because it is not a per-user installation of this application."
fi

version="unknown"
if [[ -f "$payload/VERSION" && ! -L "$payload/VERSION" ]]; then
  version=$(head -n 1 "$payload/VERSION")
  psr_is_safe_text "$version" || psr_fail "The archive version is unsafe."
fi

staging="${parent}/powershell-session-recorder.staging.$$"
previous=""
cleanup_staging() {
  if [[ -d "$staging" && ! -L "$staging" && -f "$staging/user-install-manifest.txt" ]]; then
    psr_remove_manifest_files "$staging" "$staging/user-install-manifest.txt" || true
    find -P "$staging" -depth -type d -exec rmdir {} + 2>/dev/null || true
  fi
}
trap cleanup_staging EXIT

if [[ -e "$staging" || -L "$staging" ]]; then
  psr_fail "A staging directory already exists at ${staging}."
fi
mkdir "$staging"
cp -a "${payload}/app/." "$staging/"
mkdir "$staging/icons"
cp -a "${payload}/icons/." "$staging/icons/"
cp "${payload}/uninstall-user.sh" "${payload}/user-install-lib.sh" "$staging/"
chmod 755 "$staging/PowerShellSessionRecorder" "$staging/uninstall-user.sh" "$staging/user-install-lib.sh"
psr_reject_payload_links "$staging"

icon_plan="$staging/user-install-icons.txt"
: > "$icon_plan"
while IFS= read -r png; do
  relative=${png#"$staging/icons/"}
  icon_dir_rel=${relative%/*}
  case "$icon_dir_rel" in
    hicolor/[0-9]*x[0-9]*/apps) ;;
    *) psr_fail "Unexpected icon path in the archive." ;;
  esac
  printf '%s\n' "${icon_dir_rel}/powershell-session-recorder-user.png" >> "$icon_plan"
done < <(find -P "$staging/icons/hicolor" -type f -name 'powershell-session-recorder.png' | LC_ALL=C sort)
if ! grep -q 'hicolor/256x256/apps/powershell-session-recorder-user.png' "$icon_plan"; then
  psr_fail "The archive is missing the 256 pixel icon."
fi

body=$(find -P "$staging" -type f -printf '%P\n' | LC_ALL=C sort)
{
  printf '%s\n' "powershell-session-recorder-user-install"
  printf '%s\n' "$body"
  printf '%s\n' "user-install-manifest.txt"
} > "$staging/user-install-manifest.txt"

if [[ -d "$dest" ]]; then
  previous="${parent}/powershell-session-recorder.previous.$$"
  mv "$dest" "$previous"
fi
if ! mv "$staging" "$dest"; then
  if [[ -n "$previous" ]]; then
    mv "$previous" "$dest" || psr_fail "The previous installation could not be restored."
  fi
  psr_fail "The new installation could not be moved into place."
fi
trap - EXIT
staging=""

if [[ -n "$previous" ]]; then
  psr_remove_manifest_files "$previous" "$previous/user-install-manifest.txt"
  find -P "$previous" -depth -type d -exec rmdir {} + 2>/dev/null || true
fi

while IFS= read -r relative || [[ -n "$relative" ]]; do
  [[ -z "$relative" ]] && continue
  case "$relative" in
    hicolor/[0-9]*x[0-9]*/apps/powershell-session-recorder-user.png) ;;
    *) psr_fail "Unexpected icon path in the archive." ;;
  esac
  size_dir=${relative%/*}
  source_png="$dest/icons/${size_dir}/powershell-session-recorder.png"
  icon_dir="$HOME/.local/share/icons/${size_dir}"
  psr_walk "$icon_dir" create
  installed_icon="$HOME/.local/share/icons/${relative}"
  if [[ -L "$installed_icon" || -L "$source_png" ]]; then
    psr_fail "Refusing symbolic link at ${installed_icon}."
  fi
  if [[ ! -f "$source_png" ]]; then
    psr_fail "The archive is missing ${source_png}."
  fi
  cp -- "$source_png" "$installed_icon"
done < "$dest/user-install-icons.txt"
if command -v gtk-update-icon-cache >/dev/null 2>&1 && [[ ! -L "$HOME/.local/share/icons/hicolor" ]]; then
  gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" >/dev/null 2>&1 || true
fi

applications="$HOME/.local/share/applications"
psr_walk "$applications" create
desktop="${applications}/powershell-session-recorder-user.desktop"
if [[ -L "$desktop" ]]; then
  psr_fail "Refusing symbolic link at ${desktop}."
fi
if [[ -e "$desktop" ]] && ! grep -q '^X-PSR-User-Install=1$' "$desktop"; then
  psr_fail "Refusing to replace an unrelated desktop entry at ${desktop}."
fi
temporary="${desktop}.tmp.$$"
cat > "$temporary" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=PowerShell Session Recorder (per user)
Name[pl]=Rejestrator sesji PowerShell (użytkownik)
Comment=Per-user copy of PowerShell Session Recorder
Comment[pl]=Kopia Rejestratora sesji PowerShell dla bieżącego użytkownika
Exec="${dest}/PowerShellSessionRecorder"
Icon=powershell-session-recorder-user
Terminal=false
Categories=Utility;
StartupWMClass=PowerShellSessionRecorder
X-PSR-User-Install=1
EOF
mv "$temporary" "$desktop"

if [[ -x /usr/bin/powershell-session-recorder && ! -L /usr/bin/powershell-session-recorder ]]; then
  printf '%s\n' "A system-wide launcher is also installed. This per-user copy uses a separate desktop entry and does not replace /usr/bin/powershell-session-recorder."
elif [[ -L /usr/bin/powershell-session-recorder ]]; then
  printf '%s\n' "A system launcher path exists as a symbolic link. This per-user copy does not change it."
fi

if [[ -n "$previous" ]]; then
  printf 'Upgraded the per-user application to %s. Settings, customers, history, and transcripts were not changed.\n' "$version"
else
  printf 'Installed the per-user application %s. Settings, customers, history, and transcripts were not changed.\n' "$version"
fi
