"""Export unchanged merged RGBA pixels from this project's PSD sources to PNG.

This is a native-format export, not image generation or an artwork edit.
The sources are 8-bit RGB PSD files with uncompressed composite channels.
"""

from pathlib import Path
import struct
import zlib


ROOT = Path(__file__).resolve().parents[2]
SPRITES = ROOT / "Assets/_Project/Sprites/Generated"


def chunk(kind, payload):
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)


def export(source, target):
    data = source.read_bytes()
    signature, version, channels, height, width, depth, mode = struct.unpack(">4sH6xHIIHH", data[:26])
    if (signature, version, channels, depth, mode) != (b"8BPS", 1, 4, 8, 3):
        raise ValueError(f"Unsupported PSD composite: {source.name}")
    offset = 26
    for _ in range(3):
        length = struct.unpack_from(">I", data, offset)[0]
        offset += 4 + length
    compression = struct.unpack_from(">H", data, offset)[0]
    if compression != 0:
        raise ValueError(f"Expected raw composite channels: {source.name}")
    offset += 2
    plane_size = width * height
    planes = [data[offset + i * plane_size:offset + (i + 1) * plane_size] for i in range(4)]
    if any(len(plane) != plane_size for plane in planes):
        raise ValueError(f"Truncated composite: {source.name}")
    rows = bytearray()
    for y in range(height):
        rows.append(0)  # PNG scanline filter: none.
        for x in range(width):
            pixel = y * width + x
            rows.extend(plane[pixel] for plane in planes)
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(rows), 9))
    png += chunk(b"IEND", b"")
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists() and target.read_bytes() != png:
        raise FileExistsError(f"Refusing to overwrite edited artwork: {target}")
    target.write_bytes(png)
    print(f"{source.name} -> {target.relative_to(ROOT).as_posix()} ({width}x{height})")


if __name__ == "__main__":
    for source in sorted((ROOT / "Assets/_Project/PSD").glob("*.psd")):
        folder = "Characters" if source.stem == "Player" else "Monsters"
        name = "Oni" if source.stem == "Horned" else source.stem
        export(source, SPRITES / folder / f"{name}.png")
