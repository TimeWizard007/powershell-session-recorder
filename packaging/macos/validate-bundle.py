#!/usr/bin/env python3
"""Validate one PowerShell Session Recorder.app bundle.

The main executable must be a thin Mach-O for the requested architecture.
Avalonia's native libraries may be universal binaries; the requested
architecture has to be one of their slices. This does not run the application.
"""

from __future__ import annotations

import plistlib
import stat
import struct
import sys
from pathlib import Path

CPU = {
    "osx-x64": 0x01000007,
    "osx-arm64": 0x0100000C,
}
REQUIRED_LIBRARIES = (
    "libAvaloniaNative.dylib",
    "libSkiaSharp.dylib",
    "libHarfBuzzSharp.dylib",
)
FORBIDDEN_NAMES = {
    "settings.json",
    "settings.invalid.json",
    "customers.json",
    "history.json",
    "customers.json.lock",
    "history.json.lock",
}
REQUIRED_ICNS = ("ic07", "ic08", "ic09", "ic10")


def cpu_types(path: Path) -> set[int]:
    header = path.read_bytes()[:8 + 20 * 8]
    if len(header) < 8:
        raise SystemExit(f"{path} is too small to be a Mach-O file.")
    magic = int.from_bytes(header[:4], "big")
    if magic == 0xCAFEBABE:
        count = int.from_bytes(header[4:8], "big")
        if count < 1 or 8 + count * 20 > len(header):
            raise SystemExit(f"{path} has a truncated universal header.")
        return {
            int.from_bytes(header[8 + index * 20:12 + index * 20], "big")
            for index in range(count)
        }
    if int.from_bytes(header[:4], "little") in (0xFEEDFACF, 0xFEEDFACE):
        return {int.from_bytes(header[4:8], "little")}
    raise SystemExit(f"{path} is not a Mach-O file.")


def _is_macho(path: Path) -> bool:
    header = path.read_bytes()[:4]
    return header in {
        b"\xcf\xfa\xed\xfe",
        b"\xfe\xed\xfa\xcf",
        b"\xce\xfa\xed\xfe",
        b"\xfe\xed\xfa\xce",
        b"\xca\xfe\xba\xbe",
    }


def require_architecture(path: Path, expected: int, thin: bool) -> None:
    found = cpu_types(path)
    if expected not in found:
        raise SystemExit(f"{path} does not contain CPU type {expected:#x}. Found {sorted(found)!r}.")
    if thin and found != {expected}:
        raise SystemExit(f"{path} must be a thin binary for CPU type {expected:#x}.")


def require_executable(path: Path) -> None:
    mode = path.lstat().st_mode
    if stat.S_ISLNK(mode):
        raise SystemExit(f"{path} is a symbolic link.")
    if not mode & stat.S_IXUSR:
        raise SystemExit(f"{path} is not executable.")


def icns_types(path: Path) -> set[str]:
    data = path.read_bytes()
    if len(data) < 8 or data[:4] != b"icns":
        raise SystemExit(f"{path} is not an .icns file.")
    total = struct.unpack_from(">I", data, 4)[0]
    if total != len(data):
        raise SystemExit(f"{path} has an inconsistent .icns length.")
    found = set()
    offset = 8
    while offset + 8 <= total:
        kind = data[offset:offset + 4].decode("ascii", errors="replace")
        length = struct.unpack_from(">I", data, offset + 4)[0]
        if length < 8 or offset + length > total:
            raise SystemExit(f"{path} has a truncated {kind} chunk.")
        found.add(kind)
        offset += length
    if offset != total:
        raise SystemExit(f"{path} has trailing bytes.")
    return found


