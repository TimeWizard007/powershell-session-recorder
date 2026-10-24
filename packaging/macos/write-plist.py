#!/usr/bin/env python3
"""Write the macOS Info.plist for one PowerShell Session Recorder bundle."""

from __future__ import annotations

import plistlib
import re
import sys
from pathlib import Path

BUNDLE_ID = "com.timewizard.powershellsessionrecorder"
EXECUTABLE = "PowerShellSessionRecorder"
DISPLAY_NAME = "PowerShell Session Recorder"
SHORT_VERSION = re.compile(r"^[0-9]+(?:\.[0-9]+){1,3}$")
INFORMATIONAL = re.compile(r"^[0-9A-Za-z][0-9A-Za-z.+-]*$")


def build(informational: str, short_version: str, copyright_text: str) -> dict:
    if not INFORMATIONAL.fullmatch(informational) or "+" in informational:
        raise SystemExit("Informational version is not safe for Info.plist.")
    if not SHORT_VERSION.fullmatch(short_version):
        raise SystemExit("Short version is not a numeric bundle version.")
    if not copyright_text or any(character in copyright_text for character in "\0\r\n"):
        raise SystemExit("Copyright text is empty or contains a control character.")

    return {
        "CFBundleName": DISPLAY_NAME,
        "CFBundleDisplayName": DISPLAY_NAME,
        "CFBundleIdentifier": BUNDLE_ID,
        "CFBundleExecutable": EXECUTABLE,
        "CFBundlePackageType": "APPL",
        "CFBundleIconFile": "AppIcon",
        "CFBundleShortVersionString": short_version,
        "CFBundleVersion": short_version,
        "CFBundleInfoDictionaryVersion": "6.0",
        "CFBundleGetInfoString": f"{DISPLAY_NAME} {informational}",
        "NSHumanReadableCopyright": copyright_text,
        "LSMinimumSystemVersion": "14.0",
        "LSApplicationCategoryType": "public.app-category.utilities",
        "NSHighResolutionCapable": True,
        "NSPrincipalClass": "NSApplication",
    }


def main() -> None:
    if len(sys.argv) != 5:
        raise SystemExit("Usage: write-plist.py <Info.plist> <informational> <short-version> <copyright>")
    destination = Path(sys.argv[1])
    payload = build(sys.argv[2], sys.argv[3], sys.argv[4])
    destination.parent.mkdir(parents=True, exist_ok=True)
    with destination.open("wb") as stream:
        plistlib.dump(payload, stream, fmt=plistlib.FMT_XML, sort_keys=False)


if __name__ == "__main__":
    main()
