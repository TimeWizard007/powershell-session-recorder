#!/usr/bin/env python3
"""Derive icon files from assets/branding/app-icon-source.png.

The source image is never modified. Resampling uses Lanczos and keeps the
alpha channel.
"""

from __future__ import annotations

import io
import struct
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "assets" / "branding" / "app-icon-source.png"
LINUX_SIZES = (16, 22, 24, 32, 48, 64, 128, 256, 512)
ICO_SIZES = (16, 24, 32, 48, 64, 128, 256)


def resized(source: Image.Image, size: int) -> Image.Image:
    return source.resize((size, size), Image.Resampling.LANCZOS)


def write_png(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="PNG", optimize=True)


def write_ico(images: list[Image.Image], path: Path) -> None:
    blobs = []
    for image in images:
        buffer = io.BytesIO()
        image.save(buffer, format="PNG", optimize=True)
        blobs.append(buffer.getvalue())

    header = struct.pack("<HHH", 0, 1, len(blobs))
    entries = bytearray()
    offset = 6 + 16 * len(blobs)
    payload = bytearray()
    for image, blob in zip(images, blobs, strict=True):
        width = 0 if image.width >= 256 else image.width
        height = 0 if image.height >= 256 else image.height
        entries.extend(struct.pack("<BBBBHHII", width, height, 0, 0, 1, 32, len(blob), offset))
        payload.extend(blob)
        offset += len(blob)

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(header + entries + payload)


def main() -> None:
    if not SOURCE.is_file():
        raise SystemExit(f"Approved icon source is missing: {SOURCE}")

    with Image.open(SOURCE) as opened:
        source = opened.convert("RGBA")
        if source.size != (1254, 1254):
            raise SystemExit(f"Unexpected source size: {source.size}")

    for size in LINUX_SIZES:
        destination = (
            ROOT
            / "packaging"
            / "ubuntu"
            / "icons"
            / "hicolor"
            / f"{size}x{size}"
            / "apps"
            / "powershell-session-recorder.png"
        )
        write_png(resized(source, size), destination)

    write_png(
        resized(source, 256),
        ROOT / "src" / "PowerShellSessionRecorder.App" / "Assets" / "app-icon.png",
    )
    write_png(
        resized(source, 1024),
        ROOT / "assets" / "branding" / "macos" / "app-icon-1024.png",
    )
    write_ico(
        [resized(source, size) for size in ICO_SIZES],
        ROOT / "assets" / "branding" / "app-icon.ico",
    )


if __name__ == "__main__":
    main()
