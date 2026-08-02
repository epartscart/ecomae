#!/usr/bin/env python3
"""Compare PHP vs ASP.NET catalog list JSON envelopes (manufacturers/models/modifications/brands).

Usage:
  python3 scripts/compare_catalog_list_parity.py manufacturers php.json aspnet.json
  python3 scripts/compare_catalog_list_parity.py models php.json aspnet.json --contract-only
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ENVELOPES: dict[str, list[str]] = {
    "manufacturers": ["ok", "section", "rows", "source", "data", "message"],
    "models": ["ok", "action", "section", "rows", "source", "data", "message"],
    "modifications": ["ok", "action", "section", "rows", "source", "data", "message"],
    "brands": ["ok", "action", "section", "rows", "source", "data", "message"],
}


def main() -> int:
    if len(sys.argv) < 4:
        print(
            "Usage: compare_catalog_list_parity.py <manufacturers|models|modifications|brands> "
            "<php.json> <aspnet.json> [--contract-only]",
            file=sys.stderr,
        )
        return 2

    kind = sys.argv[1].strip().lower()
    if kind not in ENVELOPES:
        print(f"Unknown catalog list kind: {kind}", file=sys.stderr)
        return 2

    php = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    aspnet = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8"))
    contract_only = "--contract-only" in sys.argv
    required = ENVELOPES[kind]
    failures: list[str] = []

    for field in required:
        if field not in php:
            failures.append(f"php missing {field}")
        if field not in aspnet:
            failures.append(f"aspnet missing {field}")

    if not isinstance(php.get("data"), list):
        failures.append("php.data must be a list")
    if not isinstance(aspnet.get("data"), list):
        failures.append("aspnet.data must be a list")

    if not contract_only and not failures:
        for field in ("ok", "section", "source"):
            if field in required and php.get(field) != aspnet.get(field):
                failures.append(f"{field} mismatch: php={php.get(field)!r} aspnet={aspnet.get(field)!r}")
        if php.get("rows") != aspnet.get("rows"):
            failures.append(f"rows mismatch: php={php.get('rows')!r} aspnet={aspnet.get('rows')!r}")
        # Value mode also requires equal list lengths; deep item compare is operator-reviewed.
        php_data = php.get("data") if isinstance(php.get("data"), list) else []
        asp_data = aspnet.get("data") if isinstance(aspnet.get("data"), list) else []
        if len(php_data) != len(asp_data):
            failures.append(f"data length mismatch: php={len(php_data)} aspnet={len(asp_data)}")

    if failures:
        print(f"CATALOG {kind.upper()} PARITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    mode = "contract" if contract_only else "value"
    print(f"CATALOG {kind.upper()} PARITY PASSED ({mode})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
