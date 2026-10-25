#!/bin/sh
# Start one signed Apple Silicon bundle far enough to prove the .NET 10 host.
#
# The executable is invoked directly with --smoke. That resolves the managed
# app, runtime configuration, and native libraries, then initializes Avalonia
# without opening a window or reading the user profile. A missing dependency
# or an immediate crash fails this script. It does not assess Gatekeeper.
set -eu

limit=60

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
  echo "Usage: smoke-bundle.sh <App.app>" >&2
  exit 1
fi

app=$1
exe="$app/Contents/MacOS/PowerShellSessionRecorder"

if [ "$(uname -s)" != "Darwin" ] || [ "$(uname -m)" != "arm64" ]; then
  echo "The startup smoke test runs on Apple Silicon macOS. This host is $(uname -s) $(uname -m)." >&2
  exit 1
fi

if [ ! -x "$exe" ] || [ ! -f "$app/Contents/Resources/PowerShellSessionRecorder.dll" ]; then
  echo "Refusing to start an incomplete bundle: $app" >&2
  exit 1
fi

log=$(mktemp)
timeout_flag=$(mktemp)
trap 'rm -f "$log" "$timeout_flag"' EXIT
"$exe" --smoke >"$log" 2>&1 &
pid=$!
(
  sleep "$limit"
  if kill -0 "$pid" 2>/dev/null; then
    echo timed-out >"$timeout_flag"
    kill "$pid" 2>/dev/null || true
    sleep 1
    kill -9 "$pid" 2>/dev/null || true
  fi
) &
timer=$!
set +e
wait "$pid"
status=$?
kill "$timer" 2>/dev/null
wait "$timer" 2>/dev/null
set -e
cat "$log"
if [ -s "$timeout_flag" ]; then
  echo "Startup smoke test timed out after ${limit}s." >&2
  exit 1
fi
if [ "$status" -ne 0 ]; then
  echo "Startup smoke test exited $status." >&2
  exit "$status"
fi
if grep -F -q "SMOKE_FAIL" "$log"; then
  echo "Startup smoke test reported a failure." >&2
  exit 1
fi
for marker in \
  "SMOKE_OK " \
  "dll=PowerShellSessionRecorder.dll" \
  "runtimeconfig=net10.0" \
  "libcoreclr.dylib" \
  "libhostfxr.dylib" \
  "libhostpolicy.dylib" \
  "libAvaloniaNative.dylib" \
  "libSkiaSharp.dylib" \
  "libHarfBuzzSharp.dylib"
do
  if ! grep -F -q "$marker" "$log"; then
    echo "Startup smoke test did not report $marker." >&2
    exit 1
  fi
done
