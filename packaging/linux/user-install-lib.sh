#!/usr/bin/env bash
# Shared path checks for the per-user installer. Do not run this file directly.

psr_fail() {
  printf '%s\n' "$1" >&2
  exit 1
}

psr_is_safe_text() {
  local value=$1
  [[ -n "$value" ]] || return 1
  [[ "$value" != *$'\n'* && "$value" != *$'\r'* ]] || return 1
  [[ "$value" != *'..'* ]] || return 1
  [[ "$value" != *'$'* && "$value" != *'`'* && "$value" != *'%'* && "$value" != *'"'* && "$value" != *'\\'* ]] || return 1
  return 0
}

psr_is_safe_absolute() {
  local path=$1
  psr_is_safe_text "$path" || return 1
  [[ "$path" == /* && "$path" != *//* ]] || return 1
  return 0
}

psr_is_safe_relative() {
  local path=$1
  psr_is_safe_text "$path" || return 1
  [[ "$path" != /* && "$path" != *'\\'* ]] || return 1
  return 0
}

psr_walk() {
  local path=$1
  local create=$2
  local current=""
  local part
  local -a parts=()
  psr_is_safe_absolute "$path" || psr_fail "Unsafe path."
  IFS='/' read -r -a parts <<< "${path#/}"
  for part in "${parts[@]}"; do
    [[ -n "$part" && "$part" != "." && "$part" != ".." ]] || psr_fail "Unsafe path."
    current="${current}/${part}"
    if [[ -L "$current" ]]; then
      psr_fail "Refusing symbolic link at ${current}."
    fi
    if [[ -d "$current" ]]; then
      continue
    fi
    if [[ -e "$current" ]]; then
      psr_fail "Refusing existing non-directory at ${current}."
    fi
    if [[ "$create" != "create" ]]; then
      psr_fail "Missing directory ${current}."
    fi
    mkdir "$current" || psr_fail "Could not create ${current}."
    if [[ -L "$current" || ! -d "$current" ]]; then
      psr_fail "Refusing destination at ${current}."
    fi
  done
}

psr_require_home() {
  psr_is_safe_absolute "${HOME:-}" || psr_fail "HOME is not a safe absolute path."
  psr_walk "$HOME" check
}

psr_install_root() {
  printf '%s\n' "$HOME/.local/opt/powershell-session-recorder"
}

psr_reject_payload_links() {
  local payload=$1
  local link
  link=$(find -P "$payload" -type l -print -quit)
  if [[ -n "$link" ]]; then
    psr_fail "Refusing symbolic link in the archive at ${link}."
  fi
}

psr_remove_manifest_files() {
  local root=$1
  local manifest=$2
  local line
  local first=1
  local relative
  local target
  [[ -f "$manifest" && ! -L "$manifest" ]] || psr_fail "The installation manifest is missing."
  while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ "$first" -eq 1 ]]; then
      first=0
      [[ "$line" == "powershell-session-recorder-user-install" ]] || psr_fail "The installation marker is not recognized."
      continue
    fi
    [[ -z "$line" ]] && continue
    psr_is_safe_relative "$line" || psr_fail "The installation manifest contains an unsafe path."
    relative=$line
    target="${root}/${relative}"
    if [[ -L "$target" ]]; then
      printf 'Left symbolic link in place: %s\n' "$target"
      continue
    fi
    if [[ -f "$target" ]]; then
      rm -- "$target" || psr_fail "Could not remove ${target}."
    fi
  done < "$manifest"
}
