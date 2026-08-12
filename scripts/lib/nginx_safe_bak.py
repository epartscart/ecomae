#!/usr/bin/env python3
"""Safe nginx conf backups — never append .bak onto already-backed names (ENAMETOOLONG)."""
from __future__ import annotations

import time
from pathlib import Path

MAX_NAME = 180


def _writable_dir(preferred: str, fallback: str) -> Path:
    for candidate in (Path(preferred), Path(fallback)):
        try:
            candidate.mkdir(parents=True, exist_ok=True)
            probe = candidate / ".write_probe"
            probe.write_text("ok", encoding="utf-8")
            probe.unlink(missing_ok=True)
            return candidate
        except OSError:
            continue
    return Path(fallback)


def bak_dir() -> Path:
    return _writable_dir("/root/nginx-bak", "/tmp/nginx-bak")


def prune_dir() -> Path:
    return _writable_dir("/root/nginx-bak-prune", "/tmp/nginx-bak-prune")


def is_bak_litter(path: Path) -> bool:
    name = path.name.lower()
    return ".bak" in name or name.endswith(".disabled") or name.endswith("~")


def conf_base_name(path: Path) -> str:
    """www.ecomae.com.conf.bak.foo… → www.ecomae.com.conf"""
    name = path.name
    if ".bak" in name:
        name = name.split(".bak", 1)[0]
    if not name.endswith(".conf"):
        name = name + ".conf"
    return name


def safe_bak_path(conf: Path, tag: str, stamp: str | None = None) -> Path:
    stamp = stamp or time.strftime("%Y%m%d%H%M%S")
    tag = "".join(c if c.isalnum() or c in "-_" else "-" for c in (tag or "bak"))[:40]
    base = conf_base_name(conf)
    short = f"{base}.{tag}.{stamp}.bak"
    if len(short) > MAX_NAME:
        stem = base[: max(20, MAX_NAME - len(tag) - len(stamp) - 10)]
        short = f"{stem}.{tag}.{stamp}.bak"
    return bak_dir() / short


def prune_sites_enabled_bak_litter(max_path_len: int = 160) -> int:
    """Move absurdly long *.bak* files out of sites-enabled/conf.d."""
    moved = 0
    dest_root = prune_dir()
    for base in (Path("/etc/nginx/sites-enabled"), Path("/etc/nginx/conf.d")):
        if not base.is_dir():
            continue
        for conf in base.iterdir():
            if not conf.is_file():
                continue
            if not (is_bak_litter(conf) or len(str(conf)) > max_path_len):
                continue
            target = dest_root / f"{conf_base_name(conf)}.{int(time.time())}.{moved}.bak"
            try:
                conf.rename(target)
                moved += 1
            except OSError:
                try:
                    conf.unlink()
                    moved += 1
                except OSError:
                    pass
    return moved


if __name__ == "__main__":
    import sys

    if len(sys.argv) >= 2 and sys.argv[1] == "prune":
        n = prune_sites_enabled_bak_litter()
        print(f"pruned={n}")
        raise SystemExit(0)
    print("usage: nginx_safe_bak.py prune")
