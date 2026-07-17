"""pyapi services — all business logic in one module.

Sections:
  1. Storefront reads   (part_search, brands, laximo_*)
  2. CP / ERP reads     (price_lists, upload_history, commerce_sources,
                         dashboard_summary, orders)
  3. Price ingest       (parse_csv, import_csv, refresh_url_sources) — writes
                         records_count + article_search AT INGEST, so nothing
                         backfills on the request path
  4. Push notifications (device registry, FCM send, order/low-stock events)
  5. Worker pass        (run_once — polled by `python -m pyapi.main worker`)

Rules: one indexed statement per query where possible, 3s hard timeout
(core.QUERY_TIMEOUT_MS), and no schema mutation outside `pyapi.main setup`.
"""

from __future__ import annotations

import csv
import io
import json
import os
import re
import time
import urllib.request
from typing import Any

from . import core

_ARTICLE_CLEAN_RE = re.compile(r"[^A-Z0-9]+")


def normalize_article(raw: str) -> str:
    """Match PHP docpart_normalize_article_for_price: uppercase alnum only."""
    return _ARTICLE_CLEAN_RE.sub("", (raw or "").upper())[:64]


def health() -> dict[str, Any]:
    started = time.monotonic()
    row = core.fetch_one("SELECT 1 AS ok")
    return {
        "status": bool(row and row.get("ok") == 1),
        "db_ms": int((time.monotonic() - started) * 1000),
    }


# ── 1. Storefront reads ──────────────────────────────────────────────────────

