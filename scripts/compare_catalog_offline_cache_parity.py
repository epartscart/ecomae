#!/usr/bin/env python3
"""Compare PHP vs ASP.NET offline-cache catalog envelopes (engines/analogs/…).

Success envelope from OfflineCacheOk:
  ok, action, section, rows, source, stale, data

Usage:
  python3 scripts/compare_catalog_offline_cache_parity.py engines php.json aspnet.json
  python3 scripts/compare_catalog_offline_cache_parity.py analogs php.json aspnet.json --contract-only
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

KINDS = {
    "engines",
    "analogs",
    "article-brands",
    "categories",
    "products",
    "engine-search",
    "article-links",
    "article",
    "articles",
    "engine",
}
ENVELOPE = ["ok", "action", "section", "rows", "source", "stale", "data"]


def main() -> int:
    if len(sys.argv) < 4:
        print(
            "Usage: compare_catalog_offline_cache_parity.py <kind> <php.json> <aspnet.json> [--contract-only]\n"
            f"Kinds: {', '.join(sorted(KINDS))}",
            file=sys.stderr,
        )
        return 2

    kind = sys.argv[1].strip().lower()
    if kind not in KINDS:
        print(f"Unknown offline-cache kind: {kind}", file=sys.stderr)
        return 2

    php = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    aspnet = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8"))
    contract_only = "--contract-only" in sys.argv
    failures: list[str] = []

    for field in ENVELOPE:
        if field not in php:
            failures.append(f"php missing {field}")
        if field not in aspnet:
            failures.append(f"aspnet missing {field}")

    if "data" in php and php.get("data") is None:
        failures.append("php.data is null")
    if "data" in aspnet and aspnet.get("data") is None:
        failures.append("aspnet.data is null")

    if not contract_only and not failures:
        for field in ("ok", "action", "section", "source", "stale", "rows"):
            if php.get(field) != aspnet.get(field):
                failures.append(f"{field} mismatch: php={php.get(field)!r} aspnet={aspnet.get(field)!r}")

    if failures:
        print(f"CATALOG OFFLINE-CACHE {kind.upper()} PARITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    mode = "contract" if contract_only else "value"
    print(f"CATALOG OFFLINE-CACHE {kind.upper()} PARITY PASSED ({mode})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
