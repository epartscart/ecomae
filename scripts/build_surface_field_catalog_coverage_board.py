#!/usr/bin/env python3
"""Map every PHP module-catalog row to an honest surface-field coverage status.

Statuses (mutually exclusive, inventory-driven):
  digest-contract        — catalog row matched a hybrid TARGET with digestRoute
  hybrid-directory-only  — hybrid preview/deeplink without digest contract
  php-only-deeplink      — full PHP catalog row; hybrid may deeplink, no ASP.NET digest
  missing                — catalog row absent from inventory or not deeplink-safe

Never invents RELEASE_OWNER_APPROVAL.md / MODULE_FUNCTION_TEST_PASS.md.
Always keeps cutoverAllowed=false and aspNetInteractiveComplete=0.
"""
from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from pathlib import Path
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parents[1]

FLOORS = {
    "erpAreas": 35,
    "erpTabs": 154,
    "erpCategories": 9,
    "bosSections": 11,
    "bosModules": 99,
    "cpBrochureFeatures": 405,
    "storefrontSurfaces": 13,
}
MIN_TOTAL = sum(FLOORS.values())
ALLOWED_COVERAGE = frozenset(
    {
        "digest-contract",
        "hybrid-directory-only",
        "php-only-deeplink",
        "missing",
    }
)


def is_allowed_deeplink(href: str) -> bool:
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


def collect_catalog_rows(catalog: dict) -> list[dict]:
    rows: list[dict] = []
    for area in catalog.get("erpAreas") or []:
        area_id = str(area.get("id") or "")
        rows.append(
            {
                "id": f"erp-area-{area_id}",
                "kind": "erp-area",
                "label": str(area.get("label") or area_id),
                "phpPath": str(area.get("href") or ""),
            }
        )
        for tab in area.get("tabs") or []:
            tab_id = str(tab.get("id") or "")
            rows.append(
                {
                    "id": f"erp-tab-{area_id}-{tab_id}",
                    "kind": "erp-tab",
                    "label": str(tab.get("label") or tab_id),
                    "phpPath": str(tab.get("href") or ""),
                }
            )
    for cat in catalog.get("erpCategories") or []:
        cat_id = str(cat.get("id") or "")
        rows.append(
            {
                "id": f"erp-category-{cat_id}",
                "kind": "erp-category",
                "label": str(cat.get("label") or cat_id),
                "phpPath": str(cat.get("href") or ""),
            }
        )
    for sec in catalog.get("bosSections") or []:
        sec_id = str(sec.get("id") or "")
        rows.append(
            {
                "id": f"bos-section-{sec_id}",
                "kind": "bos-section",
                "label": str(sec.get("label") or sec_id),
                "phpPath": str(sec.get("href") or f"/BOS/?section={sec_id}"),
            }
        )
    for bos in catalog.get("bosModules") or []:
        bos_id = str(bos.get("id") or "")
        rows.append(
            {
                "id": f"bos-{bos_id}",
                "kind": "bos-module",
                "label": str(bos.get("label") or bos_id),
                "phpPath": str(bos.get("href") or ""),
            }
        )
    for cp in catalog.get("cpBrochureFeatures") or []:
        cp_id = str(cp.get("id") or "")
        rows.append(
            {
                "id": f"cp-{cp_id}",
                "kind": "cp-feature",
                "label": str(cp.get("name") or cp_id),
                "phpPath": str(cp.get("href") or ""),
            }
        )
    for sf in catalog.get("storefrontSurfaces") or []:
        sf_id = str(sf.get("id") or "")
        rows.append(
            {
                "id": f"storefront-{sf_id}",
                "kind": "storefront-surface",
                "label": str(sf.get("label") or sf_id),
                "phpPath": str(sf.get("href") or ""),
            }
        )
    return rows


