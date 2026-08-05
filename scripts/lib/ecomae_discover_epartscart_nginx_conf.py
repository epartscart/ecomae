#!/usr/bin/env python3
"""Locate the nginx site conf whose server_name actually serves epartscart.com.

Do NOT assume /etc/nginx/sites-enabled/wildcard-ecomae — on this CloudPanel that
vhost is frequently server_name *.ecomae.com only (industry showcase), which never
receives Host: www.epartscart.com.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

SERVER_NAME_RE = re.compile(r"(?im)^\s*server_name\s+([^;]+);")
EPARTSCART_HOST_RE = re.compile(
    r"(?i)(?:^|[.\s])(?:www\.)?epartscart\.com(?:$|[.\s])"
)


def iter_conf_files() -> list[Path]:
    roots: list[Path] = [
        Path("/etc/nginx/sites-enabled"),
        Path("/etc/nginx/sites-available"),
        Path("/etc/nginx/conf.d"),
    ]
    home = Path("/home")
    if home.is_dir():
        roots.extend(sorted(home.glob("*/conf/nginx")))
    out: list[Path] = []
    seen: set[str] = set()
    for root in roots:
        if not root.is_dir():
            continue
        for p in sorted(root.rglob("*")):
            if not p.is_file():
                continue
            name = p.name
            if name.endswith(".bak") or ".bak." in name:
                continue
            # Prefer real confs; skip editor junk
            if not (
                name.endswith(".conf")
                or name in {"wildcard-ecomae", "default"}
                or "nginx" in str(p.parent)
            ):
                # CloudPanel sometimes uses extensionless site files
                if root.name not in {"sites-enabled", "sites-available"}:
                    continue
            key = str(p.resolve()) if p.exists() else str(p)
            if key in seen:
                continue
            seen.add(key)
            out.append(p)
    return out


def server_names(text: str) -> list[str]:
    names: list[str] = []
    for m in SERVER_NAME_RE.finditer(text):
        for tok in m.group(1).split():
            t = tok.strip()
            if t and t != "_":
                names.append(t)
    return names


def serves_epartscart(names: list[str], text: str) -> bool:
    for n in names:
        if EPARTSCART_HOST_RE.search(n) or n.lower() in {
            "epartscart.com",
            "www.epartscart.com",
            "*.epartscart.com",
        }:
            return True
        # Explicit wildcard for epartscart only
        if n.lower() == "*.epartscart.com":
            return True
    # Content mention alone is NOT enough (comments / host-gates on wrong vhost).
    return False


def discover() -> list[tuple[Path, list[str]]]:
    hits: list[tuple[Path, list[str]]] = []
    for p in iter_conf_files():
        try:
            text = p.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        names = server_names(text)
        if serves_epartscart(names, text):
            hits.append((p, names))
    return hits


def prefer_enabled(hits: list[tuple[Path, list[str]]]) -> Path | None:
    if not hits:
        return None
    enabled = [h for h in hits if "/sites-enabled/" in str(h[0])]
    pool = enabled or hits
    # Prefer basename containing epartscart
    named = [h for h in pool if "epartscart" in h[0].name.lower()]
    if named:
        return named[0][0]
    return pool[0][0]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--print-path",
        action="store_true",
        help="Print only the preferred conf path (exit 1 if none)",
    )
    parser.add_argument(
        "--list",
        action="store_true",
        help="List all matching confs",
    )
    args = parser.parse_args(argv)

    hits = discover()
    if args.print_path:
        path = prefer_enabled(hits)
        if path is None:
            return 1
        print(path)
        return 0

    if not hits:
        print("EPARTSCART_VHOST=missing")
        print(
            "No nginx conf has server_name matching (www.)epartscart.com. "
            "Do NOT use wildcard-ecomae (usually *.ecomae.com only). "
            "Run: bash scripts/cloudpanel_ensure_epartscart_nginx_vhost.sh",
            file=sys.stderr,
        )
        return 1

    preferred = prefer_enabled(hits)
    print(f"EPARTSCART_VHOST={preferred}")
    if args.list or True:
        print("candidates:")
        for path, names in hits:
            mark = " *" if preferred is not None and path == preferred else ""
            print(f"  {path}{mark}")
            print(f"    server_name={names[:24]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
