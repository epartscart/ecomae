"""Native push notifications wired to pyapi.

- Device tokens (Capacitor PushNotifications: FCM on Android, APNs-via-FCM on iOS)
  are stored in `epc_push_devices`.
- Dispatch goes through Firebase Cloud Messaging HTTP v1. FCM relays to APNs for
  iOS, so one transport covers both platforms.
- If FCM credentials are not configured, send() is a safe no-op that reports
  "not_configured" (nothing breaks; tokens still register).

Schema is created by the ops step (pyapi.ops.push_setup / epc-pyapi-push-setup.php),
never on the request path — same rule as the rest of the migration.
"""

from __future__ import annotations

import json
import os
import time
import urllib.request
from typing import Any

from . import db

FCM_ENDPOINT_TMPL = "https://fcm.googleapis.com/v1/projects/{project}/messages:send"


# ── Device registry ─────────────────────────────────────────────────────────

def register_device(token: str, platform: str, user_id: int, app: str = "cp") -> dict[str, Any]:
    token = (token or "").strip()
    platform = platform if platform in ("android", "ios", "web") else "android"
    app = "".join(ch for ch in (app or "cp") if ch.isalnum() or ch in "-_")[:32] or "cp"
    if not token:
        return {"status": False, "error": "empty token"}
    try:
        db.execute(
            """
            INSERT INTO `epc_push_devices` (`token`, `platform`, `user_id`, `app`, `updated_at`)
            VALUES (%s, %s, %s, %s, %s)
            ON DUPLICATE KEY UPDATE
                `platform` = VALUES(`platform`),
                `user_id`  = VALUES(`user_id`),
                `app`      = VALUES(`app`),
                `updated_at` = VALUES(`updated_at`)
            """,
            (token, platform, int(user_id), app, int(time.time())),
        )
    except Exception as exc:
        return {"status": False, "error": str(exc)}
    return {"status": True, "registered": True, "platform": platform, "app": app}


def unregister_device(token: str) -> dict[str, Any]:
    token = (token or "").strip()
    if not token:
        return {"status": False, "error": "empty token"}
    try:
        db.execute("DELETE FROM `epc_push_devices` WHERE `token` = %s", (token,))
    except Exception as exc:
        return {"status": False, "error": str(exc)}
    return {"status": True, "unregistered": True}


def active_tokens(app: str | None = None) -> list[dict]:
    """Return device rows, optionally filtered to one app (cp / erp / storefront)."""
    try:
        if app:
            return db.fetch_all(
                "SELECT `token`, `platform`, `app` FROM `epc_push_devices` WHERE `app` = %s",
                (app,),
            )
        return db.fetch_all("SELECT `token`, `platform`, `app` FROM `epc_push_devices`")
    except Exception:
        return []


# ── FCM transport ───────────────────────────────────────────────────────────

def _fcm_config() -> dict[str, str]:
    return {
        "project": os.environ.get("PYAPI_FCM_PROJECT", ""),
        "token": os.environ.get("PYAPI_FCM_ACCESS_TOKEN", ""),
    }


def _fcm_post(project: str, bearer: str, payload: dict) -> tuple[bool, str]:
    """POST one message to FCM. Injected/overridable for tests."""
    url = FCM_ENDPOINT_TMPL.format(project=project)
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, method="POST")
    req.add_header("Authorization", f"Bearer {bearer}")
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=8) as resp:
            return 200 <= resp.status < 300, f"http {resp.status}"
    except Exception as exc:  # network / auth error — never crash the caller
        return False, str(exc)


# Test seam: monkeypatch this to avoid real network in unit tests.
_transport = _fcm_post


def send(title: str, body: str, app: str | None = None, data: dict | None = None) -> dict[str, Any]:
    """Send a push to every registered device (optionally scoped to one app)."""
    cfg = _fcm_config()
    devices = active_tokens(app)
    if not cfg["project"] or not cfg["token"]:
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
        ok, _info = _transport(cfg["project"], cfg["token"], message)
        if ok:
            sent += 1
        else:
            failed += 1
    return {"status": True, "sent": sent, "failed": failed, "devices": len(devices)}


# ── Event helpers ───────────────────────────────────────────────────────────

def notify_new_orders(since_order_id: int, app: str = "cp") -> dict[str, Any]:
    """Push for orders newer than since_order_id. Returns the new high-water id."""
    try:
        rows = db.fetch_all(
            "SELECT `id`, `summ`, `client_name` FROM `shop_orders` WHERE `id` > %s ORDER BY `id` ASC LIMIT 50",
            (int(since_order_id),),
        )
    except Exception as exc:
        return {"status": False, "error": str(exc), "last_id": since_order_id}
    if not rows:
        return {"status": True, "new_orders": 0, "last_id": since_order_id}
    last_id = since_order_id
    for o in rows:
        last_id = max(last_id, int(o["id"]))
    count = len(rows)
    title = "New order" if count == 1 else f"{count} new orders"
    latest = rows[-1]
    body = f"Order #{latest['id']} — {latest.get('client_name') or 'customer'} ({latest.get('summ') or 0})"
    result = send(title, body, app=app, data={"type": "order", "order_id": last_id})
    result["new_orders"] = count
    result["last_id"] = last_id
    return result


def notify_low_stock(app: str = "cp") -> dict[str, Any]:
    """Push a low-stock alert when products cross the min-limit threshold."""
    try:
        row = db.fetch_one(
            "SELECT COUNT(`id`) AS c FROM `shop_catalogue_products` WHERE `min_limit_status` = '1'"
        )
        count = int(row["c"]) if row else 0
    except Exception as exc:
        return {"status": False, "error": str(exc)}
    if count <= 0:
        return {"status": True, "low_stock": 0}
    result = send(
        "Low stock",
        f"{count} product(s) at or below minimum stock",
        app=app,
        data={"type": "low_stock", "count": count},
    )
    result["low_stock"] = count
    return result
