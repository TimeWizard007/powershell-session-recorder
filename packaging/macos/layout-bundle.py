#!/usr/bin/env python3
"""Place managed .NET files where codesign will not treat them as nested code.

Contents/MacOS is reserved for the app host and other Mach-O files. A managed
assembly there fails `codesign --verify --deep --strict` with
"code object is not signed at all". Managed assemblies, dependency manifests,
and localization catalogs move to Contents/Resources. The host executable is
rebound to ../Resources/<app>.dll, and native library paths in the deps file
point back at Contents/MacOS. Native dylibs stay beside the host. The
publish step embeds an app-relative runtime location of ../MacOS so hostfxr
and hostpolicy load from that directory instead of DOTNET_ROOT.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

APP_DLL = "PowerShellSessionRecorder.dll"
APP_HOST = "PowerShellSessionRecorder"
EMBEDDED_APP = b"../Resources/" + APP_DLL.encode("ascii")
MACOS_NATIVE = "../MacOS/"


def is_macho(path: Path) -> bool:
    header = path.read_bytes()[:4]
    return header in {
        b"\xcf\xfa\xed\xfe",
        b"\xfe\xed\xfa\xcf",
        b"\xce\xfa\xed\xfe",
        b"\xfe\xed\xfa\xce",
        b"\xca\xfe\xba\xbe",
    }


def move_managed_files(macos: Path, resources: Path) -> None:
    for path in sorted(macos.rglob("*"), reverse=True):
        if path.is_symlink():
            raise SystemExit(f"Refusing to move a symbolic link: {path}.")
        if path.is_dir():
            continue
        if is_macho(path):
            continue
        relative = path.relative_to(macos)
        destination = resources / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists():
            raise SystemExit(f"Refusing to replace {destination}.")
        path.replace(destination)
    for path in sorted(macos.rglob("*"), reverse=True):
        if path.is_dir() and not any(path.iterdir()):
            path.rmdir()


def bind_host(executable: Path) -> None:
    data = bytearray(executable.read_bytes())
    embedded = EMBEDDED_APP + b"\0"
    published = APP_DLL.encode("ascii") + b"\0"
    if data.find(embedded) != -1:
        return
    index = data.find(published)
    if index < 0 or data.find(published, index + 1) != -1:
        raise SystemExit(f"{executable.name} does not contain one published app path.")
    extra = len(embedded) - len(published)
    if data[index + len(published):index + len(embedded)] != b"\0" * extra:
        raise SystemExit(f"{executable.name} has no room for the Resources app path.")
    data[index:index + len(embedded)] = embedded
    executable.write_bytes(data)


def rewrite_native_paths(deps_path: Path, macos: Path) -> None:
    document = json.loads(deps_path.read_text(encoding="utf-8"))
    targets = document.get("targets")
    if not isinstance(targets, dict):
        raise SystemExit(f"{deps_path.name} has no targets.")
    for libraries in targets.values():
        if not isinstance(libraries, dict):
            continue
        for library in libraries.values():
            if not isinstance(library, dict):
                continue
            for key in ("native", "runtimeTargets"):
                native = library.get(key)
                if not isinstance(native, dict):
                    continue
                rewritten = {}
                for relative, metadata in native.items():
                    name = Path(relative).name
                    candidate = macos / name
                    if candidate.is_file() and is_macho(candidate) and not relative.startswith(MACOS_NATIVE):
                        rewritten[MACOS_NATIVE + name] = metadata
                    else:
                        rewritten[relative] = metadata
                library[key] = rewritten
    deps_path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")


def layout(app: Path) -> None:
    macos = app / "Contents" / "MacOS"
    resources = app / "Contents" / "Resources"
    executable = macos / APP_HOST
    if not executable.is_file():
        raise SystemExit(f"Missing {executable}.")
    resources.mkdir(parents=True, exist_ok=True)
    move_managed_files(macos, resources)
    managed = resources / APP_DLL
    deps = resources / "PowerShellSessionRecorder.deps.json"
    config = resources / "PowerShellSessionRecorder.runtimeconfig.json"
    for path in (managed, deps, config):
        if not path.is_file():
            raise SystemExit(f"Missing {path}.")
    bind_host(executable)
    rewrite_native_paths(deps, macos)
    leftover = [path for path in macos.rglob("*") if path.is_file() and not is_macho(path)]
    if leftover:
        raise SystemExit("Contents/MacOS still contains a file that is not Mach-O: " + str(leftover[0]))


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("Usage: layout-bundle.py <App.app>")
    layout(Path(sys.argv[1]))
    print(f"Laid out {sys.argv[1]}.")


if __name__ == "__main__":
    main()
