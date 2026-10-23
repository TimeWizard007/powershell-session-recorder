#!/usr/bin/env python3
"""Require every PNG image from the approved .ico to be present in a binary."""

import struct
import sys
from pathlib import Path


def png_images(ico: bytes) -> list[bytes]:
    if ico[:4] != b"\x00\x00\x01\x00":
        raise ValueError("Icon file is not an ICO.")

    count = struct.unpack_from("<H", ico, 4)[0]
    images = []
    for index in range(count):
        offset = 6 + index * 16
        size, image_offset = struct.unpack_from("<II", ico, offset + 8)
        blob = ico[image_offset:image_offset + size]
        if not blob.startswith(b"\x89PNG\r\n\x1a\n"):
            raise ValueError(f"Icon image {index} is not a PNG.")
        images.append(blob)
    return images


def main() -> int:
    if len(sys.argv) < 3:
        print("Usage: assert-icon-resource.py <app-icon.ico> <binary>...", file=sys.stderr)
        return 1

    images = png_images(Path(sys.argv[1]).read_bytes())
    if len(images) != 7:
        print(f"Expected 7 icon images, found {len(images)}.", file=sys.stderr)
        return 1

    failed = False
    for target in sys.argv[2:]:
        data = Path(target).read_bytes()
        missing = [str(index) for index, image in enumerate(images) if image not in data]
        if missing:
            print(f"{target} is missing icon images {', '.join(missing)}.", file=sys.stderr)
            failed = True
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
