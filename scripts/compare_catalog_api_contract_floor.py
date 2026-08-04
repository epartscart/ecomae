#!/usr/bin/env python3
"""Validate catalog/API migration goldens against locked envelope contracts.

Offline contract floor for 18 catalog routes (+ price lookup via dedicated compare).
Always emits cutoverAllowed=false. Never invents RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# Mirror SurfacePayloadContractCatalog API envelope fields.
ENVELOPES: dict[str, list[str]] = {
    "api-catalog-status": ["connected", "message", "status_code", "counts", "source"],
    "api-catalog-manufacturers": ["ok", "section", "rows", "source", "data", "message"],
    "api-catalog-models": ["ok", "action", "section", "rows", "source", "data", "message"],
    "api-catalog-modifications": ["ok", "action", "section", "rows", "source", "data", "message"],
    "api-catalog-brands": ["ok", "action", "section", "rows", "source", "data", "message"],
    "api-catalog-suppliers": ["ok", "action", "section", "rows", "source", "data", "message"],
    "api-catalog-engines": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-analogs": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-article-brands": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-categories": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-products": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-engine-search": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-article-links": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-article": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-articles": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-engine": ["ok", "action", "section", "rows", "source", "stale", "data"],
    "api-catalog-vin": [
        "ok",
        "source",
        "stale",
        "cached_at",
        "vin",
        "language",
        "region",
        "vehicle_count",
        "manufacturer",
        "model_label",
        "payload",
    ],
    "api-catalog-brand-parts": ["ok", "brand", "rows", "source", "data", "message"],
}

# Offline-cache action goldens: envelope data is still an object blob (OfflineCacheOk).
OFFLINE_CACHE_OBJECT_DATA = frozenset(
    {
        "api-catalog-engines",
        "api-catalog-analogs",
        "api-catalog-article-brands",
        "api-catalog-categories",
        "api-catalog-products",
        "api-catalog-engine-search",
        "api-catalog-article-links",
        "api-catalog-article",
        "api-catalog-articles",
        "api-catalog-engine",
    }
)

# Wave-2: nested list item fields inside the object blob at data.data[]
# (mirrors PHP epc_engines_list_items / brands offline payload / UMAPI wrappers).
OFFLINE_CACHE_NESTED_LIST_ITEM_FIELDS: dict[str, list[str]] = {
    "api-catalog-engines": [
        "ENG_ID",
        "ENGINE_CODE",
        "POWER_KW",
        "CAPACITY_LT",
        "FUEL_TYPE",
    ],
    "api-catalog-analogs": [
        "ART_ID",
        "BRAND",
        "ARTICLE_NR",
        "TITLE",
    ],
    "api-catalog-article-brands": [
        "BRAND",
        "SUP_BRAND",
        "MANUFACTURER",
        "DISPLAY_NR",
        "SEARCH_NUMBER",
        "ARTICLE",
        "TITLE",
        "DES",
    ],
    "api-catalog-categories": [
        "STR_ID",
        "CATEGORY_NAME",
        "ORDER",
    ],
    "api-catalog-products": [
        "ART_ARTICLE_NR",
        "SUP_BRAND",
        "ART_PRODUCT_NAME",
    ],
    "api-catalog-articles": [
        "ART_ARTICLE_NR",
        "SUP_BRAND",
        "ART_PRODUCT_NAME",
    ],
    "api-catalog-engine-search": [
        "ENG_ID",
        "ENGINE_CODE",
        "POWER_KW",
        "CAPACITY_LT",
        "FUEL_TYPE",
    ],
}

# Single-object offline-cache blobs (article / engine by id).
OFFLINE_CACHE_OBJECT_ITEM_FIELDS: dict[str, list[str]] = {
    "api-catalog-article": ["ART_ID", "TITLE"],
    "api-catalog-engine": ["ENG_ID", "ENGINE_CODE"],
}

# Fitment section lists inside article-links object blob (PHP PC/CV/Motorcycle).
OFFLINE_CACHE_SECTION_LIST_ITEM_FIELDS: dict[str, tuple[str, list[str]]] = {
    "api-catalog-article-links": (
        "PC",
        ["CI_FROM", "CI_TO", "POWER_KW", "FUEL_TYPE"],
    ),
}

STATUS_COUNT_FIELDS = ["manufacturers", "models", "modifications", "brands", "vins"]

# Non-empty migration item-field sentinels (SurfacePayloadContractCatalog item fields).
LIST_ITEM_FIELDS: dict[str, list[str]] = {
    "api-catalog-manufacturers": [
        "MFA_ID",
        "manufacturer",
        "manufacturer_ru",
        "type",
        "country",
        "popular",
        "is_logo",
    ],
    "api-catalog-models": ["MFA_ID", "MS_ID", "model_series", "year_from", "year_to"],
    "api-catalog-modifications": [
        "MS_ID",
        "modification_id",
        "title",
        "year_from",
        "year_to",
        "power_kw",
        "capacity_lt",
        "fuel_type",
    ],
    "api-catalog-brands": ["sup_id", "brand", "full_name"],
    "api-catalog-suppliers": ["sup_id", "brand", "full_name"],
    "api-catalog-brand-parts": [
        "manufacturer",
        "article_show",
        "article",
        "name",
        "exist",
        "price",
        "time_to_exe",
        "storage",
    ],
}
LIST_NONEMPTY_DATA = frozenset(LIST_ITEM_FIELDS)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--migration-dir",
        type=Path,
        default=ROOT / "docs/migration/evidence/surface-parity/samples/migration",
    )
    ap.add_argument(
        "--out",
        type=Path,
        default=ROOT / "docs/migration/evidence/catalog-api/compare-result.json",
    )
    ap.add_argument("--skip-price", action="store_true")
    args = ap.parse_args()

    results = []
    failed = 0
    for stem, required in ENVELOPES.items():
        path = args.migration_dir / f"{stem}.json"
        entry = {"stem": stem, "file": path.name, "ok": False, "errors": []}
        if not path.is_file():
            entry["errors"].append("missing migration golden")
            failed += 1
            results.append(entry)
            continue
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            entry["errors"].append(f"invalid json: {ex}")
            failed += 1
            results.append(entry)
            continue
        if not isinstance(doc, dict):
            entry["errors"].append("root must be object")
            failed += 1
            results.append(entry)
            continue
        if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
            entry["errors"].append("must not claim cutover/PHP removal")
        missing = [k for k in required if k not in doc]
        if missing:
            entry["errors"].append(f"missing envelope fields: {missing}")
        if stem == "api-catalog-status":
            counts = doc.get("counts") if isinstance(doc.get("counts"), dict) else {}
            for field in STATUS_COUNT_FIELDS:
                if field not in counts:
                    entry["errors"].append(f"counts missing {field}")
        item_fields = LIST_ITEM_FIELDS.get(stem) or []
        if stem in LIST_NONEMPTY_DATA or item_fields:
            data = doc.get("data")
            if not isinstance(data, list) or len(data) < 1:
                entry["errors"].append("expected non-empty data[] item-field sentinel")
            else:
                first = data[0]
                if not isinstance(first, dict):
                    entry["errors"].append("data[0] must be object")
                else:
                    for field in item_fields:
                        if field not in first:
                            entry["errors"].append(f"data[0] missing {field}")
        if stem in OFFLINE_CACHE_OBJECT_DATA:
            data = doc.get("data")
            if not isinstance(data, dict):
                entry["errors"].append("offline-cache data must be object (JSON blob envelope)")
            nested_fields = OFFLINE_CACHE_NESTED_LIST_ITEM_FIELDS.get(stem) or []
            if nested_fields and isinstance(data, dict):
                nested = data.get("data")
                if not isinstance(nested, list) or len(nested) < 1:
                    entry["errors"].append(
                        "expected non-empty nested data.data[] item-field sentinel"
                    )
                else:
                    first = nested[0]
                    if not isinstance(first, dict):
                        entry["errors"].append("data.data[0] must be object")
                    else:
                        for field in nested_fields:
                            if field not in first:
                                entry["errors"].append(f"data.data[0] missing {field}")
            object_fields = OFFLINE_CACHE_OBJECT_ITEM_FIELDS.get(stem) or []
            if object_fields and isinstance(data, dict):
                for field in object_fields:
                    if field not in data:
                        entry["errors"].append(f"offline-cache object data missing {field}")
            section_spec = OFFLINE_CACHE_SECTION_LIST_ITEM_FIELDS.get(stem)
            if section_spec and isinstance(data, dict):
                section_key, section_fields = section_spec
                rows = data.get(section_key)
                if not isinstance(rows, list) or len(rows) < 1:
                    entry["errors"].append(
                        f"expected non-empty data.{section_key}[] item-field sentinel"
                    )
                else:
                    first = rows[0]
                    if not isinstance(first, dict):
                        entry["errors"].append(f"data.{section_key}[0] must be object")
                    else:
                        for field in section_fields:
                            if field not in first:
                                entry["errors"].append(
                                    f"data.{section_key}[0] missing {field}"
                                )
        if stem == "api-catalog-vin":
            if not isinstance(doc.get("payload"), dict):
                entry["errors"].append("vin.payload must be object")
            if not str(doc.get("vin") or "").strip():
                entry["errors"].append("vin must be non-empty string")
        entry["ok"] = not entry["errors"]
        if not entry["ok"]:
            failed += 1
        results.append(entry)
        print(("PASS" if entry["ok"] else "FAIL") + f" migration/{stem}")

    price_ok = True
    if not args.skip_price:
        proc = subprocess.run(
            [
                sys.executable,
                str(ROOT / "scripts/compare_price_lookup_parity.py"),
                "--contract-only",
            ],
            capture_output=True,
            text=True,
        )
        price_ok = proc.returncode == 0
        if not price_ok:
            failed += 1
        print(("PASS" if price_ok else "FAIL") + " price-lookup contract")
        if proc.stdout:
            print(proc.stdout.strip())

    out = {
        "role": "compare-result",
        "ok": failed == 0,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "catalogGoldensChecked": len(ENVELOPES),
        "failed": failed,
        "priceLookupOk": price_ok if not args.skip_price else None,
        "listItemFieldStems": sorted(LIST_ITEM_FIELDS),
        "offlineCacheObjectDataStems": sorted(OFFLINE_CACHE_OBJECT_DATA),
        "offlineCacheNestedListItemFieldStems": sorted(OFFLINE_CACHE_NESTED_LIST_ITEM_FIELDS),
        "offlineCacheObjectItemFieldStems": sorted(OFFLINE_CACHE_OBJECT_ITEM_FIELDS),
        "offlineCacheSectionListItemFieldStems": sorted(OFFLINE_CACHE_SECTION_LIST_ITEM_FIELDS),
        "vinEnvelopeExpanded": True,
        "results": results,
        "note": (
            "Catalog/API contract floor only. Wave-1 list goldens keep non-empty "
            "item-field sentinels; VIN envelope requires manufacturer/model_label/cached_at; "
            "offline-cache action goldens keep object data blobs with nested/object/section "
            "item-field sentinels for all 10 offline-cache stems. "
            "Exact-route shadows remain operator-gated. Never invents RELEASE_OWNER_APPROVAL.md."
        ),
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"ok": out["ok"], "failed": failed, "cutoverAllowed": False}, indent=2))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
