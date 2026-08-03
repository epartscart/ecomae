#!/usr/bin/env python3
"""Compare captured PHP and ASP.NET price lookup JSON samples.

Usage:
  python3 scripts/compare_price_lookup_parity.py php.json aspnet.json
  python3 scripts/compare_price_lookup_parity.py php.json aspnet.json --contract-only
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ENVELOPE = ["status", "brand", "article", "offers"]
REQUIRED_OFFER_FIELDS = ["supplier", "brand", "article", "name", "price", "currency", "stockHint", "leadTime"]


def normalized_offer(offer: dict) -> dict:
    return {field: offer.get(field) for field in REQUIRED_OFFER_FIELDS}


def main() -> int:
    args = [a for a in sys.argv[1:] if a != "--contract-only"]
    contract_only = "--contract-only" in sys.argv
    php_path = Path(args[0]) if len(args) > 0 else Path("docs/migration/evidence/price-lookup/php-baseline-sample.json")
    aspnet_path = Path(args[1]) if len(args) > 1 else Path("docs/migration/evidence/price-lookup/aspnet-output-sample.json")
    php = json.loads(php_path.read_text(encoding="utf-8"))
    aspnet = json.loads(aspnet_path.read_text(encoding="utf-8"))

    failures: list[str] = []
    for field in ENVELOPE:
        if field not in php:
            failures.append(f"php missing {field}")
        if field not in aspnet:
            failures.append(f"aspnet missing {field}")

    php_offers = php.get("offers") if isinstance(php.get("offers"), list) else None
    asp_offers = aspnet.get("offers") if isinstance(aspnet.get("offers"), list) else None
    if php_offers is None:
        failures.append("php.offers must be a list")
    if asp_offers is None:
        failures.append("aspnet.offers must be a list")

    if php_offers is not None and asp_offers is not None:
        for side, offers in (("php", php_offers), ("aspnet", asp_offers)):
            if not offers:
                continue
            first = offers[0]
            if not isinstance(first, dict):
                failures.append(f"{side}.offers[0] must be an object")
                continue
            for field in REQUIRED_OFFER_FIELDS:
                if field not in first:
                    failures.append(f"{side}.offers[0] missing {field}")

    if not contract_only and not failures:
        for field in ["status", "brand", "article"]:
            if php.get(field) != aspnet.get(field):
                failures.append(f"{field} mismatch: php={php.get(field)!r} aspnet={aspnet.get(field)!r}")
        php_norm = [normalized_offer(o) for o in (php_offers or []) if isinstance(o, dict)]
        asp_norm = [normalized_offer(o) for o in (asp_offers or []) if isinstance(o, dict)]
        if php_norm != asp_norm:
            failures.append("offer list mismatch")

    if failures:
        print("PRICE LOOKUP PARITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    if contract_only:
        print("PRICE LOOKUP PARITY PASSED (contract)")
    else:
        print(f"PRICE LOOKUP PARITY PASSED: {len(php_offers or [])} offer(s) matched")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
