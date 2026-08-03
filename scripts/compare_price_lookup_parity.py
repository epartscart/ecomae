#!/usr/bin/env python3
"""Compare captured PHP and ASP.NET price lookup JSON samples.

Usage:
  python3 scripts/compare_price_lookup_parity.py
  python3 scripts/compare_price_lookup_parity.py php.json aspnet.json
  python3 scripts/compare_price_lookup_parity.py --contract-only --out compare-result.json

Always emits cutoverAllowed=false when --out is set. Never invents RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ENVELOPE = ["status", "brand", "article", "offers"]
REQUIRED_OFFER_FIELDS = [
    "supplier",
    "brand",
    "article",
    "name",
    "price",
    "currency",
    "stockHint",
    "leadTime",
]


def normalized_offer(offer: dict) -> dict:
    return {field: offer.get(field) for field in REQUIRED_OFFER_FIELDS}


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "php",
        nargs="?",
        type=Path,
        default=Path("docs/migration/evidence/price-lookup/php-baseline-sample.json"),
    )
    ap.add_argument(
        "aspnet",
        nargs="?",
        type=Path,
        default=Path("docs/migration/evidence/price-lookup/aspnet-output-sample.json"),
    )
    ap.add_argument("--contract-only", action="store_true")
    ap.add_argument("--out", type=Path, default=None)
    args = ap.parse_args()

    php = json.loads(args.php.read_text(encoding="utf-8"))
    aspnet = json.loads(args.aspnet.read_text(encoding="utf-8"))

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

    if not args.contract_only and not failures:
        for field in ["status", "brand", "article"]:
            if php.get(field) != aspnet.get(field):
                failures.append(
                    f"{field} mismatch: php={php.get(field)!r} aspnet={aspnet.get(field)!r}"
                )
        php_norm = [normalized_offer(o) for o in (php_offers or []) if isinstance(o, dict)]
        asp_norm = [normalized_offer(o) for o in (asp_offers or []) if isinstance(o, dict)]
        if php_norm != asp_norm:
            failures.append("offer list mismatch")

    ok = not failures
    out = {
        "role": "compare-result",
        "ok": ok,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "contractOnly": bool(args.contract_only),
        "offerCount": len(php_offers or []) if ok else 0,
        "errors": failures,
        "note": "Price lookup exact-route parity floor. Never invents RELEASE_OWNER_APPROVAL.md.",
    }
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("PRICE LOOKUP PARITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    if args.contract_only:
        print("PRICE LOOKUP PARITY PASSED (contract)")
    else:
        print(f"PRICE LOOKUP PARITY PASSED: {len(php_offers or [])} offer(s) matched")
    print(
        f"PASS: ok=true offerCount={len(php_offers or [])} cutoverAllowed=false",
        file=sys.stderr,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
