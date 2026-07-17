"""Price-list CSV ingest (phase 3b).

Mirrors epc_commerce_import_csv_local (PHP), but writes `records_count` AND
`article_search` at ingest time — so nothing has to backfill on the request
path ever again (the root cause of earlier CP/search slowness).

Column order matches the commerce export: manufacturer, article, name, exist, price.
Pure parsing (parse_csv) is unit-tested without a DB; import_csv performs the
transactional upsert.
"""

from __future__ import annotations

import csv
import io
import time
from typing import Any

from . import db
from .services import normalize_article


def _num(raw: Any) -> float:
    s = str(raw or "").strip().replace(" ", "").replace(",", ".")
    out = []
    seen_dot = False
    for ch in s:
        if ch.isdigit():
            out.append(ch)
        elif ch == "." and not seen_dot:
            out.append(ch)
            seen_dot = True
    try:
        return float("".join(out)) if out else 0.0
    except ValueError:
        return 0.0


def _qty(raw: Any) -> int:
    s = str(raw or "").strip().lower()
    if s in ("", "-"):
        return 0
    if any(w in s for w in ("да", "yes", "in stock", "есть", "+")):
        return 999
    digits = "".join(ch for ch in s if ch.isdigit())
    return int(digits) if digits else 0


def parse_csv(content: bytes | str, has_header: bool = True) -> dict[str, Any]:
    """Parse CSV bytes into normalized price rows. No DB access."""
    if isinstance(content, bytes):
        text = content.decode("utf-8", errors="replace")
    else:
        text = content
    reader = csv.reader(io.StringIO(text))
    rows: list[dict] = []
    skipped = 0
    first = True
    for cols in reader:
        if first and has_header:
            first = False
            continue
        first = False
        if not cols:
            continue
        manufacturer = (cols[0] if len(cols) > 0 else "").strip()[:255]
        article_show = (cols[1] if len(cols) > 1 else "").strip()[:255]
        name = (cols[2] if len(cols) > 2 else "").strip()[:255]
        exist = _qty(cols[3] if len(cols) > 3 else 0)
        price = _num(cols[4] if len(cols) > 4 else 0)
        article = normalize_article(article_show)
        if article == "" or price <= 0:
            skipped += 1
            continue
        rows.append({
            "manufacturer": manufacturer,
            "article": article,
            "article_show": article_show,
            "article_search": article,  # written at ingest — no backfill later
            "name": name,
            "exist": exist,
            "price": price,
        })
    return {"rows": rows, "skipped": skipped}


def import_csv(price_id: int, content: bytes | str, has_header: bool = True) -> dict[str, Any]:
    """Replace a price list's rows from CSV, writing records_count + article_search."""
    price_id = int(price_id)
    if price_id <= 0:
        return {"status": False, "error": "invalid price_id"}

    parsed = parse_csv(content, has_header)
    rows = parsed["rows"]
    started = time.monotonic()

    try:
        db.execute("DELETE FROM `shop_docpart_prices_data` WHERE `price_id` = %s", (price_id,))
        if rows:
            with db.cursor() as cur:
                cur.executemany(
                    """
                    INSERT INTO `shop_docpart_prices_data`
                        (`price_id`, `manufacturer`, `article`, `article_show`,
                         `article_search`, `name`, `exist`, `price`, `time_to_exe`,
                         `storage`, `min_order`)
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s, 1, '', 0)
                    """,
                    [
                        (
                            price_id, r["manufacturer"], r["article"], r["article_show"],
                            r["article_search"], r["name"], r["exist"], r["price"],
                        )
                        for r in rows
                    ],
                )
        # Denormalized counters updated at ingest — CP listing never recomputes.
        db.execute(
            "UPDATE `shop_docpart_prices` SET `last_updated` = %s, `records_count` = %s WHERE `id` = %s",
            (int(time.time()), len(rows), price_id),
        )
    except Exception as exc:
        return {"status": False, "error": str(exc), "imported": 0, "skipped": parsed["skipped"]}

    return {
        "status": True,
        "price_id": price_id,
        "imported": len(rows),
        "skipped": parsed["skipped"],
        "records_count": len(rows),
        "took_ms": int((time.monotonic() - started) * 1000),
    }
