#!/bin/sh
# Map an informational version to a Debian package version.
# 0.1.0-checkpoint.4.1 -> 0.1.0~checkpoint.4.1-1
# 1.0.0 -> 1.0.0-1
# Text after '+' is build metadata and is omitted.
debian_version_from_informational() {
  info=$1
  info=${info%%+*}
  case "$info" in
    *-*)
      core=${info%%-*}
      pre=${info#*-}
      if [ -z "$core" ] || [ -z "$pre" ] || [ "$pre" != "${pre#*-}" ]; then
        echo "Informational version cannot be mapped: $1" >&2
        return 1
      fi
      printf '%s~%s-1\n' "$core" "$pre"
      ;;
    *)
      printf '%s-1\n' "$info"
      ;;
  esac
}
