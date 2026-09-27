#!/usr/bin/env python3
"""Reconcile the generated ERP navigation matrix against the PHP authority.

This is intentionally a source-to-artifact check, not a count check. It
compares ordering, labels, icons, descriptions, groups, visibility metadata,
links, and category membership from erp_nav_areas.php.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from generate_php_module_catalog import parse_erp_areas, parse_erp_categories


def normalize(row: dict[str, Any], keys: tuple[str, ...]) -> tuple[Any, ...]:
    return tuple(row.get(key) for key in keys)


def compare_sequence(
    label: str,
    expected: list[dict[str, Any]],
    actual: list[dict[str, Any]],
    keys: tuple[str, ...],
    failures: list[str],
) -> None:
    if len(expected) != len(actual):
        failures.append(f"{label}: expected {len(expected)} records, found {len(actual)}")

    for index, (left, right) in enumerate(zip(expected, actual)):
        if normalize(left, keys) != normalize(right, keys):
            failures.append(
                f"{label}[{index}]: PHP={normalize(left, keys)!r} "
                f"generated={normalize(right, keys)!r}"
            )

    if len(expected) > len(actual):
        for row in expected[len(actual) :]:
            failures.append(f"{label}: missing generated record {row.get('id')!r}")
    elif len(actual) > len(expected):
        for row in actual[len(expected) :]:
            failures.append(f"{label}: invented generated record {row.get('id')!r}")


def duplicate_values(rows: list[dict[str, Any]], key_name: str = "id") -> list[str]:
    seen: set[str] = set()
    duplicates: set[str] = set()
    for row in rows:
        key = str(row.get(key_name) or "")
        if key in seen:
            duplicates.add(key)
        seen.add(key)
    return sorted(duplicates)


def reconcile(root: Path) -> dict[str, Any]:
    php_path = root / "cp/content/shop/finance/erp/erp_nav_areas.php"
    generated_path = (
        root
        / "aspnet/src/EcomAE.Platform/Presentation/Generated/php_module_catalog.json"
    )
    php_text = php_path.read_text(encoding="utf-8", errors="replace")
    generated = json.loads(generated_path.read_text(encoding="utf-8"))
    expected_areas = parse_erp_areas(php_text)
    expected_categories = parse_erp_categories(php_text)
    actual_areas = generated.get("erpAreas", [])
    actual_categories = generated.get("erpCategories", [])
    failures: list[str] = []

    compare_sequence(
        "categories",
        expected_categories,
        actual_categories,
        ("id", "label", "short", "icon", "areas", "href"),
        failures,
    )
    compare_sequence(
        "areas",
        expected_areas,
        actual_areas,
        ("id", "label", "icon", "description", "href"),
        failures,
    )

    expected_area_ids = [row["id"] for row in expected_areas]
    actual_area_ids = [row["id"] for row in actual_areas]
    if duplicate_values(expected_areas):
        failures.append(f"PHP duplicate area IDs: {duplicate_values(expected_areas)}")
    if duplicate_values(actual_areas):
        failures.append(f"generated duplicate area IDs: {duplicate_values(actual_areas)}")

    expected_tabs: list[dict[str, Any]] = []
    actual_tabs: list[dict[str, Any]] = []
    for expected, actual in zip(expected_areas, actual_areas):
        if expected["id"] != actual.get("id"):
            continue
        expected_tabs.extend(
            {**tab, "placement": f"{expected['id']}/{tab['id']}"}
            for tab in expected["tabs"]
        )
        actual_tabs.extend(
            {**tab, "placement": f"{actual['id']}/{tab.get('id')}"}
            for tab in actual.get("tabs", [])
        )

        compare_sequence(
            f"area {expected['id']} tabs",
            expected["tabs"],
            actual.get("tabs", []),
            ("id", "label", "icon", "group", "isJewellery", "isRaw", "href"),
            failures,
        )

    compare_sequence(
        "all tab placements",
        expected_tabs,
        actual_tabs,
        ("placement", "label", "icon", "group", "isJewellery", "isRaw", "href"),
        failures,
    )

    expected_placements = [row["placement"] for row in expected_tabs]
    actual_placements = [row["placement"] for row in actual_tabs]
    if duplicate_values(expected_tabs, "placement"):
        failures.append(
            f"PHP duplicate tab placements: {duplicate_values(expected_tabs, 'placement')}"
        )
    if duplicate_values(actual_tabs, "placement"):
        failures.append(
            "generated duplicate tab placements: "
            f"{duplicate_values(actual_tabs, 'placement')}"
        )
    if any(not row.get("href", "").strip() for row in expected_tabs + expected_areas):
        failures.append("PHP ERP navigation contains an empty href")
    if any(not row.get("href", "").strip() for row in actual_tabs + actual_areas):
        failures.append("generated ERP navigation contains an empty href")

    return {
        "php": str(php_path.relative_to(root)),
        "generated": str(generated_path.relative_to(root)),
        "expected": {
            "categories": len(expected_categories),
            "areas": len(expected_areas),
            "tabs": len(expected_tabs),
            "areaOrder": expected_area_ids,
            "tabPlacements": expected_placements,
        },
        "actual": {
            "categories": len(actual_categories),
            "areas": len(actual_areas),
            "tabs": len(actual_tabs),
            "areaOrder": actual_area_ids,
            "tabPlacements": actual_placements,
        },
        "failures": failures,
        "status": "pass" if not failures else "fail",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--json", action="store_true", help="emit the full report as JSON")
    args = parser.parse_args()
    report = reconcile(args.root)
    if args.json:
        print(json.dumps(report, indent=2))
    else:
        print(
            f"ERP navigation reconciliation: {report['status']} "
            f"({report['expected']['areas']} areas, {report['expected']['tabs']} tabs, "
            f"{report['expected']['categories']} categories)"
        )
        for failure in report["failures"]:
            print(f"- {failure}")
    return 0 if report["status"] == "pass" else 1


if __name__ == "__main__":
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    raise SystemExit(main())
