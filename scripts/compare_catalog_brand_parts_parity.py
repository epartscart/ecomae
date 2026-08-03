#!/usr/bin/env python3
"""Compare PHP vs ASP.NET catalog brand-parts envelopes.

Envelope: ok, brand, rows, source, data, message

Usage:
  python3 scripts/compare_catalog_brand_parts_parity.py php.json aspnet.json [--contract-only]
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ENVELOPE = ["ok", "brand", "rows", "source", "data", "message"]


def main() -> int:
    if len(sys.argv) < 3:
        print(
            "Usage: compare_catalog_brand_parts_parity.py <php.json> <aspnet.json> [--contract-only]",
            file=sys.stderr,
        )
        return 2

    php = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    aspnet = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    contract_only = "--contract-only" in sys.argv
    failures: list[str] = []

    for field in ENVELOPE:
        if field not in php:
            failures.append(f"php missing {field}")
        if field not in aspnet:
            failures.append(f"aspnet missing {field}")

    if not isinstance(php.get("data"), list):
        failures.append("php.data must be a list")
    if not isinstance(aspnet.get("data"), list):
        failures.append("aspnet.data must be a list")

    if not contract_only and not failures:
        for field in ("ok", "brand", "source", "rows"):
            if php.get(field) != aspnet.get(field):
                failures.append(f"{field} mismatch: php={php.get(field)!r} aspnet={aspnet.get(field)!r}")
        php_data = php.get("data") if isinstance(php.get("data"), list) else []
        asp_data = aspnet.get("data") if isinstance(aspnet.get("data"), list) else []
        if len(php_data) != len(asp_data):
            failures.append(f"data length mismatch: php={len(php_data)} aspnet={len(asp_data)}")

    if failures:
        print("CATALOG BRAND-PARTS PARITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    mode = "contract" if contract_only else "value"
    print(f"CATALOG BRAND-PARTS PARITY PASSED ({mode})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
