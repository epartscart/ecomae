"""pyapi — FastAPI strangler service for the ecomae/epartscart PHP platform.

Consolidated layout (4 Python files):
    core.py      config + DB pool + session auth + schema
    services.py  all business logic (reads, ingest, push, worker pass)
    main.py      HTTP routes + CLI (this file)
    __init__.py

Serve the API:
    uvicorn pyapi.main:app --host 127.0.0.1 --port 8090 --workers 2

CLI (same file — fewer moving parts):
    python -m pyapi.main setup     # create push-device table (ops-only DDL)
    python -m pyapi.main once      # single worker pass (cron-friendly)
    python -m pyapi.main worker    # order/low-stock push + URL refresh loop

nginx (in the site vhost):
    location /pyapi/ { proxy_pass http://127.0.0.1:8090/pyapi/; proxy_read_timeout 10s; }
"""

from __future__ import annotations

import json
import logging
import sys
import time

from fastapi import FastAPI, Query, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel

from . import core, services
from .core import settings

log = logging.getLogger("pyapi")

app = FastAPI(
    title="ecomae pyapi",
    version="0.4.0",
    docs_url="/pyapi/docs",
    openapi_url="/pyapi/openapi.json",
)


@app.exception_handler(Exception)
async def unhandled(_, exc: Exception):
    log.exception("pyapi error: %s", exc)
    return JSONResponse(status_code=500, content={"status": False, "error": str(exc)})


@app.get("/pyapi/health")
def health():
    return services.health()


@app.get("/pyapi/v1/migration/status")
def migration_status():
    """Honest coverage report — which surfaces pyapi serves vs still PHP."""
    return {
        "service": "pyapi",
        "version": app.version,
        "files": ["core.py", "services.py", "main.py"],
        "phases": {
            "0_service": "done",
            "1_storefront_search_api": "done",
            "2_cp_erp_data_apis": "done",
            "2_session_auth": "done",
            "3_push_notifications": "done",
            "3b_price_ingest": "done",
            "3b_url_source_refresh": "done",
            "3b_retire_pyprices_cgi": "pending_cutover",
            "4_ssr_pages": "not_started",
        },
        "endpoints": [r.path for r in app.routes if getattr(r, "path", "").startswith("/pyapi")],
        "notes": (
            "API + mobile + push + ingest + URL refresh are Python. PHP still "
            "renders CP/ERP/storefront HTML until each endpoint is validated "
            "under live traffic (fallback stays). Full PHP retirement requires "
            "production cutover."
        ),
    }


# ── Public storefront reads ──────────────────────────────────────────────────

@app.get("/pyapi/v1/search")
def search(
    article: str = Query(..., min_length=1, max_length=64),
    limit: int = Query(100, ge=1, le=500),
):
    return services.part_search(article, limit)


@app.get("/pyapi/v1/brands")
def brands(limit: int = Query(5000, ge=1, le=20000)):
    return services.brands(limit)


@app.get("/pyapi/v1/laximo/catalogs")
def laximo_catalogs():
    return services.laximo_catalogs()


@app.get("/pyapi/v1/laximo/status")
def laximo_status():
    return services.laximo_status()


# ── CP / ERP endpoints — admin session cookie OR tech_key ───────────────────

def _cp_auth(request: Request, key: str = "") -> None:
    if settings.tech_key and key == settings.tech_key:
        return
    core.require_admin(request)  # raises 401 otherwise


@app.get("/pyapi/v1/prices")
def prices(request: Request, key: str = Query("")):
    _cp_auth(request, key)
    return services.price_lists()


@app.get("/pyapi/v1/upload-history")
def upload_history(
    request: Request,
    key: str = Query(""),
    price_id: int = Query(0, ge=0),
    limit: int = Query(50, ge=1, le=200),
):
    _cp_auth(request, key)
    return services.upload_history(price_id, limit)


@app.get("/pyapi/v1/commerce/sources")
def commerce_sources(request: Request, key: str = Query("")):
    _cp_auth(request, key)
    return services.commerce_sources()


@app.get("/pyapi/v1/dashboard")
def dashboard(request: Request, key: str = Query("")):
    _cp_auth(request, key)
    return services.dashboard_summary()


@app.get("/pyapi/v1/orders")
def orders(
    request: Request,
    key: str = Query(""),
    limit: int = Query(50, ge=1, le=200),
    offset: int = Query(0, ge=0),
):
    _cp_auth(request, key)
    return services.orders(limit, offset)


@app.post("/pyapi/v1/commerce/refresh-urls")
def commerce_refresh_urls(request: Request, key: str = Query(""), max_age_sec: int = Query(3600, ge=60)):
    _cp_auth(request, key)
    return services.refresh_url_sources(max_age_sec)


# ── Push notifications ───────────────────────────────────────────────────────

class DeviceIn(BaseModel):
    token: str
    platform: str = "android"
    app: str = "cp"


class PushTestIn(BaseModel):
    title: str = "Test notification"
    body: str = "pyapi push is working."
    app: str | None = None


@app.post("/pyapi/v1/push/register")
def push_register(payload: DeviceIn, request: Request):
    user_id = core.require_admin(request)
    return services.register_device(payload.token, payload.platform, user_id, payload.app)


@app.post("/pyapi/v1/push/unregister")
def push_unregister(payload: DeviceIn, request: Request):
    core.require_admin(request)
    return services.unregister_device(payload.token)


@app.post("/pyapi/v1/push/test")
def push_test(payload: PushTestIn, request: Request, key: str = Query("")):
    _cp_auth(request, key)
    return services.send_push(payload.title, payload.body, app=payload.app, data={"type": "test"})


# ── CLI: setup / once / worker (python -m pyapi.main <cmd>) ─────────────────

def _load_state() -> dict:
    try:
        return json.loads(core.state_file().read_text())
    except Exception:
        return {}


def _save_state(state: dict) -> None:
    try:
        core.state_file().write_text(json.dumps(state))
    except Exception:
        pass


def cli(argv: list[str]) -> int:
    import os

    cmd = argv[0] if argv else "worker"
    interval = int(os.environ.get("PYAPI_WORKER_INTERVAL_SEC", "30"))
    low_stock_every = int(os.environ.get("PYAPI_LOW_STOCK_EVERY_SEC", "3600"))
    url_refresh_every = int(os.environ.get("PYAPI_URL_REFRESH_EVERY_SEC", "3600"))

    if cmd == "setup":
        core.ensure_schema()
        print("epc_push_devices ready")
        return 0

    if cmd in ("once", "worker"):
        try:
            core.ensure_schema()
        except Exception as exc:
            print(f"[pyapi] schema ensure failed: {exc}", file=sys.stderr)
        state = _load_state()
        if cmd == "once":
            result = services.worker_run_once(state, low_stock_every, url_refresh_every)
            _save_state(state)
            print(json.dumps(result))
            return 0
        print(f"[pyapi] worker started (interval={interval}s)")
        while True:
            try:
                services.worker_run_once(state, low_stock_every, url_refresh_every)
                _save_state(state)
            except Exception as exc:
                print(f"[pyapi] pass error: {exc}", file=sys.stderr)
            time.sleep(interval)

    print(f"Unknown command: {cmd}. Use: setup | once | worker", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(cli(sys.argv[1:]))
