#!/usr/bin/env python3
"""Compare captured PHP and ASP.NET price lookup JSON samples."""
from __future__ import annotations

import json
import sys
from pathlib import Path

REQUIRED_OFFER_FIELDS = ["supplier", "brand", "article", "name", "price", "currency", "stockHint", "leadTime"]


def normalized_offer(offer: dict) -> dict:
    return {field: offer.get(field) for field in REQUIRED_OFFER_FIELDS}


def main() -> int:
    php_path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("docs/migration/evidence/price-lookup/php-baseline-sample.json")
    aspnet_path = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("docs/migration/evidence/price-lookup/aspnet-output-sample.json")
    php = json.loads(php_path.read_text())
    aspnet = json.loads(aspnet_path.read_text())

    failures: list[str] = []
    for field in ["status", "brand", "article"]:
        if php.get(field) != aspnet.get(field):
            failures.append(f"{field} mismatch: php={php.get(field)!r} aspnet={aspnet.get(field)!r}")

    php_offers = [normalized_offer(offer) for offer in php.get("offers", [])]
    aspnet_offers = [normalized_offer(offer) for offer in aspnet.get("offers", [])]
    if php_offers != aspnet_offers:
        failures.append("offer list mismatch")

    if failures:
        print("PRICE LOOKUP PARITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print(f"PRICE LOOKUP PARITY PASSED: {len(php_offers)} offer(s) matched")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
