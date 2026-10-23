#!/usr/bin/env bash
# Remove only the per-user application files. Settings, customers, history, and transcripts stay.
set -euo pipefail

script_dir=$(dirname -- "${BASH_SOURCE[0]}")
case "$script_dir" in
  /*) ;;
  *) script_dir="${PWD}/${script_dir}" ;;
esac
# shellcheck source=user-install-lib.sh
source "${script_dir}/user-install-lib.sh"

psr_require_home
dest=$(psr_install_root)
if [[ -L "$dest" ]]; then
  psr_fail "Refusing symbolic link at ${dest}."
fi
if [[ ! -d "$dest" ]]; then
  printf '%s\n' "No per-user installation was found. Settings, customers, history, and transcripts were kept."
  exit 0
fi
if [[ ! -f "$dest/user-install-manifest.txt" || -L "$dest/user-install-manifest.txt" ]]; then
  psr_fail "Refusing to remove ${dest} because it is not a per-user installation of this application."
fi

icon_list="$dest/user-install-icons.txt"
if [[ -f "$icon_list" && ! -L "$icon_list" ]]; then
  while IFS= read -r relative || [[ -n "$relative" ]]; do
    [[ -z "$relative" ]] && continue
    case "$relative" in
      hicolor/[0-9]*x[0-9]*/apps/powershell-session-recorder-user.png) ;;
      *) psr_fail "The icon list contains an unexpected path." ;;
    esac
    psr_is_safe_relative "$relative" || psr_fail "The icon list contains an unsafe path."
    target="$HOME/.local/share/icons/${relative}"
    if [[ -L "$target" ]]; then
      printf 'Left symbolic link in place: %s\n' "$target"
      continue
    fi
    if [[ -f "$target" ]]; then
      rm -- "$target"
    fi
    rmdir "$(dirname -- "$target")" 2>/dev/null || true
    rmdir "$(dirname -- "$(dirname -- "$target")")" 2>/dev/null || true
  done < "$icon_list"
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1 && [[ -d "$HOME/.local/share/icons/hicolor" && ! -L "$HOME/.local/share/icons/hicolor" ]]; then
  gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" >/dev/null 2>&1 || true
fi

psr_remove_manifest_files "$dest" "$dest/user-install-manifest.txt"
find -P "$dest" -depth -type d -exec rmdir {} + 2>/dev/null || true

desktop="$HOME/.local/share/applications/powershell-session-recorder-user.desktop"
if [[ -L "$desktop" ]]; then
  printf 'Left symbolic link in place: %s\n' "$desktop"
elif [[ -f "$desktop" ]] && grep -q '^X-PSR-User-Install=1$' "$desktop"; then
  rm -- "$desktop"
fi

if [[ -d "$dest" ]]; then
  printf 'Removed the listed per-user application files. Some unexpected files were left in place. Settings, customers, history, and transcripts were kept.\n'
else
  printf 'Removed the per-user application files. Settings, customers, history, and transcripts were kept.\n'
fi
