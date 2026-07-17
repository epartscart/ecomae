"""pyapi — FastAPI strangler service for the ecomae/epartscart PHP platform.

Phase 1 endpoints (hot, read-only):
    GET /pyapi/health
    GET /pyapi/v1/search?article=C110J
    GET /pyapi/v1/prices          (requires ?key=<tech_key>)
    GET /pyapi/v1/laximo/catalogs

Run:
    uvicorn pyapi.main:app --host 127.0.0.1 --port 8090 --workers 2

nginx (in the site vhost):
    location /pyapi/ { proxy_pass http://127.0.0.1:8090/pyapi/; proxy_read_timeout 10s; }
"""

from __future__ import annotations

import logging

from fastapi import Depends, FastAPI, HTTPException, Query, Request
from fastapi.responses import JSONResponse

from pydantic import BaseModel

from . import auth, push, services
from .config import settings

log = logging.getLogger("pyapi")

app = FastAPI(
    title="ecomae pyapi",
    version="0.2.0",
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


# ── CP / ERP endpoints — session-cookie auth OR tech_key (server-to-server) ──

def _cp_auth(request: Request, key: str = Query("")) -> None:
    """Allow either a valid admin session cookie or the tech_key."""
    if settings.tech_key and key == settings.tech_key:
        return
    auth.require_admin(request)  # raises 401 otherwise


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


# ── Push notifications ──────────────────────────────────────────────────────

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
    # Any authenticated admin device can register for CP/ERP alerts.
    user_id = auth.require_admin(request)
    return push.register_device(payload.token, payload.platform, user_id, payload.app)


@app.post("/pyapi/v1/push/unregister")
def push_unregister(payload: DeviceIn, request: Request):
    auth.require_admin(request)
    return push.unregister_device(payload.token)


@app.post("/pyapi/v1/push/test")
def push_test(payload: PushTestIn, request: Request, key: str = Query("")):
    _cp_auth(request, key)
    return push.send(payload.title, payload.body, app=payload.app, data={"type": "test"})
