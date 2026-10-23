#!/bin/sh
# Confirm a release tag points at this clean, detached checkout and at the project version.
# This script does not create a tag or a GitHub Release.
set -eu

if [ "$#" -ne 1 ]; then
  echo "Usage: resolve-release-tag.sh v0.1.0-beta.2" >&2
  exit 1
fi

tag=$1
case "$tag" in
  v[0-9]*) ;;
  *)
    echo "Tag must look like v0.1.0-beta.2." >&2
    exit 1
    ;;
esac
case "$tag" in
  *[!A-Za-z0-9._-]*)
    echo "Tag contains unsupported characters." >&2
    exit 1
    ;;
esac

root=$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)
cd "$root"

if git symbolic-ref -q HEAD >/dev/null; then
  echo "Refusing to build from a branch. Check out the tag $tag." >&2
  exit 1
fi
if [ -n "$(git status --porcelain)" ]; then
  echo "Refusing to build a dirty tree." >&2
  exit 1
fi
if ! git rev-parse --verify --quiet "refs/tags/$tag" >/dev/null; then
  echo "Tag $tag does not exist." >&2
  exit 1
fi

head=$(git rev-parse HEAD)
tagged=$(git rev-parse "refs/tags/$tag^{commit}")
if [ "$head" != "$tagged" ]; then
  echo "HEAD $head is not $tag ($tagged)." >&2
  exit 1
fi

version=${tag#v}
versions=$(packaging/ci/assert-version-files.sh "$version")
debian=$(printf '%s\n' "$versions" | sed -n '2p')

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  {
    printf 'sha=%s\n' "$head"
    printf 'version=%s\n' "$version"
    printf 'debian=%s\n' "$debian"
  } >> "$GITHUB_OUTPUT"
fi
printf 'sha=%s\nversion=%s\ndebian=%s\n' "$head" "$version" "$debian"
