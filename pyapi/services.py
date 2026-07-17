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


def laximo_status() -> dict[str, Any]:
    """Laximo sync snapshot (DB-only; connection test stays in PHP proxy)."""
    started = time.monotonic()
    try:
        row = db.fetch_one(
            "SELECT COUNT(*) AS cnt, MAX(`updated_at`) AS last_sync FROM `epc_laximo_catalogs`"
        ) or {}
    except Exception:
        row = {}
    cache = 0
    try:
        cr = db.fetch_one("SELECT COUNT(*) AS c FROM `epc_laximo_cache`")
        cache = int(cr["c"]) if cr else 0
    except Exception:
        cache = 0
    catalogs = int(row.get("cnt", 0) or 0)
    return {
        "status": True,
        "catalogs_count": catalogs,
        "last_sync": int(row.get("last_sync", 0) or 0),
        "cache_rows": cache,
        "offline_ready": catalogs > 0,
        "took_ms": int((time.monotonic() - started) * 1000),
    }


def brands(limit: int = 5000) -> dict[str, Any]:
    """In-stock manufacturer list with part counts (storefront brand grid).

    Index-friendly GROUP BY, excludes storefront-disabled price lists.
    """
    limit = max(1, min(20000, int(limit)))
    started = time.monotonic()
    rows = db.fetch_all(
        """
        SELECT TRIM(d.`manufacturer`) AS `name`,
               COUNT(DISTINCT COALESCE(NULLIF(TRIM(d.`article_show`), ''), TRIM(d.`article`))) AS `parts_count`
        FROM `shop_docpart_prices_data` d
        INNER JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
        WHERE TRIM(IFNULL(d.`manufacturer`, '')) <> ''
          AND IFNULL(d.`price`, 0) > 0
          AND IFNULL(d.`exist`, 0) > 0
          AND IFNULL(p.`storefront_temp_disabled`, 0) = 0
        GROUP BY UPPER(TRIM(d.`manufacturer`))
        ORDER BY UPPER(TRIM(d.`manufacturer`)) ASC
        LIMIT %s
        """,
        (limit,),
    )
    out = [r for r in rows if (r.get("name") or "").strip()]
    return {
        "status": True,
        "brands": out,
        "count": len(out),
        "took_ms": int((time.monotonic() - started) * 1000),
    }


def upload_history(price_id: int = 0, limit: int = 50) -> dict[str, Any]:
    """Recent price-list upload history (CP)."""
    limit = max(1, min(200, int(limit)))
    started = time.monotonic()
    try:
        if price_id > 0:
            rows = db.fetch_all(
                """
                SELECT `id`, `price_id`, `price_name`, `upload_source`,
                       `original_filename`, `rows_imported`, `rows_in_db`,
                       `status`, `is_active`, `created_at`
                FROM `epc_price_upload_history`
                WHERE `price_id` = %s
                ORDER BY `id` DESC LIMIT %s
                """,
                (int(price_id), limit),
            )
        else:
            rows = db.fetch_all(
                """
                SELECT `id`, `price_id`, `price_name`, `upload_source`,
                       `original_filename`, `rows_imported`, `rows_in_db`,
                       `status`, `is_active`, `created_at`
                FROM `epc_price_upload_history`
                ORDER BY `id` DESC LIMIT %s
                """,
                (limit,),
            )
    except Exception as exc:
        return {"status": False, "error": str(exc), "history": []}
    return {
        "status": True,
        "history": rows,
        "count": len(rows),
        "took_ms": int((time.monotonic() - started) * 1000),
    }


def commerce_sources() -> dict[str, Any]:
    """Commerce S/P/L price sources for the CP commerce page."""
    started = time.monotonic()
    try:
        rows = db.fetch_all(
            """
            SELECT `id`, `name`, `link`, `load_mode`, `last_updated`,
                   COALESCE(`records_count`, 0) AS `records_count`
            FROM `shop_docpart_prices`
            WHERE `name` LIKE '%-S' OR `name` LIKE '%.P' OR `name` LIKE '%-L'
               OR `message_header_substring` LIKE 'EPC_COMMERCE:%'
            ORDER BY `name` ASC
            """
        )
    except Exception as exc:
        return {"status": False, "error": str(exc), "sources": []}
    return {
        "status": True,
        "sources": rows,
        "count": len(rows),
        "took_ms": int((time.monotonic() - started) * 1000),
    }


def dashboard_summary() -> dict[str, Any]:
    """KPI tiles for the CP/ERP mobile dashboard — each guarded so one missing
    table never breaks the whole payload."""
    started = time.monotonic()
    kpis: dict[str, Any] = {}

    def _count(key: str, sql: str) -> None:
        try:
            row = db.fetch_one(sql)
            kpis[key] = int(row["c"]) if row else 0
        except Exception:
            kpis[key] = None

    _count("price_lists", "SELECT COUNT(*) AS c FROM `shop_docpart_prices`")
    _count("orders_total", "SELECT COUNT(*) AS c FROM `shop_orders`")
    _count(
        "orders_unread",
        "SELECT COUNT(*) AS c FROM `shop_orders` WHERE IFNULL(`read`, 0) = 0",
    )
    _count("products", "SELECT COUNT(*) AS c FROM `shop_catalogue_products`")
    _count("customers", "SELECT COUNT(*) AS c FROM `users` WHERE `unlocked` = 1")

    return {
        "status": True,
        "kpis": kpis,
        "took_ms": int((time.monotonic() - started) * 1000),
    }


def orders(limit: int = 50, offset: int = 0) -> dict[str, Any]:
    """Paginated order list for the CP/ERP mobile app."""
    limit = max(1, min(200, int(limit)))
    offset = max(0, int(offset))
    started = time.monotonic()
    try:
        rows = db.fetch_all(
            """
            SELECT `id`, `date`, `status_id`, `summ`, `read`, `client_name`, `client_phone`
            FROM `shop_orders`
            ORDER BY `id` DESC
            LIMIT %s OFFSET %s
            """,
            (limit, offset),
        )
    except Exception as exc:
        return {"status": False, "error": str(exc), "orders": []}
    return {
        "status": True,
        "orders": rows,
        "count": len(rows),
        "limit": limit,
        "offset": offset,
        "took_ms": int((time.monotonic() - started) * 1000),
    }