def validate(app: Path, rid: str, informational: str) -> None:
    if rid not in CPU:
        raise SystemExit(f"Unsupported architecture {rid}.")
    contents = app / "Contents"
    macos = contents / "MacOS"
    executable = macos / "PowerShellSessionRecorder"
    plist_path = contents / "Info.plist"
    icon = contents / "Resources" / "AppIcon.icns"
    for path in (contents, macos, executable, plist_path, icon):
        if not path.exists():
            raise SystemExit(f"Missing {path}.")
    if (contents / "_CodeSignature").exists():
        raise SystemExit("The bundle already has a signature directory. Sign it only after the contents are complete.")

    with plist_path.open("rb") as stream:
        plist = plistlib.load(stream)
    expected = {
        "CFBundleDisplayName": "PowerShell Session Recorder",
        "CFBundleIdentifier": "com.timewizard.powershellsessionrecorder",
        "CFBundleExecutable": "PowerShellSessionRecorder",
        "CFBundlePackageType": "APPL",
        "CFBundleIconFile": "AppIcon",
        "CFBundleShortVersionString": "0.1.0",
        "CFBundleVersion": "0.1.0",
        "LSMinimumSystemVersion": "14.0",
    }
    for key, value in expected.items():
        if plist.get(key) != value:
            raise SystemExit(f"{key} is {plist.get(key)!r}, expected {value!r}.")
    if plist.get("CFBundleGetInfoString") != f"PowerShell Session Recorder {informational}":
        raise SystemExit("CFBundleGetInfoString does not match the informational version.")

    require_executable(executable)
    require_architecture(executable, CPU[rid], thin=True)
    host = executable.read_bytes()
    embedded = b"../Resources/PowerShellSessionRecorder.dll\0"
    if embedded not in host:
        raise SystemExit("The app host is not bound to ../Resources/PowerShellSessionRecorder.dll.")
    # Search flag 2 is app-relative only. The following NUL-terminated path is
    # resolved from the managed assembly directory, Contents/Resources.
    if b"\x02\x00../MacOS\x00" not in host:
        raise SystemExit("The app host does not look for the self-contained runtime in ../MacOS.")
    for name in (
        "PowerShellSessionRecorder.dll",
        "PowerShellSessionRecorder.deps.json",
        "PowerShellSessionRecorder.runtimeconfig.json",
    ):
        if not (contents / "Resources" / name).is_file():
            raise SystemExit(f"Managed app file is missing from Contents/Resources: {name}.")
    for name in REQUIRED_LIBRARIES:
        library = macos / name
        if not library.is_file():
            raise SystemExit(f"Avalonia native library is missing: {name}.")
        require_architecture(library, CPU[rid], thin=False)
    for path in macos.rglob("*"):
        if path.name in FORBIDDEN_NAMES:
            raise SystemExit(f"The bundle contains per-user data: {path}.")
        if path.is_file() and not _is_macho(path):
            raise SystemExit(f"Contents/MacOS contains a file that is not a Mach-O binary: {path.name}.")
    missing_icons = [kind for kind in REQUIRED_ICNS if kind not in icns_types(icon)]
    if missing_icons:
        raise SystemExit(f"AppIcon.icns is missing {', '.join(missing_icons)}.")
    for language in ("en-US.json", "pl-PL.json"):
        if not (contents / "Resources" / "Localization" / language).is_file():
            raise SystemExit(f"Localization/{language} is missing from Contents/Resources.")

    for path in app.rglob("*"):
        mode = path.lstat().st_mode
        if stat.S_ISLNK(mode):
            raise SystemExit(f"The bundle contains a symbolic link: {path}.")
        if path.name in FORBIDDEN_NAMES:
            raise SystemExit(f"The bundle contains per-user data: {path}.")


def main() -> None:
    if len(sys.argv) != 4:
        raise SystemExit("Usage: validate-bundle.py <App.app> <osx-x64|osx-arm64> <informational>")
    validate(Path(sys.argv[1]), sys.argv[2], sys.argv[3])
    print(f"Validated {sys.argv[1]} for {sys.argv[2]}.")


if __name__ == "__main__":
    main()
