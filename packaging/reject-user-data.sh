#!/bin/sh
# Stop a publish or package step when per-user data is inside the payload.
# The application reads those files from the user profile, never from the install directory.
reject_user_data() {
  dir=$1
  if [ -z "$dir" ] || [ ! -d "$dir" ]; then
    echo "User-data check needs an existing directory." >&2
    exit 1
  fi

  found=$(find "$dir" -type f \( \
    -name 'settings.json' -o \
    -name 'settings.invalid.json' -o \
    -name 'customers.json' -o \
    -name 'history.json' -o \
    -name 'customers.json.lock' -o \
    -name 'history.json.lock' \
    \) -print)
  if [ -n "$found" ]; then
    echo "Refusing to package per-user data:" >&2
    printf '%s\n' "$found" >&2
    exit 1
  fi
}
