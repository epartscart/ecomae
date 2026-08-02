#!/usr/bin/env python3
"""Compare captured PHP and ASP.NET catalog status JSON samples (PHP-shaped fields)."""
from __future__ import annotations

import json
import sys
from pathlib import Path

ENVELOPE = ["connected", "message", "status_code", "counts", "source"]
COUNT_FIELDS = ["manufacturers", "models", "modifications", "brands", "vins"]


def main() -> int:
    php_path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("docs/migration/evidence/catalog-status/php-baseline-sample.json")
    aspnet_path = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("docs/migration/evidence/catalog-status/aspnet-output-sample.json")
    php = json.loads(php_path.read_text(encoding="utf-8"))
    aspnet = json.loads(aspnet_path.read_text(encoding="utf-8"))

    failures: list[str] = []
    for field in ENVELOPE:
        if field not in php:
            failures.append(f"php missing {field}")
        if field not in aspnet:
            failures.append(f"aspnet missing {field}")

    php_counts = php.get("counts") if isinstance(php.get("counts"), dict) else {}
    asp_counts = aspnet.get("counts") if isinstance(aspnet.get("counts"), dict) else {}
    for field in COUNT_FIELDS:
        if field not in php_counts:
            failures.append(f"php.counts missing {field}")
        if field not in asp_counts:
            failures.append(f"aspnet.counts missing {field}")

    # Contract-only mode: --contract-only compares shape, not values.
    contract_only = "--contract-only" in sys.argv
    if not contract_only:
        for field in ("connected", "status_code", "source"):
            if php.get(field) != aspnet.get(field):
                failures.append(f"{field} mismatch: php={php.get(field)!r} aspnet={aspnet.get(field)!r}")
        for field in COUNT_FIELDS:
            if php_counts.get(field) != asp_counts.get(field):
                failures.append(
                    f"counts.{field} mismatch: php={php_counts.get(field)!r} aspnet={asp_counts.get(field)!r}"
                )

    if failures:
        print("CATALOG STATUS PARITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    mode = "contract" if contract_only else "value"
    print(f"CATALOG STATUS PARITY PASSED ({mode})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
