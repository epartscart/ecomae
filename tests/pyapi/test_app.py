"""pyapi unit tests — DB layer mocked, no MySQL needed.

Run: python3 -m pytest tests/pyapi/ -q
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from fastapi.testclient import TestClient  # noqa: E402

from pyapi import core, services  # noqa: E402
from pyapi.main import app  # noqa: E402


@pytest.fixture()
def client():
    return TestClient(app, raise_server_exceptions=False)


@pytest.fixture()
def admin_ok(monkeypatch):
    monkeypatch.setattr(core, "_valid_session", lambda s, u, admin: True)


@pytest.fixture()
def tech_key(monkeypatch):
    monkeypatch.setattr(core.Settings, "tech_key", property(lambda self: "sekret"))


# ── Core / storefront ──

def test_normalize_article():
    assert services.normalize_article(" c-110/j ") == "C110J"
    assert services.normalize_article("dt.068") == "DT068"
    assert services.normalize_article("") == ""
    assert services.normalize_article("!!!") == ""


def test_health(monkeypatch, client):
    monkeypatch.setattr(core, "fetch_one", lambda sql, params=(): {"ok": 1})
    r = client.get("/pyapi/health")
    assert r.status_code == 200
    assert r.json()["status"] is True


def test_search_returns_rows(monkeypatch, client):
    rows = [
        {"price_id": 3, "price_list": "UAE-S", "manufacturer": "TOYOTA",
         "article": "C110J", "article_show": "C110J", "name": "Filter",
         "price": 25.0, "exist": 4, "storage": "DXB"},
    ]
    captured = {}

    def fake_fetch_all(sql, params=()):
        captured["params"] = params
        return rows

    monkeypatch.setattr(core, "fetch_all", fake_fetch_all)
    r = client.get("/pyapi/v1/search", params={"article": "c-110j"})
    assert r.status_code == 200
    body = r.json()
    assert body["status"] is True
    assert body["count"] == 1
    assert body["article"] == "C110J"
    assert captured["params"][0] == "C110J"


def test_search_rejects_empty_article(client):
    r = client.get("/pyapi/v1/search", params={"article": "///"})
    assert r.status_code == 200
    assert r.json()["status"] is False


def test_brands_public(monkeypatch, client):
    monkeypatch.setattr(
        core, "fetch_all",
        lambda sql, params=(): [
            {"name": "TOYOTA", "parts_count": 1200},
            {"name": "  ", "parts_count": 5},
        ],
    )
    r = client.get("/pyapi/v1/brands")
    assert r.status_code == 200
    body = r.json()
    assert body["count"] == 1
    assert body["brands"][0]["name"] == "TOYOTA"


def test_laximo_catalogs_filters_junk(monkeypatch, client):
    monkeypatch.setattr(
        core, "fetch_all",
        lambda sql, params=(): [
            {"code": "TOYOTA01", "brand": "TOYOTA", "name": "Toyota",
             "icon_url": "", "vin_example": "", "support_vin": 1,
             "support_wizard": 1, "support_quickgroups": 1},
            {"code": "", "brand": "", "name": "", "icon_url": "",
             "vin_example": "", "support_vin": 0, "support_wizard": 0,
             "support_quickgroups": 0},
        ],
    )
    r = client.get("/pyapi/v1/laximo/catalogs")
    assert r.status_code == 200
    body = r.json()
    assert body["count"] == 1
    assert body["catalogs"][0]["code"] == "TOYOTA01"


def test_laximo_status(monkeypatch, client):
    def fake_one(sql, params=()):
        if "epc_laximo_catalogs" in sql:
            return {"cnt": 56, "last_sync": 123}
        return {"c": 1}
    monkeypatch.setattr(core, "fetch_one", fake_one)
    r = client.get("/pyapi/v1/laximo/status")
    assert r.status_code == 200
    body = r.json()
    assert body["catalogs_count"] == 56
    assert body["offline_ready"] is True


# ── CP auth ──

def test_cp_endpoint_requires_auth(client):
    assert client.get("/pyapi/v1/dashboard").status_code == 401
    assert client.get("/pyapi/v1/orders").status_code == 401
    assert client.get("/pyapi/v1/prices").status_code == 401


def test_cp_endpoint_rejects_wrong_key(tech_key, client):
    assert client.get("/pyapi/v1/prices", params={"key": "wrong"}).status_code == 401


def test_cp_endpoint_accepts_tech_key(tech_key, monkeypatch, client):
    monkeypatch.setattr(core, "fetch_one", lambda sql, params=(): {"c": 7})
    r = client.get("/pyapi/v1/dashboard", params={"key": "sekret"})
    assert r.status_code == 200
    assert r.json()["kpis"]["price_lists"] == 7


def test_cp_endpoint_accepts_admin_cookie(admin_ok, monkeypatch, client):
    monkeypatch.setattr(core, "fetch_one", lambda sql, params=(): {"c": 3})
    client.cookies.set("admin_session", "abc")
    client.cookies.set("admin_u_id", "18")
    r = client.get("/pyapi/v1/dashboard")
    assert r.status_code == 200
    assert "kpis" in r.json()


def test_prices_qty_fallback(tech_key, monkeypatch, client):
    calls = []

    def fake_fetch_all(sql, params=()):
        calls.append(sql)
        if "GROUP BY `price_id`" in sql:
            return [{"price_id": 3, "c": 1200}, {"price_id": 8, "c": 340}]
        return [
            {"id": 3, "name": "UAE-S", "load_mode": 1, "last_updated": 0,
             "records_count": 0, "storefront_disabled": 0},
            {"id": 8, "name": "GCC.P", "load_mode": 1, "last_updated": 0,
             "records_count": 0, "storefront_disabled": 0},
        ]

    monkeypatch.setattr(core, "fetch_all", fake_fetch_all)
    r = client.get("/pyapi/v1/prices", params={"key": "sekret"})
    assert r.status_code == 200
    lists = {row["id"]: row["records_count"] for row in r.json()["lists"]}
    assert lists == {3: 1200, 8: 340}
    assert len(calls) == 2


def test_orders_with_key(tech_key, monkeypatch, client):
    monkeypatch.setattr(
        core, "fetch_all",
        lambda sql, params=(): [{"id": 5, "date": 0, "status_id": 1, "summ": 99.0,
                                 "read": 0, "client_name": "A", "client_phone": "1"}],
    )
    r = client.get("/pyapi/v1/orders", params={"key": "sekret", "limit": 10})
    assert r.status_code == 200
    assert r.json()["orders"][0]["id"] == 5


# ── Ingest ──

def test_ingest_parse_csv():
    csv_bytes = (
        b"manufacturer,article,name,exist,price\n"
        b"TOYOTA,C-110/J,Oil filter,4,25.50\n"
        b"BOSCH,DT.068,Brake pad,yes,90\n"
        b"BADROW,,No article,3,10\n"
        b"NOPRICE,X99,Zero price,2,0\n"
    )
    out = services.parse_csv(csv_bytes)
    assert len(out["rows"]) == 2
    assert out["skipped"] == 2
    assert out["rows"][0]["article"] == "C110J"
    assert out["rows"][0]["article_search"] == "C110J"
    assert out["rows"][0]["price"] == 25.5
    assert out["rows"][1]["exist"] == 999


def test_ingest_import_writes_records_count(monkeypatch):
    calls = {"delete": 0, "update": None}

    def fake_execute(sql, params=()):
        if sql.strip().startswith("DELETE"):
            calls["delete"] += 1
        elif "records_count" in sql:
            calls["update"] = params
        return 1

    class FakeCur:
        def executemany(self, sql, seq):
            calls["rows"] = len(list(seq))
        def execute(self, *a, **k):
            pass
        def close(self):
            pass

    class FakeCtx:
        def __enter__(self):
            return FakeCur()
        def __exit__(self, *a):
            return False

    monkeypatch.setattr(core, "execute", fake_execute)
    monkeypatch.setattr(core, "cursor", lambda: FakeCtx())
    res = services.import_csv(3, b"manufacturer,article,name,exist,price\nTOYOTA,C110J,Filter,4,25\n")
    assert res["status"] is True
    assert res["imported"] == 1
    assert res["records_count"] == 1
    assert calls["delete"] == 1
    assert calls["update"][1] == 1


def test_refresh_url_sources(monkeypatch):
    monkeypatch.setattr(
        core, "fetch_all",
        lambda sql, params=(): [
            {"id": 4, "name": "UAE-S", "link": "https://x/prices.csv", "last_updated": 0},
            {"id": 5, "name": "Fresh", "link": "https://x/f.csv", "last_updated": core.now_ts()},
        ],
    )
    monkeypatch.setattr(services, "_http_get", lambda url, timeout=20: b"m,a,n,e,p\nTOYOTA,C1,F,2,9\n")
    imported = {}

    def fake_import(pid, content):
        imported[pid] = True
        return {"status": True, "imported": 1, "skipped": 0}

    monkeypatch.setattr(services, "import_csv", fake_import)
    res = services.refresh_url_sources(max_age_sec=3600)
    assert res["status"] is True
    assert res["checked"] == 2
    assert len(res["refreshed"]) == 1        # only the stale one
    assert res["refreshed"][0]["id"] == 4


# ── Push ──

def test_push_register_requires_admin(client):
    r = client.post("/pyapi/v1/push/register", json={"token": "abc", "platform": "android"})
    assert r.status_code == 401


def test_push_register_ok(admin_ok, monkeypatch, client):
    captured = {}
    monkeypatch.setattr(core, "execute", lambda sql, params=(): captured.setdefault("p", params) or 1)
    client.cookies.set("admin_session", "s")
    client.cookies.set("admin_u_id", "18")
    r = client.post("/pyapi/v1/push/register", json={"token": "tok123", "platform": "ios", "app": "cp"})
    assert r.status_code == 200
    body = r.json()
    assert body["status"] is True
    assert body["platform"] == "ios"
    assert captured["p"][0] == "tok123"


def test_push_send_not_configured(monkeypatch):
    monkeypatch.delenv("PYAPI_FCM_PROJECT", raising=False)
    monkeypatch.delenv("PYAPI_FCM_ACCESS_TOKEN", raising=False)
    monkeypatch.setattr(services, "active_tokens", lambda app=None: [{"token": "a", "platform": "android", "app": "cp"}])
    res = services.send_push("Hi", "There")
    assert res["status"] is False
    assert res["reason"] == "not_configured"
    assert res["devices"] == 1


def test_push_send_dispatches(monkeypatch):
    monkeypatch.setenv("PYAPI_FCM_PROJECT", "proj")
    monkeypatch.setenv("PYAPI_FCM_ACCESS_TOKEN", "bearer")
    monkeypatch.setattr(
        services, "active_tokens",
        lambda app=None: [
            {"token": "a", "platform": "android", "app": "cp"},
            {"token": "b", "platform": "ios", "app": "cp"},
        ],
    )
    sent = []
    monkeypatch.setattr(services, "_transport", lambda project, bearer, payload: (sent.append(payload) or (True, "ok")))
    res = services.send_push("New order", "Order #5", app="cp", data={"type": "order"})
    assert res["status"] is True
    assert res["sent"] == 2
    assert sent[0]["message"]["notification"]["title"] == "New order"


def test_notify_new_orders(monkeypatch):
    monkeypatch.setattr(
        core, "fetch_all",
        lambda sql, params=(): [
            {"id": 10, "summ": 50, "client_name": "A"},
            {"id": 12, "summ": 90, "client_name": "B"},
        ],
    )
    monkeypatch.setattr(services, "send_push", lambda title, body, app=None, data=None: {"status": True, "sent": 1})
    res = services.notify_new_orders(9, app="cp")
    assert res["new_orders"] == 2
    assert res["last_id"] == 12


def test_notify_new_orders_none(monkeypatch):
    monkeypatch.setattr(core, "fetch_all", lambda sql, params=(): [])
    called = {"n": 0}
    monkeypatch.setattr(services, "send_push", lambda *a, **k: called.__setitem__("n", called["n"] + 1) or {})
    res = services.notify_new_orders(99, app="cp")
    assert res["new_orders"] == 0
    assert res["last_id"] == 99
    assert called["n"] == 0


def test_notify_low_stock(monkeypatch):
    monkeypatch.setattr(core, "fetch_one", lambda sql, params=(): {"c": 4})
    monkeypatch.setattr(services, "send_push", lambda title, body, app=None, data=None: {"status": True, "sent": 1})
    res = services.notify_low_stock(app="cp")
    assert res["low_stock"] == 4


# ── Worker ──

def test_worker_run_once_baseline(monkeypatch):
    monkeypatch.setattr(services, "_max_order_id", lambda: 100)
    seen = {}

    def fake_notify(since, app="cp"):
        seen["since"] = since
        return {"status": True, "last_id": since, "new_orders": 0}

    monkeypatch.setattr(services, "notify_new_orders", fake_notify)
    monkeypatch.setattr(services, "notify_low_stock", lambda app="cp": {"status": True, "low_stock": 0})
    state: dict = {}
    services.worker_run_once(state, low_stock_every=3600)
    assert state["last_order_id"] == 100
    assert seen["since"] == 100


def test_worker_url_refresh_gated(monkeypatch):
    monkeypatch.setattr(services, "_max_order_id", lambda: 1)
    monkeypatch.setattr(services, "notify_new_orders", lambda since, app="cp": {"status": True, "last_id": since, "new_orders": 0})
    monkeypatch.setattr(services, "notify_low_stock", lambda app="cp": {"status": True, "low_stock": 0})
    called = {"n": 0}
    monkeypatch.setattr(services, "refresh_url_sources", lambda max_age_sec=3600: called.__setitem__("n", called["n"] + 1) or {"status": True})
    # Flag off → no URL refresh
    monkeypatch.delenv("PYAPI_URL_REFRESH", raising=False)
    out = services.worker_run_once({}, 3600, 3600)
    assert out["url_refresh"] == {"skipped": True}
    assert called["n"] == 0
    # Flag on → runs
    monkeypatch.setenv("PYAPI_URL_REFRESH", "1")
    services.worker_run_once({}, 3600, 3600)
    assert called["n"] == 1


# ── Meta ──

def test_migration_status(client):
    r = client.get("/pyapi/v1/migration/status")
    assert r.status_code == 200
    body = r.json()
    assert body["phases"]["3b_price_ingest"] == "done"
    assert body["phases"]["3b_url_source_refresh"] == "done"
    assert body["phases"]["4_ssr_pages"] == "not_started"
    assert body["files"] == ["core.py", "services.py", "main.py"]
    assert any(p.startswith("/pyapi/v1/push") for p in body["endpoints"])
