#!/usr/bin/env python3
"""Create an uncompressed UDIF disk image around an ISO 9660 filesystem.

macOS CI uses hdiutil instead. This writer is for hosts that do not have
hdiutil, including Ubuntu. The image is a whole-disk UDIF file whose data
fork is a Rock Ridge ISO, so Finder can mount it without an Apple signature.
It is not notarized and it does not change Gatekeeper.
"""

from __future__ import annotations

import base64
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

SECTOR = 512
ISO_VOLUME = "PSRECORDER"


def checksum() -> bytes:
    return struct.pack(">II", 0, 0) + bytes(128)


def mish(sector_count: int, data_length: int) -> bytes:
    table = struct.pack(">IIQQQ", 0x6D697368, 1, 0, sector_count, 0)
    table += struct.pack(">IIIIIIII", 0, 2, 0, 0, 0, 0, 0, 0)
    table += checksum()
    table += struct.pack(">I", 2)
    table += struct.pack(">IIQQQQ", 1, 0, 0, sector_count, 0, data_length)
    table += struct.pack(">IIQQQQ", 0xFFFFFFFF, 0, sector_count, 0, 0, 0)
    return table


def koly(data_length: int, xml_offset: int, xml_length: int, sector_count: int) -> bytes:
    blob = struct.pack(">4sIII", b"koly", 4, 512, 1)
    blob += struct.pack(">QQQQQ", 0, 0, data_length, 0, 0)
    blob += struct.pack(">II", 1, 1)
    blob += bytes(16)
    blob += checksum()
    blob += struct.pack(">QQ", xml_offset, xml_length)
    blob += bytes(120)
    blob += checksum()
    blob += struct.pack(">IQ", 1, sector_count)
    blob += struct.pack(">III", 0, 0, 0)
    if len(blob) != 512:
        raise SystemExit(f"koly block is {len(blob)} bytes.")
    return blob


def plist(table: bytes) -> bytes:
    encoded = base64.b64encode(table).decode("ascii")
    wrapped = "\n".join(encoded[index:index + 68] for index in range(0, len(encoded), 68))
    text = f"""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
<key>resource-fork</key>
<dict>
<key>blkx</key>
<array>
<dict>
<key>Attributes</key>
<string>0x0050</string>
<key>CFName</key>
<string>{ISO_VOLUME}</string>
<key>Data</key>
<data>
{wrapped}
</data>
<key>ID</key>
<string>0</string>
<key>Name</key>
<string>{ISO_VOLUME}</string>
</dict>
</array>
</dict>
</dict>
</plist>
"""
    return text.encode("utf-8")


def iso_from(stage: Path) -> bytes:
    with tempfile.TemporaryDirectory(prefix="psr-iso-") as temporary:
        image = Path(temporary) / "volume.iso"
        completed = subprocess.run(
            [
                "xorriso",
                "-as",
                "mkisofs",
                "-R",
                "-iso-level",
                "3",
                "-V",
                ISO_VOLUME,
                "-o",
                str(image),
                str(stage),
            ],
            check=False,
            capture_output=True,
            text=True,
        )
        if completed.returncode != 0:
            raise SystemExit(completed.stderr.strip() or "xorriso failed.")
        data = image.read_bytes()
    padded = (len(data) + SECTOR - 1) // SECTOR * SECTOR
    return data.ljust(padded, b"\0")


def write_image(stage: Path, output: Path) -> None:
    data = iso_from(stage)
    if data[0x8001:0x8006] != b"CD001":
        raise SystemExit("The filesystem image is not an ISO 9660 volume.")
    sectors = len(data) // SECTOR
    table = mish(sectors, len(data))
    xml = plist(table)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(data + xml + koly(len(data), len(data), len(xml), sectors))


def check_image(path: Path) -> None:
    data = path.read_bytes()
    if len(data) < 512 or data[-512:-508] != b"koly":
        raise SystemExit(f"{path} has no koly trailer.")
    header = data[-512:]
    version, header_size = struct.unpack_from(">II", header, 4)
    data_offset, data_length = struct.unpack_from(">QQ", header, 24)
    xml_offset, xml_length = struct.unpack_from(">QQ", header, 216)
    sector_count = struct.unpack_from(">Q", header, 492)[0]
    if version != 4 or header_size != 512:
        raise SystemExit(f"{path} has an unexpected koly header.")
    if data_offset != 0 or data_length < 0x8006 or data_offset + data_length > len(data) - 512:
        raise SystemExit(f"{path} has an invalid data fork.")
    if data[0x8001:0x8006] != b"CD001":
        raise SystemExit(f"{path} does not contain an ISO 9660 filesystem.")
    if xml_offset != data_length or xml_offset + xml_length + 512 != len(data):
        raise SystemExit(f"{path} XML resource is not between the data fork and koly.")
    if sector_count != data_length // SECTOR:
        raise SystemExit(f"{path} sector count does not match the data fork.")
    print(f"Checked {path} ({sector_count} sectors).")


def main() -> None:
    if len(sys.argv) == 3 and sys.argv[1] == "--check":
        check_image(Path(sys.argv[2]))
        return
    if len(sys.argv) != 3:
        raise SystemExit("Usage: write-dmg.py <stage-directory> <output.dmg> | write-dmg.py --check <output.dmg>")
    write_image(Path(sys.argv[1]), Path(sys.argv[2]))


if __name__ == "__main__":
    main()
