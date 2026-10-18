"""Generates Tally's PWA icons (stdlib only): a tally-mark glyph on the brand green.

Usage: python3 -I tools/icons/make_icons.py web/public
"""
import math
import os
import struct
import sys
import zlib

GREEN = (31, 111, 92)
WHITE = (255, 255, 255)
SS = 4  # supersampling factor for anti-aliasing


def png(path, size, pixels):
    raw = b"".join(b"\x00" + bytes(c for px in row for c in px) for row in pixels)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def inside_round_rect(x, y, size, radius):
    cx = min(max(x, radius), size - radius)
    cy = min(max(y, radius), size - radius)
    return (x - cx) ** 2 + (y - cy) ** 2 <= radius ** 2


def dist_to_segment(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def glyph_segments(size, scale):
    """Four upright strokes and one diagonal, centred, occupying `scale` of the icon."""
    span = size * scale
    left = (size - span) / 2
    top = size / 2 - span * 0.36
    bottom = size / 2 + span * 0.36
    gap = span / 4.6
    xs = [left + span * 0.16 + i * gap for i in range(4)]
    strokes = [(x, top, x, bottom) for x in xs]
    strokes.append((left + span * 0.02, bottom - span * 0.08, left + span * 0.98, top + span * 0.08))
    return strokes, span * 0.075


def render(size, glyph_scale, rounded):
    big = size * SS
    strokes, half_width = glyph_segments(big, glyph_scale)
    radius = big * 0.22 if rounded else 0
    rows = []
    for y in range(size):
        row = []
        for x in range(size):
            bg = fg = 0
            for sy in range(SS):
                for sx in range(SS):
                    px, py = x * SS + sx + 0.5, y * SS + sy + 0.5
                    if rounded and not inside_round_rect(px, py, big, radius):
                        continue
                    bg += 1
                    if any(dist_to_segment(px, py, *s) <= half_width for s in strokes):
                        fg += 1
            n = SS * SS
            alpha = bg / n
            if bg == 0:
                row.append((0, 0, 0, 0))
                continue
            mix = fg / bg
            colour = tuple(round(GREEN[i] * (1 - mix) + WHITE[i] * mix) for i in range(3))
            row.append(colour + (round(alpha * 255),))
        rows.append(row)
    return rows


def main(public_dir):
    icons = os.path.join(public_dir, "icons")
    os.makedirs(icons, exist_ok=True)
    for name, size, scale, rounded in [
        ("icon-192.png", 192, 0.62, True),
        ("icon-512.png", 512, 0.62, True),
        ("maskable-512.png", 512, 0.48, False),  # glyph inside the 80% safe zone
    ]:
        png(os.path.join(icons, name), size, render(size, scale, rounded))
    # iOS ignores transparency and applies its own mask: full-bleed square.
    png(os.path.join(public_dir, "apple-touch-icon.png"), 180, render(180, 0.58, False))
    print("icons written to", public_dir)


if __name__ == "__main__":
    main(sys.argv[1])
