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

from fastapi import FastAPI, HTTPException, Query
from fastapi.responses import JSONResponse

from . import services
from .config import settings

log = logging.getLogger("pyapi")

app = FastAPI(
    title="ecomae pyapi",
    version="0.1.0",
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


@app.get("/pyapi/v1/prices")
def prices(key: str = Query("")):
    # CP data — same tech_key gate as the PHP AJAX endpoints.
    if not settings.tech_key or key != settings.tech_key:
        raise HTTPException(status_code=403, detail="Invalid key")
    return services.price_lists()


@app.get("/pyapi/v1/laximo/catalogs")
def laximo_catalogs():
    return services.laximo_catalogs()
