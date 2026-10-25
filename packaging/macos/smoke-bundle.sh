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
  echo "Packaging or signing failed: the bundle is incomplete: $app" >&2
  exit 1
fi

log=$(mktemp)
trace=$(mktemp)
timeout_flag=$(mktemp)
trap 'rm -f "$log" "$trace" "$timeout_flag"' EXIT
# Host tracing applies only to this process. A normal GUI launch does not set it.
COREHOST_TRACE=1 COREHOST_TRACEFILE="$trace" "$exe" --smoke >"$log" 2>&1 &
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

classify_failure() {
  if grep -F -q "SMOKE_FAIL" "$log"; then
    if grep -F -q "DllNotFoundException" "$log" \
      || grep -F -q "did not resolve" "$log" \
      || grep -F -q "instead of Contents/MacOS" "$log"; then
      echo "Native dependency loading failed." >&2
    elif grep -F -q "Avalonia" "$log" || grep -F -q "subsystem" "$log"; then
      echo "Avalonia backend initialization failed." >&2
    elif grep -F -q "beside the runtime configuration" "$log" \
      || grep -F -q "application base directory" "$log"; then
      echo "Apphost could not locate the managed entry assembly." >&2
    else
      echo "Startup smoke test reported a failure." >&2
    fi
  elif grep -F -q "The application to execute does not exist" "$log" "$trace" \
    || grep -F -q "This executable is not bound to a managed DLL" "$log" "$trace"; then
    echo "Apphost could not locate the managed entry assembly." >&2
  elif grep -F -q "libhostpolicy" "$log" "$trace" \
    || grep -F -q "libhostfxr" "$log" "$trace" \
    || grep -F -q "Failed to run as a self-contained" "$log" "$trace"; then
    echo "Hostfxr or hostpolicy could not initialize." >&2
  else
    echo "Startup smoke test failed before the application reported a result." >&2
  fi
  if [ -s "$trace" ]; then
    echo "--- .NET host trace ---" >&2
    cat "$trace" >&2
  fi
}

if [ -s "$timeout_flag" ]; then
  echo "Startup smoke test timed out after ${limit}s." >&2
  classify_failure
  exit 1
fi
if [ "$status" -ne 0 ]; then
  echo "Startup smoke test exited $status." >&2
  classify_failure
  exit "$status"
fi
if grep -F -q "SMOKE_FAIL" "$log"; then
  echo "Startup smoke test reported a failure." >&2
  classify_failure
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
  "libHarfBuzzSharp.dylib" \
  "platform=Avalonia.Native.AvaloniaNativePlatform"
do
  if ! grep -F -q "$marker" "$log"; then
    echo "Startup smoke test did not report $marker." >&2
    classify_failure
    exit 1
  fi
done
