"""Business queries for the hot endpoints (phase 1 of the PHP → Python move).

Each function mirrors an existing slow PHP path but with:
- one indexed SQL statement instead of N queries,
- a hard per-statement timeout (see db.QUERY_TIMEOUT_MS),
- no schema mutation ever (ALTER/backfill live in ops scripts only).
"""

from __future__ import annotations

import re
import time
from typing import Any

from . import db

_ARTICLE_CLEAN_RE = re.compile(r"[^A-Z0-9]+")


def normalize_article(raw: str) -> str:
    """Match PHP docpart_normalize_article_for_price: uppercase alnum only."""
    return _ARTICLE_CLEAN_RE.sub("", (raw or "").upper())[:64]


def health() -> dict[str, Any]:
    started = time.monotonic()
    row = db.fetch_one("SELECT 1 AS ok")
    return {
        "status": bool(row and row.get("ok") == 1),
        "db_ms": int((time.monotonic() - started) * 1000),
    }


def part_search(article: str, limit: int = 100) -> dict[str, Any]:
    """Warehouse part search — the storefront's hottest click→result path.

    Uses the indexed article_search column (x_article_search_price index);
    excludes storefront-disabled price lists exactly like the PHP path.
    """
    norm = normalize_article(article)
    if not norm:
        return {"status": False, "error": "empty article", "rows": []}
    limit = max(1, min(500, int(limit)))
    started = time.monotonic()
    rows = db.fetch_all(
        """
        SELECT d.`price_id`, p.`name` AS `price_list`,
               d.`manufacturer`, d.`article`, d.`article_show`, d.`name`,
               d.`price`, d.`exist`, d.`storage`
        FROM `shop_docpart_prices_data` d
        INNER JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
        WHERE d.`article_search` = %s
          AND IFNULL(p.`storefront_temp_disabled`, 0) = 0
          AND IFNULL(d.`price`, 0) > 0
        ORDER BY d.`price` ASC
        LIMIT %s
        """,
        (norm, limit),
    )
    return {
        "status": True,
        "article": norm,
        "rows": rows,
        "count": len(rows),
        "took_ms": int((time.monotonic() - started) * 1000),
    }


def price_lists() -> dict[str, Any]:
    """CP price module listing with guaranteed QTY (records_count fallback)."""
    started = time.monotonic()
    lists = db.fetch_all(
        """
        SELECT `id`, `name`, `load_mode`, `last_updated`,
               COALESCE(`records_count`, 0) AS `records_count`,
               IFNULL(`storefront_temp_disabled`, 0) AS `storefront_disabled`
        FROM `shop_docpart_prices`
        ORDER BY `id`
        """
    )
    if lists and all(int(r["records_count"]) == 0 for r in lists):
        live = db.fetch_all(
            "SELECT `price_id`, COUNT(*) AS c FROM `shop_docpart_prices_data` GROUP BY `price_id`"
        )
        live_map = {int(r["price_id"]): int(r["c"]) for r in live}
        for r in lists:
            r["records_count"] = live_map.get(int(r["id"]), 0)
    return {
        "status": True,
        "lists": lists,
        "count": len(lists),
        "took_ms": int((time.monotonic() - started) * 1000),
    }


def laximo_catalogs() -> dict[str, Any]:
    """Manufacturer catalogs from the local Laximo sync tables (no SOAP call)."""
    started = time.monotonic()
    rows = db.fetch_all(
        """
        SELECT `code`, `brand`, `name`, `icon_url`, `vin_example`,
               `support_vin`, `support_wizard`, `support_quickgroups`
        FROM `epc_laximo_catalogs`
        ORDER BY `brand` ASC
        """
    )
    usable = [r for r in rows if (r.get("code") or r.get("brand") or r.get("name"))]
    return {
        "status": bool(usable),
        "catalogs": usable,
        "count": len(usable),
        "source": "db",
        "took_ms": int((time.monotonic() - started) * 1000),
    }
