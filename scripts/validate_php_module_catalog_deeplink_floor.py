#!/usr/bin/env python3
"""Lock full PHP module catalog hrefs as hybrid deeplink-safe targets.

Never invents RELEASE_OWNER_APPROVAL.md. cutoverAllowed stays false.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from urllib.parse import urlparse

FLOORS = {
    "erpAreas": 35,
    "erpTabs": 154,
    "erpCategories": 9,
    "bosSections": 11,
    "bosModules": 99,
    "cpBrochureFeatures": 405,
    "storefrontSurfaces": 12,
}


def is_allowed(href: str) -> bool:
    value = (href or "").strip()
    if not value:
        return False
    lowered = value.lower()
    if lowered.startswith(("javascript:", "data:", "vbscript:")):
        return False
    if lowered.startswith(("http://", "https://")):
        host = (urlparse(value).hostname or "").lower()
        return (
            host == "epartscart.com"
            or host.endswith(".epartscart.com")
            or host == "www.ecomae.com"
            or host.endswith(".ecomae.com")
        )
    if not value.startswith("/"):
        return False
    if value.startswith(("/cp/", "/erp/", "/bos/", "/storefront/")):
        return False
    if lowered.startswith(("/migration/", "/auth/", "/api/")):
        return False
    return (
        value.upper().startswith("/CP")
        or value.upper().startswith("/ERP")
        or value.upper().startswith("/BOS")
        or value.startswith("/shop/")
        or value.startswith("/content/")
        or value.lower().endswith(".php")
        or value == "/"
    )


def collect_hrefs(catalog: dict) -> list[tuple[str, str, str]]:
    rows: list[tuple[str, str, str]] = []
    for area in catalog.get("erpAreas") or []:
        rows.append(("erp-area", str(area.get("id")), str(area.get("href") or "")))
        for tab in area.get("tabs") or []:
            rows.append(
                (
                    "erp-tab",
                    f"{area.get('id')}/{tab.get('id')}",
                    str(tab.get("href") or ""),
                )
            )
    for cat in catalog.get("erpCategories") or []:
        rows.append(("erp-category", str(cat.get("id")), str(cat.get("href") or "")))
    for sec in catalog.get("bosSections") or []:
        rows.append(
            (
                "bos-section",
                str(sec.get("id")),
                str(sec.get("href") or f"/BOS/?section={sec.get('id')}"),
            )
        )
    for bos in catalog.get("bosModules") or []:
        rows.append(("bos-module", str(bos.get("id")), str(bos.get("href") or "")))
    for cp in catalog.get("cpBrochureFeatures") or []:
        rows.append(("cp-feature", str(cp.get("id")), str(cp.get("href") or "")))
    for sf in catalog.get("storefrontSurfaces") or []:
        rows.append(("storefront-surface", str(sf.get("id")), str(sf.get("href") or "")))
    return rows


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--catalog",
        type=Path,
        default=Path("aspnet/src/EcomAE.Platform/Presentation/Generated/php_module_catalog.json"),
    )
    ap.add_argument(
        "--evidence-out",
        type=Path,
        default=Path("docs/migration/evidence/hybrid-ui-dual-samples/php-full-catalog-deeplink-floor.json"),
    )
    args = ap.parse_args()
    errors: list[str] = []

    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    counts = catalog.get("counts") if isinstance(catalog.get("counts"), dict) else {}
    for key, floor in FLOORS.items():
        try:
            value = int(counts.get(key))
        except (TypeError, ValueError):
            errors.append(f"counts.{key} missing")
            continue
        if value < floor:
            errors.append(f"counts.{key}={value} < {floor}")

    rows = collect_hrefs(catalog)
    bad = [(kind, id_, href) for kind, id_, href in rows if not is_allowed(href)]
    if len(rows) < sum(FLOORS.values()):
        errors.append(f"href rows={len(rows)} < {sum(FLOORS.values())}")
    if bad:
        errors.append(f"disallowed deeplinks={len(bad)} sample={bad[:10]}")

    out = {
        "role": "php-full-catalog-deeplink-floor",
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "totalTracked": len(rows),
        "counts": counts,
        "floors": FLOORS,
        "disallowedCount": len(bad),
        "ok": not errors and not bad,
        "note": (
            "Every generated PHP catalog href must be a safe hybrid iframe deeplink "
            "(PHP /CP|/ERP|/BOS or storefront host). Never invents RELEASE_OWNER_APPROVAL.md."
        ),
    }
    args.evidence_out.parent.mkdir(parents=True, exist_ok=True)
    args.evidence_out.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")

    if errors:
        print("FAIL: php module catalog deeplink floor", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: totalTracked={len(rows)} disallowed=0 "
        f"cutoverAllowed=false aspNetInteractiveComplete=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
