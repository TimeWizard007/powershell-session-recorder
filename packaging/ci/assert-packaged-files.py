#!/usr/bin/env python3
"""Reject sample user data and require the approved Linux icon files."""

import subprocess
import sys
from pathlib import Path

FORBIDDEN = (
    "settings.json",
    "settings.invalid.json",
    "customers.json",
    "history.json",
    "customers.json.lock",
    "history.json.lock",
)
ICON_SIZES = ("16", "22", "24", "32", "48", "64", "128", "256", "512")


def listing(path: Path) -> str:
    if path.suffix == ".deb":
        command = ["dpkg-deb", "-c", str(path)]
    else:
        command = ["tar", "-tzf", str(path)]
    return subprocess.check_output(command, text=True)


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: assert-packaged-files.py <package>...", file=sys.stderr)
        return 1

    failed = False
    for raw in sys.argv[1:]:
        path = Path(raw)
        names = listing(path)
        base_names = [Path(line.split()[-1]).name for line in names.splitlines() if line.strip()]
        for forbidden in FORBIDDEN:
            if any(name == forbidden for name in base_names):
                print(f"{path} contains {forbidden}.", file=sys.stderr)
                failed = True
        if any(name.endswith(".pdb") for name in base_names):
            print(f"{path} contains a PDB.", file=sys.stderr)
            failed = True
        if ".svg" in names:
            print(f"{path} contains an SVG icon.", file=sys.stderr)
            failed = True
        for size in ICON_SIZES:
            expected = f"hicolor/{size}x{size}/apps/powershell-session-recorder.png"
            if expected not in names:
                print(f"{path} is missing {expected}.", file=sys.stderr)
                failed = True
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