def part_search(article: str, limit: int = 100) -> dict[str, Any]:
    """Warehouse part search — the storefront's hottest click→result path."""
    norm = normalize_article(article)
    if not norm:
        return {"status": False, "error": "empty article", "rows": []}
    limit = max(1, min(500, int(limit)))
    started = time.monotonic()
    rows = core.fetch_all(
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


def brands(limit: int = 5000) -> dict[str, Any]:
    """In-stock manufacturer list with part counts (storefront brand grid)."""
    limit = max(1, min(20000, int(limit)))
    started = time.monotonic()
    rows = core.fetch_all(
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


def laximo_catalogs() -> dict[str, Any]:
    """Manufacturer catalogs from the local Laximo sync tables (no SOAP call)."""
    started = time.monotonic()
    rows = core.fetch_all(
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
    """Laximo sync snapshot (DB-only; the SOAP connection test stays in PHP)."""
    started = time.monotonic()
    try:
        row = core.fetch_one(
            "SELECT COUNT(*) AS cnt, MAX(`updated_at`) AS last_sync FROM `epc_laximo_catalogs`"
        ) or {}
    except Exception:
        row = {}
    cache = 0
    try:
        cr = core.fetch_one("SELECT COUNT(*) AS c FROM `epc_laximo_cache`")
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


# ── 2. CP / ERP reads ────────────────────────────────────────────────────────

def price_lists() -> dict[str, Any]:
    """CP price module listing with guaranteed QTY (records_count fallback)."""
    started = time.monotonic()
    lists = core.fetch_all(
        """
        SELECT `id`, `name`, `load_mode`, `last_updated`,
               COALESCE(`records_count`, 0) AS `records_count`,
               IFNULL(`storefront_temp_disabled`, 0) AS `storefront_disabled`
        FROM `shop_docpart_prices`
        ORDER BY `id`
        """
    )
    if lists and all(int(r["records_count"]) == 0 for r in lists):
        live = core.fetch_all(
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


def upload_history(price_id: int = 0, limit: int = 50) -> dict[str, Any]:
    limit = max(1, min(200, int(limit)))
    started = time.monotonic()
    base = """
        SELECT `id`, `price_id`, `price_name`, `upload_source`,
               `original_filename`, `rows_imported`, `rows_in_db`,
               `status`, `is_active`, `created_at`
        FROM `epc_price_upload_history`
    """
    try:
        if price_id > 0:
            rows = core.fetch_all(
                base + " WHERE `price_id` = %s ORDER BY `id` DESC LIMIT %s",
                (int(price_id), limit),
            )
        else:
            rows = core.fetch_all(base + " ORDER BY `id` DESC LIMIT %s", (limit,))
    except Exception as exc:
        return {"status": False, "error": str(exc), "history": []}
    return {
        "status": True,
        "history": rows,
        "count": len(rows),
        "took_ms": int((time.monotonic() - started) * 1000),
    }


def commerce_sources() -> dict[str, Any]:
    started = time.monotonic()
    try:
        rows = core.fetch_all(
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
    """KPI tiles — each guarded so one missing table never breaks the payload."""
    started = time.monotonic()
    kpis: dict[str, Any] = {}

    def _count(key: str, sql: str) -> None:
        try:
            row = core.fetch_one(sql)
            kpis[key] = int(row["c"]) if row else 0
        except Exception:
            kpis[key] = None

    _count("price_lists", "SELECT COUNT(*) AS c FROM `shop_docpart_prices`")
    _count("orders_total", "SELECT COUNT(*) AS c FROM `shop_orders`")
    _count("orders_unread", "SELECT COUNT(*) AS c FROM `shop_orders` WHERE IFNULL(`read`, 0) = 0")
    _count("products", "SELECT COUNT(*) AS c FROM `shop_catalogue_products`")
    _count("customers", "SELECT COUNT(*) AS c FROM `users` WHERE `unlocked` = 1")

    return {
        "status": True,
        "kpis": kpis,
        "took_ms": int((time.monotonic() - started) * 1000),
    }


def orders(limit: int = 50, offset: int = 0) -> dict[str, Any]:
    limit = max(1, min(200, int(limit)))
    offset = max(0, int(offset))
    started = time.monotonic()
    try:
        rows = core.fetch_all(
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


# ── 3. Price ingest ──────────────────────────────────────────────────────────

def _num(raw: Any) -> float:
    s = str(raw or "").strip().replace(" ", "").replace(",", ".")
    out: list[str] = []
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
    """CSV → normalized price rows (manufacturer, article, name, exist, price).
    Pure parsing — no DB. article_search is computed here, at ingest."""
    text = content.decode("utf-8", errors="replace") if isinstance(content, bytes) else content
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
            "article_search": article,
            "name": name,
            "exist": exist,
            "price": price,
        })
    return {"rows": rows, "skipped": skipped}


def import_csv(price_id: int, content: bytes | str, has_header: bool = True) -> dict[str, Any]:
    """Replace a price list's rows from CSV; records_count + article_search
    written at ingest so nothing recomputes on the request path."""
    price_id = int(price_id)
    if price_id <= 0:
        return {"status": False, "error": "invalid price_id"}

    parsed = parse_csv(content, has_header)
    rows = parsed["rows"]
    started = time.monotonic()

    try:
        core.execute("DELETE FROM `shop_docpart_prices_data` WHERE `price_id` = %s", (price_id,))
        if rows:
            with core.cursor() as cur:
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
        core.execute(
            "UPDATE `shop_docpart_prices` SET `last_updated` = %s, `records_count` = %s WHERE `id` = %s",
            (core.now_ts(), len(rows), price_id),
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


def _http_get(url: str, timeout: int = 20) -> bytes:
    """Download a price CSV. Test seam — monkeypatched in unit tests."""
    req = urllib.request.Request(url, headers={"User-Agent": "pyapi-ingest/1.0"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.read()


def refresh_url_sources(max_age_sec: int = 3600) -> dict[str, Any]:
    """Re-import URL-based price lists (load_mode=4, http link) that are stale.
    Replaces the PHP cron path for the common URL-refresh case."""
    started = time.monotonic()
    try:
        lists = core.fetch_all(
            """
            SELECT `id`, `name`, `link`, IFNULL(`last_updated`, 0) AS `last_updated`
            FROM `shop_docpart_prices`
            WHERE `load_mode` = 4 AND `link` LIKE 'http%'
            ORDER BY `id`
            """
        )
    except Exception as exc:
        return {"status": False, "error": str(exc), "refreshed": []}

    cutoff = core.now_ts() - max(60, int(max_age_sec))
    refreshed: list[dict] = []
    for row in lists:
        if int(row["last_updated"]) >= cutoff:
            continue
        try:
            content = _http_get(str(row["link"]))
            result = import_csv(int(row["id"]), content)
        except Exception as exc:
            result = {"status": False, "error": str(exc)}
        refreshed.append({"id": int(row["id"]), "name": row["name"], **{
            k: result.get(k) for k in ("status", "imported", "skipped", "error") if k in result
        }})
    return {
        "status": True,
        "checked": len(lists),
        "refreshed": refreshed,
        "took_ms": int((time.monotonic() - started) * 1000),
    }


# ── 4. Push notifications ────────────────────────────────────────────────────

FCM_ENDPOINT_TMPL = "https://fcm.googleapis.com/v1/projects/{project}/messages:send"


def register_device(token: str, platform: str, user_id: int, app: str = "cp") -> dict[str, Any]:
    token = (token or "").strip()
    platform = platform if platform in ("android", "ios", "web") else "android"
    app = "".join(ch for ch in (app or "cp") if ch.isalnum() or ch in "-_")[:32] or "cp"
    if not token:
        return {"status": False, "error": "empty token"}
    try:
        core.execute(
            """
            INSERT INTO `epc_push_devices` (`token`, `platform`, `user_id`, `app`, `updated_at`)
            VALUES (%s, %s, %s, %s, %s)
            ON DUPLICATE KEY UPDATE
                `platform` = VALUES(`platform`),
                `user_id`  = VALUES(`user_id`),
                `app`      = VALUES(`app`),
                `updated_at` = VALUES(`updated_at`)
            """,
            (token, platform, int(user_id), app, core.now_ts()),
        )
    except Exception as exc:
        return {"status": False, "error": str(exc)}
    return {"status": True, "registered": True, "platform": platform, "app": app}


def unregister_device(token: str) -> dict[str, Any]:
    token = (token or "").strip()
    if not token:
        return {"status": False, "error": "empty token"}
    try:
        core.execute("DELETE FROM `epc_push_devices` WHERE `token` = %s", (token,))
    except Exception as exc:
        return {"status": False, "error": str(exc)}
    return {"status": True, "unregistered": True}


def active_tokens(app: str | None = None) -> list[dict]:
    try:
        if app:
            return core.fetch_all(
                "SELECT `token`, `platform`, `app` FROM `epc_push_devices` WHERE `app` = %s",
                (app,),
            )
        return core.fetch_all("SELECT `token`, `platform`, `app` FROM `epc_push_devices`")
    except Exception:
        return []


def _fcm_post(project: str, bearer: str, payload: dict) -> tuple[bool, str]:
    """POST one message to FCM (relays to APNs for iOS). Test seam."""
    url = FCM_ENDPOINT_TMPL.format(project=project)
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, method="POST")
    req.add_header("Authorization", f"Bearer {bearer}")
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=8) as resp:
            return 200 <= resp.status < 300, f"http {resp.status}"
    except Exception as exc:
        return False, str(exc)


_transport = _fcm_post


def send_push(title: str, body: str, app: str | None = None, data: dict | None = None) -> dict[str, Any]:
    project = os.environ.get("PYAPI_FCM_PROJECT", "")
    bearer = os.environ.get("PYAPI_FCM_ACCESS_TOKEN", "")
    devices = active_tokens(app)
    if not project or not bearer:
        return {
            "status": False,
            "reason": "not_configured",
            "devices": len(devices),
            "hint": "Set PYAPI_FCM_PROJECT + PYAPI_FCM_ACCESS_TOKEN to enable sending.",
        }
    if not devices:
        return {"status": True, "sent": 0, "devices": 0}

    sent = 0
    failed = 0
    for d in devices:
        message = {
            "message": {
                "token": d["token"],
                "notification": {"title": title, "body": body},
                "data": {k: str(v) for k, v in (data or {}).items()},
            }
        }
        ok, _info = _transport(project, bearer, message)
        sent += 1 if ok else 0
        failed += 0 if ok else 1
    return {"status": True, "sent": sent, "failed": failed, "devices": len(devices)}


def notify_new_orders(since_order_id: int, app: str = "cp") -> dict[str, Any]:
    try:
        rows = core.fetch_all(
            "SELECT `id`, `summ`, `client_name` FROM `shop_orders` WHERE `id` > %s ORDER BY `id` ASC LIMIT 50",
            (int(since_order_id),),
        )
    except Exception as exc:
        return {"status": False, "error": str(exc), "last_id": since_order_id}
    if not rows:
        return {"status": True, "new_orders": 0, "last_id": since_order_id}
    last_id = max(int(o["id"]) for o in rows)
    count = len(rows)
    title = "New order" if count == 1 else f"{count} new orders"
    latest = rows[-1]
    body = f"Order #{latest['id']} — {latest.get('client_name') or 'customer'} ({latest.get('summ') or 0})"
    result = send_push(title, body, app=app, data={"type": "order", "order_id": last_id})
    result["new_orders"] = count
    result["last_id"] = last_id
    return result


def notify_low_stock(app: str = "cp") -> dict[str, Any]:
    try:
        row = core.fetch_one(
            "SELECT COUNT(`id`) AS c FROM `shop_catalogue_products` WHERE `min_limit_status` = '1'"
        )
        count = int(row["c"]) if row else 0
    except Exception as exc:
        return {"status": False, "error": str(exc)}
    if count <= 0:
        return {"status": True, "low_stock": 0}
    result = send_push(
        "Low stock",
        f"{count} product(s) at or below minimum stock",
        app=app,
        data={"type": "low_stock", "count": count},
    )
    result["low_stock"] = count
    return result


# ── 5. Worker pass ───────────────────────────────────────────────────────────

def _max_order_id() -> int:
    try:
        row = core.fetch_one("SELECT MAX(`id`) AS m FROM `shop_orders`")
        return int(row["m"]) if row and row.get("m") is not None else 0
    except Exception:
        return 0


def worker_run_once(state: dict, low_stock_every: int = 3600, url_refresh_every: int = 3600) -> dict[str, Any]:
    """One dispatcher pass: order alerts, low-stock alert, URL price refresh."""
    now = core.now_ts()

    # First run baselines to "now" — never notify the entire order backlog.
    if "last_order_id" not in state:
        state["last_order_id"] = _max_order_id()

    order_res = notify_new_orders(int(state.get("last_order_id", 0)), app="cp")
    if order_res.get("status") and "last_id" in order_res:
        state["last_order_id"] = order_res["last_id"]

    low_res: dict[str, Any] = {"skipped": True}
    if now - int(state.get("last_low_stock_ts", 0)) >= low_stock_every:
        low_res = notify_low_stock(app="cp")
        state["last_low_stock_ts"] = now

    url_res: dict[str, Any] = {"skipped": True}
    if os.environ.get("PYAPI_URL_REFRESH", "0") == "1" \
            and now - int(state.get("last_url_refresh_ts", 0)) >= url_refresh_every:
        url_res = refresh_url_sources(max_age_sec=url_refresh_every)
        state["last_url_refresh_ts"] = now

    return {"orders": order_res, "low_stock": low_res, "url_refresh": url_res, "state": state}
