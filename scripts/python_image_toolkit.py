#!/usr/bin/env python3
"""Image toolkit sidecar (Python/Pillow) for logos + product images.

Batch operations the platform needs but C# does awkwardly:
  trim      — crop transparent/white margins (logo cleanup)
  letterbox — fit whole image inside a square canvas (hero marks, brand slots)
  resize    — cap longest side (web-weight product images)
  favicon   — emit multi-size favicon PNGs (16/32/48/180)

Usage:
  python3 scripts/python_image_toolkit.py trim in.png out.png [--pad 12]
  python3 scripts/python_image_toolkit.py letterbox in.png out.png [--size 512] [--bg white]
  python3 scripts/python_image_toolkit.py resize in.png out.png --max 800
  python3 scripts/python_image_toolkit.py favicon in.png out_dir
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


def _pil():
    try:
        from PIL import Image, ImageChops  # noqa: F401
        return Image, ImageChops
    except ImportError:
        sys.exit("Pillow not installed — `pip install pillow`")


def _bbox(im, bg=(255, 255, 255)):
    """Bounding box of non-background content, tolerant to near-white."""
    from PIL import Image, ImageChops
    if im.mode in ("RGBA", "LA"):
        alpha = im.getchannel("A")
        bbox = alpha.point(lambda a: 255 if a > 8 else 0).getbbox()
        if bbox:
            return bbox
    rgb = im.convert("RGB")
    base = Image.new("RGB", rgb.size, bg)
    diff = ImageChops.difference(rgb, base)
    return diff.point(lambda p: 255 if p > 24 else 0).getbbox()


def cmd_trim(inp: Path, out: Path, pad: int) -> None:
    Image, _ = _pil()
    im = Image.open(inp)
    bbox = _bbox(im)
    if bbox:
        l, t, r, b = bbox
        l, t = max(0, l - pad), max(0, t - pad)
        r, b = min(im.width, r + pad), min(im.height, b + pad)
        im = im.crop((l, t, r, b))
    im.save(out)
    print(f"trim {inp.name} -> {out.name} {im.size}")


def cmd_letterbox(inp: Path, out: Path, size: int, bg: str, pad_pct: float) -> None:
    Image, _ = _pil()
    im = Image.open(inp).convert("RGBA")
    bbox = _bbox(im)
    if bbox:
        im = im.crop(bbox)
    inner = int(size * (1 - pad_pct / 100))
    im.thumbnail((inner, inner), Image.LANCZOS)
    canvas = Image.new("RGBA", (size, size), bg)
    canvas.paste(im, ((size - im.width) // 2, (size - im.height) // 2), im)
    canvas.convert("RGBA").save(out)
    print(f"letterbox {inp.name} -> {out.name} {size}x{size}")


def cmd_resize(inp: Path, out: Path, max_side: int) -> None:
    Image, _ = _pil()
    im = Image.open(inp)
    im.thumbnail((max_side, max_side), Image.LANCZOS)
    im.save(out, optimize=True)
    print(f"resize {inp.name} -> {out.name} {im.size}")


def cmd_favicon(inp: Path, out_dir: Path) -> None:
    Image, _ = _pil()
    im = Image.open(inp).convert("RGBA")
    out_dir.mkdir(parents=True, exist_ok=True)
    for s in (16, 32, 48, 180):
        icon = im.copy()
        icon.thumbnail((s, s), Image.LANCZOS)
        canvas = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        canvas.paste(icon, ((s - icon.width) // 2, (s - icon.height) // 2), icon)
        name = "apple-touch-icon.png" if s == 180 else f"favicon-{s}x{s}.png"
        canvas.save(out_dir / name)
    print(f"favicon set written to {out_dir}")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("trim"); p.add_argument("inp"); p.add_argument("out"); p.add_argument("--pad", type=int, default=12)
    p = sub.add_parser("letterbox"); p.add_argument("inp"); p.add_argument("out"); p.add_argument("--size", type=int, default=512); p.add_argument("--bg", default="white"); p.add_argument("--pad-pct", type=float, default=6)
    p = sub.add_parser("resize"); p.add_argument("inp"); p.add_argument("out"); p.add_argument("--max", type=int, default=800)
    p = sub.add_parser("favicon"); p.add_argument("inp"); p.add_argument("out_dir")
    a = ap.parse_args()
    if a.cmd == "trim":
        cmd_trim(Path(a.inp), Path(a.out), a.pad)
    elif a.cmd == "letterbox":
        cmd_letterbox(Path(a.inp), Path(a.out), a.size, a.bg, a.pad_pct)
    elif a.cmd == "resize":
        cmd_resize(Path(a.inp), Path(a.out), a.max)
    elif a.cmd == "favicon":
        cmd_favicon(Path(a.inp), Path(a.out_dir))
    return 0


if __name__ == "__main__":
    sys.exit(main())
