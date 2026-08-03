#!/usr/bin/env python3
"""Compare PHP vs ASP.NET catalog VIN decode envelopes.

Success payload fields:
  ok, source, stale, vin, language, region, vehicle_count, payload

Usage:
  python3 scripts/compare_catalog_vin_parity.py php.json aspnet.json
  python3 scripts/compare_catalog_vin_parity.py php.json aspnet.json --contract-only
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ENVELOPE = ["ok", "source", "stale", "vin", "language", "region", "vehicle_count", "payload"]


def main() -> int:
    if len(sys.argv) < 3:
        print(
            "Usage: compare_catalog_vin_parity.py <php.json> <aspnet.json> [--contract-only]",
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

    if not contract_only and not failures:
        for field in ("ok", "source", "stale", "vin", "language", "region", "vehicle_count"):
            if php.get(field) != aspnet.get(field):
                failures.append(f"{field} mismatch: php={php.get(field)!r} aspnet={aspnet.get(field)!r}")

    if failures:
        print("CATALOG VIN PARITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    mode = "contract" if contract_only else "value"
    print(f"CATALOG VIN PARITY PASSED ({mode})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
