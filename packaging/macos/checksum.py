#!/usr/bin/env python3
"""Write a sha256sum-compatible checksum beside an artifact."""

from __future__ import annotations

import hashlib
import sys
from pathlib import Path


def write_checksum(path: Path) -> None:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while True:
            block = stream.read(1024 * 1024)
            if not block:
                break
            digest.update(block)
    checksum = path.with_name(path.name + ".sha256")
    text = f"{digest.hexdigest()}  {path.name}\n"
    checksum.write_text(text, encoding="ascii")
    recorded, name = checksum.read_text(encoding="ascii").split()
    if recorded != digest.hexdigest() or name != path.name:
        raise SystemExit(f"Checksum filename does not match {path.name}.")
    print(checksum)


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("Usage: checksum.py <file>")
    write_checksum(Path(sys.argv[1]))


if __name__ == "__main__":
    main()
