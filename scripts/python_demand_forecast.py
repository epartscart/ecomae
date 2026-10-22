#!/usr/bin/env python3
"""Demand / reorder forecasting sidecar (Python).

Scores every part in `shop_docpart_prices_data` for reorder urgency using the
signals available in the price feed: stock level `exist`, supplier lead time
`time_to_exe` (days), multi-supplier coverage, and price competitiveness.

Urgency = low stock relative to lead time, weighted by supplier coverage risk.
This is the classic heuristic Python handles better than C#/SQL batch code —
no external deps needed (stdlib only), MySQL via PyMySQL or CSV.

Usage:
  python3 scripts/python_demand_forecast.py --mysql [--min-cover-days 14]
  python3 scripts/python_demand_forecast.py --csv rows.csv --json out.json --md out.md
CSV columns: price_id,manufacturer,article,name,price,exist,storage,time_to_exe,min_order
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import statistics
import sys
from collections import defaultdict
from dataclasses import dataclass


@dataclass
class Row:
    price_id: int
    manufacturer: str
    article: str
    name: str
    price: float
    exist: float
    storage: str
    lead_days: int
    min_order: float


def _f(v):
    try:
        return float(str(v).replace(",", ".").strip() or 0)
    except ValueError:
        return 0.0


def _i(v):
    try:
        return int(_f(v))
    except (ValueError, TypeError):
        return 0


def load_csv(path: str) -> list[Row]:
    rows = []
    with open(path, newline="", encoding="utf-8-sig") as fh:
        for r in csv.reader(fh):
            if len(r) < 6 or r[0].lower() in ("price_id", "id"):
                continue
            rows.append(Row(_i(r[0]), r[1].strip(), r[2].strip(),
                            (r[3] if len(r) > 3 else "").strip(), _f(r[4]), _f(r[5]),
                            (r[6] if len(r) > 6 else "").strip(),
                            _i(r[7]) if len(r) > 7 else 0,
                            _f(r[8]) if len(r) > 8 else 0))
    return rows


def load_mysql() -> list[Row]:
    try:
        import pymysql  # type: ignore
    except ImportError:
        sys.exit("PyMySQL not installed — use --csv or `pip install pymysql`")
    conn = pymysql.connect(
        host=os.environ.get("ECOMAE_MYSQL_HOST", "127.0.0.1"),
        port=int(os.environ.get("ECOMAE_MYSQL_PORT", "3306")),
        user=os.environ["ECOMAE_MYSQL_USER"],
        password=os.environ["ECOMAE_MYSQL_PASS"],
        database=os.environ["ECOMAE_MYSQL_DB"], charset="utf8mb4")
    with conn.cursor() as cur:
        cur.execute("SELECT `price_id`,`manufacturer`,`article`,`name`,`price`,`exist`,`storage`,`time_to_exe`,`min_order` FROM `shop_docpart_prices_data`")
        return [Row(_i(r[0]), (r[1] or "").strip(), (r[2] or "").strip(), (r[3] or "").strip(),
                    _f(r[4]), _f(r[5]), (r[6] or "").strip(), _i(r[7]), _f(r[8]))
                for r in cur.fetchall()]


def forecast(rows: list[Row], cover_days: int) -> dict:
    by_part: dict[tuple[str, str], list[Row]] = defaultdict(list)
    for r in rows:
        if r.manufacturer and r.article:
            by_part[(r.manufacturer, r.article)].append(r)

    reorder = []
    stockouts = []
    for (brand, article), rs in by_part.items():
        total_stock = sum(r.exist for r in rs)
        leads = [r.lead_days for r in rs if r.lead_days > 0]
        lead = min(leads) if leads else 14
        suppliers = {r.price_id for r in rs if r.exist > 0}
        priced = [r.price for r in rs if r.price > 0]
        best = min(priced) if priced else None

        if total_stock <= 0:
            stockouts.append({"manufacturer": brand, "article": article,
                              "suppliers": len({r.price_id for r in rs}),
                              "lead_days": lead,
                              "best_price": best})
            continue

        # Urgency: stock cannot outlast the lead time + safety cover window.
        cover_ratio = total_stock / max(1, len(rs))          # stock per offer
        days_cover = cover_ratio * 7                        # heuristic: weekly velocity proxy
        if days_cover < lead + cover_days:
            score = round((lead + cover_days - days_cover) / max(1, len(suppliers)), 1)
            reorder.append({"manufacturer": brand, "article": article,
                            "stock": total_stock, "lead_days": lead,
                            "active_suppliers": len(suppliers),
                            "best_price": best, "urgency": score,
                            "suggested_qty": max(1, int(min_order_suggest(rs) * 2))})
    reorder.sort(key=lambda r: -r["urgency"])

    return {
        "summary": {
            "parts": len(by_part),
            "stockouts": len(stockouts),
            "reorder_candidates": len(reorder),
            "cover_days_target": cover_days,
        },
        "reorder": reorder[:500],
        "stockouts": stockouts[:500],
    }


def min_order_suggest(rs: list[Row]) -> float:
    mos = [r.min_order for r in rs if r.min_order > 0]
    return min(mos) if mos else 1.0


def to_markdown(report: dict) -> str:
    s = report["summary"]
    out = ["# Demand / reorder forecast", "",
           f"- Parts analyzed: **{s['parts']}**",
           f"- Stock-outs: **{s['stockouts']}**",
           f"- Reorder candidates (cover < lead + {s['cover_days_target']}d): **{s['reorder_candidates']}**", "",
           "## Top reorder candidates", "",
           "| part | stock | lead | suppliers | best price | urgency | suggested qty |",
           "|---|---|---|---|---|---|---|"]
    for r in report["reorder"][:40]:
        out.append(f"| {r['manufacturer']} {r['article']} | {r['stock']} | {r['lead_days']}d | {r['active_suppliers']} | {r['best_price']} | {r['urgency']} | {r['suggested_qty']} |")
    return "\n".join(out) + "\n"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    src = ap.add_mutually_exclusive_group(required=True)
    src.add_argument("--csv")
    src.add_argument("--mysql", action="store_true")
    ap.add_argument("--min-cover-days", type=int, default=14)
    ap.add_argument("--json")
    ap.add_argument("--md")
    args = ap.parse_args()
    rows = load_csv(args.csv) if args.csv else load_mysql()
    report = forecast(rows, args.min_cover_days)
    if args.json:
        with open(args.json, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1, ensure_ascii=False)
    if args.md:
        with open(args.md, "w", encoding="utf-8") as fh:
            fh.write(to_markdown(report))
    s = report["summary"]
    print(f"PASS forecast parts={s['parts']} stockouts={s['stockouts']} reorder={s['reorder_candidates']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
