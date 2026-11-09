"""Offline lossless RGBA PNG optimization; never used by the game or build.

Python 3 + Pillow. Keeps IHDR and all ancillary chunks byte-for-byte, replacing
only IDAT when smaller. Checks CRCs, decompressed pixels and Pillow's RGBA bytes.
Run after slice-art.ps1 / export-farm-art.ps1 to recompress regenerated textures.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import struct
import zlib

from PIL import Image

SIGNATURE = b"\x89PNG\r\n\x1a\n"


def chunks(data):
    if not data.startswith(SIGNATURE):
        raise ValueError("Invalid PNG signature")
    result, offset = [], 8
    while offset < len(data):
        length = struct.unpack_from(">I", data, offset)[0]
        kind = data[offset + 4:offset + 8]
        payload = data[offset + 8:offset + 8 + length]
        end = offset + length + 12
        crc = struct.unpack_from(">I", data, end - 4)[0]
        if zlib.crc32(kind + payload) & 0xFFFFFFFF != crc:
            raise ValueError("Invalid PNG CRC")
        result.append((kind, payload, data[offset:end]))
        offset = end
    if offset != len(data) or result[-1][0] != b"IEND":
        raise ValueError("Invalid PNG structure")
    return result


def idat(parts):
    return b"".join(payload for kind, payload, _ in parts if kind == b"IDAT")


def chunk(kind, payload):
    return (struct.pack(">I", len(payload)) + kind + payload
            + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF))


def pixels(parts):
    width, height, depth, color, compression, filtering, interlace = struct.unpack(
        ">IIBBBBB", parts[0][1])
    if (depth, color, compression, filtering, interlace) != (8, 6, 0, 0, 0):
        raise ValueError("Only non-interlaced RGBA8 textures are supported")
    raw = zlib.decompress(idat(parts))
    stride = width * 4
    if len(raw) != height * (stride + 1):
        raise ValueError("Invalid PNG scanline length")
    output = bytearray()
    previous = bytearray(stride)
    for y in range(height):
        start = y * (stride + 1)
        mode = raw[start]
        row = bytearray(raw[start + 1:start + 1 + stride])
        if mode not in range(5):
            raise ValueError("Invalid PNG filter")
        for x in range(stride):
            left = row[x - 4] if x >= 4 else 0
            up, upper_left = previous[x], previous[x - 4] if x >= 4 else 0
            if mode == 1:
                predictor = left
            elif mode == 2:
                predictor = up
            elif mode == 3:
                predictor = (left + up) // 2
            elif mode == 4:
                p = left + up - upper_left
                a, b, c = abs(p - left), abs(p - up), abs(p - upper_left)
                predictor = left if a <= b and a <= c else up if b <= c else upper_left
            else:
                predictor = 0
            row[x] = (row[x] + predictor) & 255
        output.extend(row)
        previous = row
    return bytes(output)


def optimize(path):
    original = path.read_bytes()
    parts = chunks(original)
    before_pixels = pixels(parts)
    compressed = idat(parts)
    scanlines = zlib.decompress(compressed)
    with Image.open(io.BytesIO(original)) as image:
        image.load()
        if image.mode != "RGBA" or image.tobytes() != before_pixels:
            raise ValueError("Independent PNG decoder mismatch")
        width, height = image.size
        temporary = io.BytesIO()
        image.save(temporary, format="PNG", optimize=True, compress_level=9)
    alternate = chunks(temporary.getvalue())
    if alternate[0][1] != parts[0][1] or pixels(alternate) != before_pixels:
        raise ValueError("Re-encoding changed image format or pixels")
    candidates = [compressed, idat(alternate)]
    for raw in (scanlines, zlib.decompress(idat(alternate))):
        for strategy in (zlib.Z_DEFAULT_STRATEGY, zlib.Z_FILTERED, zlib.Z_RLE):
            encoder = zlib.compressobj(9, zlib.DEFLATED, 15, 9, strategy)
            candidates.append(encoder.compress(raw) + encoder.flush())
    best = min(candidates, key=len)
    rewritten, written = bytearray(SIGNATURE), False
    for kind, _, serialized in parts:
        if kind == b"IDAT":
            if not written:
                rewritten.extend(chunk(kind, best))
                written = True
        else:
            rewritten.extend(serialized)
    result = bytes(rewritten) if len(rewritten) < len(original) else original
    final_parts = chunks(result)
    if ([(k, d) for k, d, _ in final_parts if k != b"IDAT"]
            != [(k, d) for k, d, _ in parts if k != b"IDAT"]):
        raise ValueError("Non-IDAT PNG chunks changed")
    if pixels(final_parts) != before_pixels:
        raise ValueError("Pixel changes after optimization")
    with Image.open(io.BytesIO(result)) as check:
        if check.tobytes() != before_pixels:
            raise ValueError("Independent final PNG decode mismatch")
    if result != original:
        path.write_bytes(result)
    return dict(file=path.name, width=width, height=height,
                before_bytes=len(original), after_bytes=len(result),
                saved_bytes=len(original) - len(result),
                rgba_sha256=hashlib.sha256(before_pixels).hexdigest(),
                sha256=hashlib.sha256(result).hexdigest(), pixels_identical=True,
                ancillary_chunks_identical=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--items", type=Path, default=Path(__file__).parent / "Items")
    parser.add_argument("--report", type=Path)
    parser.add_argument("--update-farm-metadata", action="store_true")
    args = parser.parse_args()
    rows = [optimize(path) for path in sorted(args.items.glob("*.png"))]
    if not rows:
        raise ValueError("No PNG textures found")
    if args.update_farm_metadata:
        metadata = Path(__file__).parent.parent / "Design/farm-art-exports.json"
        entries = json.loads(metadata.read_text("utf-8-sig"))
        by_name = {row["file"]: row for row in rows}
        for entry in entries:
            row = by_name[entry["key"] + ".png"]
            entry.update(bytes=row["after_bytes"], sha256=row["sha256"].upper())
        metadata.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + "\n", "utf-8")
    report = dict(files=len(rows), before_bytes=sum(r["before_bytes"] for r in rows),
                  after_bytes=sum(r["after_bytes"] for r in rows), images=rows)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", "utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "images"}))


if __name__ == "__main__":
    main()
