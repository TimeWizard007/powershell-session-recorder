#!/usr/bin/env python3
"""Require a built assembly to contain the informational version plus the release commit."""

import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 4:
        print("Usage: assert-informational-version.py <file> <version> <sha>", file=sys.stderr)
        return 1

    path, version, sha = sys.argv[1:]
    if len(sha) != 40 or any(character not in "0123456789abcdef" for character in sha):
        print("Commit SHA must be 40 lowercase hex characters.", file=sys.stderr)
        return 1

    expected = f"{version}+{sha}".encode("ascii")
    data = Path(path).read_bytes()
    if expected not in data and expected.decode("ascii").encode("utf-16le") not in data:
        print(f"{path} does not contain informational version {version}+{sha}.", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
