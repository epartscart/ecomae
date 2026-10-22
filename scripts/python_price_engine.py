#!/usr/bin/env python3
"""Supplier price analytics engine (Python sidecar for the CP price pipeline).

The ASP.NET CpPriceImport pipeline owns parsing/import/cron; this engine owns what
Python does better than C#/SQL: bulk analytics over `shop_docpart_prices_data` —
cross-supplier best-buy spreads, outlier detection, stale/empty-list detection.

Inputs (pick one):
  --csv FILE        rows exported as: price_id,manufacturer,article,name,price,exist,storage,time_to_exe,min_order
  --mysql           read the table live via PyMySQL (env: ECOMAE_MYSQL_HOST/_PORT/_USER/_PASS/_DB)

Outputs:
  --json PATH       machine-readable report
  --md PATH         human-readable summary
  stdout            one-line PASS/summary for cron logs
"""
from __future__ import annotations

import argparse
import csv
import json
import math
import os
import statistics
import sys
from collections import defaultdict
from dataclasses import dataclass, field


@dataclass
class Row:
    price_id: int
    manufacturer: str
    article: str
    name: str
    price: float
    exist: float
    storage: str
    time_to_exe: int
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
                            (r[3] if len(r) > 3 else "").strip(),
                            _f(r[4]), _f(r[5]),
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
        database=os.environ["ECOMAE_MYSQL_DB"],
        charset="utf8mb4")
    with conn.cursor() as cur:
        cur.execute(
            "SELECT `price_id`,`manufacturer`,`article`,`name`,`price`,`exist`,`storage`,`time_to_exe`,`min_order`"
            " FROM `shop_docpart_prices_data`")
        return [Row(_i(r[0]), (r[1] or "").strip(), (r[2] or "").strip(), (r[3] or "").strip(),
                    _f(r[4]), _f(r[5]), (r[6] or "").strip(), _i(r[7]), _f(r[8]))
                for r in cur.fetchall()]


def _median_mad(values: list[float]) -> tuple[float, float]:
    med = statistics.median(values)
    mad = statistics.median([abs(v - med) for v in values])
    return med, mad


def analyze(rows: list[Row]) -> dict:
    by_supplier: dict[int, list[Row]] = defaultdict(list)
    by_part: dict[tuple[str, str], list[Row]] = defaultdict(list)
    for r in rows:
        by_supplier[r.price_id].append(r)
        if r.manufacturer and r.article:
            by_part[(r.manufacturer.lower(), r.article.lower())].append(r)

    suppliers = []
    for pid, rs in sorted(by_supplier.items()):
        prices = [r.price for r in rs if r.price > 0]
        in_stock = sum(1 for r in rs if r.exist > 0)
        suppliers.append({
            "price_id": pid,
            "rows": len(rs),
            "in_stock_rows": in_stock,
            "in_stock_pct": round(100.0 * in_stock / len(rs), 1) if rs else 0,
            "zero_price_rows": len(rs) - len(prices),
            "avg_price": round(statistics.mean(prices), 2) if prices else None,
            "min_price": round(min(prices), 2) if prices else None,
            "max_price": round(max(prices), 2) if prices else None,
        })

    best_buy = []
    savings_potential = 0.0
    for (brand, article), rs in by_part.items():
        priced = [r for r in rs if r.price > 0]
        if len({r.price_id for r in priced}) < 2:
            continue
        lo = min(priced, key=lambda r: r.price)
        hi = max(priced, key=lambda r: r.price)
        spread_pct = round(100.0 * (hi.price - lo.price) / lo.price, 1) if lo.price > 0 else 0
        savings_potential += hi.price - lo.price
        if spread_pct >= 20:
            best_buy.append({
                "manufacturer": lo.manufacturer, "article": lo.article, "name": lo.name,
                "best_price_id": lo.price_id, "best_price": round(lo.price, 2),
                "worst_price_id": hi.price_id, "worst_price": round(hi.price, 2),
                "spread_pct": spread_pct})
    best_buy.sort(key=lambda b: -b["spread_pct"])

    anomalies = []
    for (brand, article), rs in by_part.items():
        prices = [r.price for r in rs if r.price > 0]
        if len(prices) < 4:
            continue
        med, mad = _median_mad(prices)
        if mad == 0:
            continue
        for r in rs:
            if r.price > 0 and abs(r.price - med) / mad > 8:
                anomalies.append({
                    "manufacturer": r.manufacturer, "article": r.article,
                    "price_id": r.price_id, "price": round(r.price, 2),
                    "median": round(med, 2), "deviation_mad": round(abs(r.price - med) / mad, 1)})
    anomalies.sort(key=lambda a: -a["deviation_mad"])

    return {
        "summary": {
            "rows": len(rows),
            "suppliers": len(by_supplier),
            "distinct_parts": len(by_part),
            "multi_supplier_parts": sum(1 for rs in by_part.values() if len({r.price_id for r in rs}) > 1),
            "savings_potential": round(savings_potential, 2),
            "price_anomalies": len(anomalies),
        },
        "suppliers": suppliers,
        "best_buy_opportunities": best_buy[:200],
        "price_anomalies": anomalies[:200],
    }


def to_markdown(report: dict) -> str:
    s = report["summary"]
    out = [
        "# Supplier price engine report", "",
        f"- Rows analyzed: **{s['rows']}** across **{s['suppliers']}** price lists",
        f"- Distinct parts: **{s['distinct_parts']}** ({s['multi_supplier_parts']} offered by >1 supplier)",
        f"- Potential savings vs worst offer: **{s['savings_potential']}**",
        f"- Price anomalies (MAD outliers): **{s['price_anomalies']}**", "",
        "## Suppliers", "",
        "| price_id | rows | in-stock % | zero-price | avg | min | max |",
        "|---|---|---|---|---|---|---|",
    ]
    for sp in report["suppliers"]:
        out.append("| {price_id} | {rows} | {in_stock_pct}% | {zero_price_rows} | {avg_price} | {min_price} | {max_price} |".format(**sp))
    out += ["", "## Top best-buy opportunities (spread ≥ 20%)", "",
            "| part | best | worst | spread |", "|---|---|---|---|"]
    for b in report["best_buy_opportunities"][:25]:
        out.append(f"| {b['manufacturer']} {b['article']} | #{b['best_price_id']} @ {b['best_price']} | #{b['worst_price_id']} @ {b['worst_price']} | {b['spread_pct']}% |")
    return "\n".join(out) + "\n"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    src = ap.add_mutually_exclusive_group(required=True)
    src.add_argument("--csv")
    src.add_argument("--mysql", action="store_true")
    ap.add_argument("--json")
    ap.add_argument("--md")
    args = ap.parse_args()

    rows = load_csv(args.csv) if args.csv else load_mysql()
    report = analyze(rows)
    if args.json:
        with open(args.json, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=1, ensure_ascii=False)
    if args.md:
        with open(args.md, "w", encoding="utf-8") as fh:
            fh.write(to_markdown(report))
    s = report["summary"]
    print(f"PASS price-engine rows={s['rows']} suppliers={s['suppliers']} "
          f"bestBuy={len(report['best_buy_opportunities'])} anomalies={s['price_anomalies']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