def coverage_for(
    inv_row: dict | None,
    php_path: str,
    digest_routes: set[str],
) -> str:
    if inv_row is None:
        return "missing"
    status = str(inv_row.get("status") or "")
    digest = str(inv_row.get("digestRoute") or "").strip()
    if "digest" in status:
        if digest and digest in digest_routes:
            return "digest-contract"
        # digest-status without a locked surface-field contract is still a hybrid gap
        return "hybrid-directory-only"
    if status == "hybrid-deeplink":
        return "hybrid-directory-only"
    if status == "php-only" and is_allowed_deeplink(php_path):
        return "php-only-deeplink"
    return "missing"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--catalog",
        type=Path,
        default=ROOT
        / "aspnet/src/EcomAE.Platform/Presentation/Generated/php_module_catalog.json",
    )
    ap.add_argument(
        "--inventory",
        type=Path,
        default=ROOT
        / "docs/migration/evidence/module-function-parity/module-function-inventory.json",
    )
    ap.add_argument(
        "--surface-field-board",
        type=Path,
        default=ROOT / "docs/migration/evidence/surface-parity/www-surface-field-parity.json",
    )
    ap.add_argument(
        "--out",
        type=Path,
        default=ROOT
        / "docs/migration/evidence/surface-parity/php-catalog-coverage-board.json",
    )
    args = ap.parse_args()
    errors: list[str] = []

    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    inventory = json.loads(args.inventory.read_text(encoding="utf-8"))
    board = json.loads(args.surface_field_board.read_text(encoding="utf-8"))

    if inventory.get("cutoverAllowed") is True or inventory.get("readyForPhpRemoval") is True:
        errors.append("inventory must keep cutoverAllowed/readyForPhpRemoval false")
    if board.get("cutoverAllowed") is True or board.get("readyForPhpRemoval") is True:
        errors.append("surface-field board must keep cutoverAllowed/readyForPhpRemoval false")

    counts = catalog.get("counts") if isinstance(catalog.get("counts"), dict) else {}
    for key, floor in FLOORS.items():
        try:
            value = int(counts.get(key))
        except (TypeError, ValueError):
            errors.append(f"catalog.counts.{key} missing")
            continue
        if value < floor:
            errors.append(f"catalog.counts.{key}={value} < {floor}")

    digest_routes = {
        str(c.get("aspNetRoute") or "").strip()
        for c in (board.get("contracts") or [])
        if isinstance(c, dict) and str(c.get("aspNetRoute") or "").strip()
    }

    inv_by_id = {
        str(m.get("id")): m
        for m in (inventory.get("modules") or [])
        if isinstance(m, dict) and m.get("id") and m.get("kind") != "hybrid-preview"
    }

    catalog_rows = collect_catalog_rows(catalog)
    items: list[dict] = []
    status_counts: Counter[str] = Counter()
    kind_counts: Counter[str] = Counter()
    kind_status: dict[str, Counter[str]] = {}

    for row in catalog_rows:
        inv = inv_by_id.get(row["id"])
        coverage = coverage_for(inv, row["phpPath"], digest_routes)
        status_counts[coverage] += 1
        kind_counts[row["kind"]] += 1
        kind_status.setdefault(row["kind"], Counter())[coverage] += 1
        items.append(
            {
                "id": row["id"],
                "kind": row["kind"],
                "label": row["label"],
                "phpPath": row["phpPath"],
                "coverage": coverage,
                "moduleStatus": (inv or {}).get("status"),
                "digestRoute": (inv or {}).get("digestRoute"),
                "aspnetRoute": (inv or {}).get("aspnetRoute"),
                "aspnetComplete": False,
            }
        )

    if len(catalog_rows) < MIN_TOTAL:
        errors.append(f"catalog rows={len(catalog_rows)} < {MIN_TOTAL}")
    missing = int(status_counts.get("missing", 0))
    if missing != 0:
        sample = [i["id"] for i in items if i["coverage"] == "missing"][:10]
        errors.append(f"missingCount={missing} sample={sample}")
    for coverage in status_counts:
        if coverage not in ALLOWED_COVERAGE:
            errors.append(f"unexpected coverage status {coverage!r}")

    # Honest floor: most catalog rows remain php-only-deeplink until digests land.
    php_only = int(status_counts.get("php-only-deeplink", 0))
    if php_only < 600:
        errors.append(f"php-only-deeplink={php_only} unexpectedly low (honesty floor >=600)")
    digest_contract = int(status_counts.get("digest-contract", 0))
    if digest_contract < 1:
        errors.append("digest-contract count must be >=1 (hybrid TARGET digests exist)")

    out = {
        "role": "php-catalog-coverage-board",
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "totalTracked": len(items),
        "floors": FLOORS,
        "catalogCounts": counts,
        "kindCounts": dict(kind_counts),
        "coverageCounts": dict(status_counts),
        "coverageByKind": {k: dict(v) for k, v in sorted(kind_status.items())},
        "digestContractRouteCount": len(digest_routes),
        "missingCount": missing,
        "ok": not errors,
        "errors": errors,
        "items": items,
        "note": (
            "Full PHP catalog (725) coverage board. digest-contract means a locked "
            "surface-field digest route exists for a hybrid TARGET match. "
            "Interactive aspNetInteractiveComplete stays 0 until human "
            "MODULE_FUNCTION_TEST_PASS.md. Never invents RELEASE_OWNER_APPROVAL.md."
        ),
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")

    if errors:
        print("FAIL: php catalog coverage board", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: totalTracked={len(items)} coverageCounts={dict(status_counts)} "
        f"missing=0 cutoverAllowed=false aspNetInteractiveComplete=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
