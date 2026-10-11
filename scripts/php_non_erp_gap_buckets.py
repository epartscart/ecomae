#!/usr/bin/env python3
"""Group current PHP gap files into the Cursor non-ERP plan buckets.

Reads the inventory JSON from scripts/php_reference_gap_inventory.py.
Rules are path-prefix only so the progress-report table can be regenerated
and will always sum to gap_files / gap_lines.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# (step, label, matcher). First match wins.
BUCKETS = [
    ("7", "ERP finance (Devin)", lambda p: "/finance/" in p or p.startswith("content/shop/finance")),
    ("6", "Price engine", lambda p: p.startswith("content/shop/price_engine/")),
    ("1", "Storefront: parts/docpart", lambda p: p.startswith("content/shop/docpart/")),
    ("1", "Storefront: catalogue", lambda p: p.startswith("content/shop/catalogue/") or p.startswith("modules/shop/catalogue/")),
    ("1", "Storefront: other shop", lambda p: p.startswith("content/shop/")),
    ("1", "Storefront: modules", lambda p: p.startswith("modules/")),
    ("1", "Storefront: templates", lambda p: p.startswith("templates/")),
    ("1", "Storefront: users/plugins", lambda p: p.startswith("content/users/") or p.startswith("plugins/")),
    ("3", "CP shop core (orders, catalogue, price upload)", lambda p: p.startswith("cp/content/shop/order_process/") or p.startswith("cp/content/shop/catalogue/") or p.startswith("cp/content/shop/prices_upload/")),
    ("3", "CP shop smaller", lambda p: p.startswith("cp/content/shop/")),
    ("4", "CP control/portal", lambda p: p.startswith("cp/content/control/") or p.startswith("cp/content/portal/")),
    ("3", "CP other", lambda p: p.startswith("cp/")),
    ("8", "Core/root", lambda p: p.startswith("core/") or p.startswith("lib/") or "/" not in p),
    ("5", "Marketing/BOS/industries", lambda p: True),
]


def bucket(path: str) -> tuple[str, str]:
    for step, label, match in BUCKETS:
        if match(path):
            return step, label
    return "?", path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory-json", default="/tmp/gap_inv.json")
    args = parser.parse_args()
    data = json.load(open(args.inventory_json, encoding="utf-8"))
    gaps = [r for r in data["files"] if r["triage"] == "gap"]
    rows: dict[tuple[str, str], list] = {}
    for r in gaps:
        key = bucket(r["file"])
        rows.setdefault(key, []).append(r)
    print("| Plan step | Area | Gap files | Lines |")
    print("|---|---|---:|---:|")
    total_f = total_l = non_f = non_l = erp_f = erp_l = 0
    labels = [(s, lab) for s, lab, _ in BUCKETS]
    seen: set[tuple[str, str]] = set()
    ordered = []
    for key in labels:
        if key in seen or key not in rows:
            continue
        seen.add(key)
        ordered.append(key)
    ordered.sort(key=lambda k: (int(k[0]) if k[0].isdigit() else 99, k[1]))
    for key in ordered:
        files = rows[key]
        n, lines = len(files), sum(x["lines"] for x in files)
        print(f"| {key[0]} | {key[1]} | {n} | {lines:,} |")
        total_f += n
        total_l += lines
        if key[0] == "7":
            erp_f += n
            erp_l += lines
        else:
            non_f += n
            non_l += lines
    print(f"| | **Total** | **{total_f}** | **{total_l:,}** |")
    print(f"\nnon-ERP {non_f} / {non_l}")
    print(f"ERP {erp_f} / {erp_l}")
    if total_f != len(gaps) or total_l != sum(r["lines"] for r in gaps):
        sys.exit(f"bucket sum {total_f}/{total_l} != inventory {len(gaps)}/{sum(r['lines'] for r in gaps)}")


if __name__ == "__main__":
    main()
