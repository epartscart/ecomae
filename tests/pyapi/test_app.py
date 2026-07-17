"""pyapi unit tests — DB layer mocked, no MySQL needed.

Run: python3 -m pytest tests/pyapi/ -q
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from fastapi.testclient import TestClient  # noqa: E402

from pyapi import auth, db, push, services  # noqa: E402
from pyapi.main import app  # noqa: E402


@pytest.fixture()
def client():
    return TestClient(app, raise_server_exceptions=False)


@pytest.fixture()
def admin_ok(monkeypatch):
    """Force the admin-session check to pass."""
    monkeypatch.setattr(auth, "_valid_session", lambda s, u, admin: True)


def test_normalize_article():
    assert services.normalize_article(" c-110/j ") == "C110J"
    assert services.normalize_article("dt.068") == "DT068"
    assert services.normalize_article("") == ""
    assert services.normalize_article("!!!") == ""


def test_health(monkeypatch, client):
    monkeypatch.setattr(db, "fetch_one", lambda sql, params=(): {"ok": 1})
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

    monkeypatch.setattr(db, "fetch_all", fake_fetch_all)
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


def test_prices_requires_auth(monkeypatch, client):
    # No admin session and no/invalid key → 401 (session check fails).
    monkeypatch.setattr("pyapi.main.settings.__class__.tech_key", property(lambda self: "sekret"))
    assert client.get("/pyapi/v1/prices").status_code == 401
    assert client.get("/pyapi/v1/prices", params={"key": "wrong"}).status_code == 401


def test_prices_qty_fallback(monkeypatch, client):
    monkeypatch.setattr("pyapi.main.settings.__class__.tech_key", property(lambda self: "sekret"))
    monkeypatch.setattr(auth, "_valid_session", lambda s, u, admin: False)

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

    monkeypatch.setattr(db, "fetch_all", fake_fetch_all)
    r = client.get("/pyapi/v1/prices", params={"key": "sekret"})
    assert r.status_code == 200
    lists = {row["id"]: row["records_count"] for row in r.json()["lists"]}
    assert lists == {3: 1200, 8: 340}
    assert len(calls) == 2  # listing + live-count fallback


def test_laximo_catalogs_filters_junk(monkeypatch, client):
    monkeypatch.setattr(
        db, "fetch_all",
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


# ── Phase 2: CP endpoints + session auth ──

def test_cp_endpoint_requires_auth(client):
    # No cookie, no key → 401 (session) since tech_key check fails first.
    r = client.get("/pyapi/v1/dashboard")
    assert r.status_code == 401


def test_cp_endpoint_accepts_tech_key(monkeypatch, client):
    monkeypatch.setattr("pyapi.main.settings.__class__.tech_key", property(lambda self: "sekret"))
    monkeypatch.setattr(db, "fetch_one", lambda sql, params=(): {"c": 7})
    r = client.get("/pyapi/v1/dashboard", params={"key": "sekret"})
    assert r.status_code == 200
    assert r.json()["kpis"]["price_lists"] == 7


def test_cp_endpoint_accepts_admin_cookie(monkeypatch, admin_ok, client):
    monkeypatch.setattr(db, "fetch_one", lambda sql, params=(): {"c": 3})
    client.cookies.set("admin_session", "abc")
    client.cookies.set("admin_u_id", "18")
    r = client.get("/pyapi/v1/dashboard")
    assert r.status_code == 200
    assert "kpis" in r.json()


def test_brands_public(monkeypatch, client):
    monkeypatch.setattr(
        db, "fetch_all",
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


def test_orders_requires_auth(client):
    assert client.get("/pyapi/v1/orders").status_code == 401


def test_orders_with_key(monkeypatch, client):
    monkeypatch.setattr("pyapi.main.settings.__class__.tech_key", property(lambda self: "sekret"))
    monkeypatch.setattr(
        db, "fetch_all",
        lambda sql, params=(): [{"id": 5, "date": 0, "status_id": 1, "summ": 99.0,
                                 "read": 0, "client_name": "A", "client_phone": "1"}],
    )
    r = client.get("/pyapi/v1/orders", params={"key": "sekret", "limit": 10})
    assert r.status_code == 200
    assert r.json()["orders"][0]["id"] == 5


def test_laximo_status(monkeypatch, client):
    def fake_one(sql, params=()):
        if "epc_laximo_catalogs" in sql:
            return {"cnt": 56, "last_sync": 123}
        return {"c": 1}
    monkeypatch.setattr(db, "fetch_one", fake_one)
    r = client.get("/pyapi/v1/laximo/status")
    assert r.status_code == 200
    body = r.json()
    assert body["catalogs_count"] == 56
    assert body["offline_ready"] is True


# ── Push notifications ──

def test_push_register_requires_admin(client):
    r = client.post("/pyapi/v1/push/register", json={"token": "abc", "platform": "android"})
    assert r.status_code == 401


def test_push_register_ok(monkeypatch, admin_ok, client):
    captured = {}
    monkeypatch.setattr(db, "execute", lambda sql, params=(): captured.setdefault("p", params) or 1)
    client.cookies.set("admin_session", "s")
    client.cookies.set("admin_u_id", "18")
    r = client.post("/pyapi/v1/push/register", json={"token": "tok123", "platform": "ios", "app": "cp"})
    assert r.status_code == 200
    body = r.json()
    assert body["status"] is True
    assert body["platform"] == "ios"
    assert captured["p"][0] == "tok123"


def test_push_send_not_configured(monkeypatch):
    # No FCM env → safe no-op, still reports device count.
    monkeypatch.delenv("PYAPI_FCM_PROJECT", raising=False)
    monkeypatch.delenv("PYAPI_FCM_ACCESS_TOKEN", raising=False)
    monkeypatch.setattr(push, "active_tokens", lambda app=None: [{"token": "a", "platform": "android", "app": "cp"}])
    res = push.send("Hi", "There")
    assert res["status"] is False
    assert res["reason"] == "not_configured"
    assert res["devices"] == 1


def test_push_send_dispatches(monkeypatch):
    monkeypatch.setenv("PYAPI_FCM_PROJECT", "proj")
    monkeypatch.setenv("PYAPI_FCM_ACCESS_TOKEN", "bearer")
    monkeypatch.setattr(
        push, "active_tokens",
        lambda app=None: [
            {"token": "a", "platform": "android", "app": "cp"},
            {"token": "b", "platform": "ios", "app": "cp"},
        ],
    )
    sent = []
    monkeypatch.setattr(push, "_transport", lambda project, bearer, payload: (sent.append(payload) or (True, "ok")))
    res = push.send("New order", "Order #5", app="cp", data={"type": "order"})
    assert res["status"] is True
    assert res["sent"] == 2
    assert sent[0]["message"]["notification"]["title"] == "New order"


def test_notify_new_orders(monkeypatch):
    monkeypatch.setattr(
        db, "fetch_all",
        lambda sql, params=(): [
            {"id": 10, "summ": 50, "client_name": "A"},
            {"id": 12, "summ": 90, "client_name": "B"},
        ],
    )
    monkeypatch.setattr(push, "send", lambda title, body, app=None, data=None: {"status": True, "sent": 1})
    res = push.notify_new_orders(9, app="cp")
    assert res["new_orders"] == 2
    assert res["last_id"] == 12


def test_notify_new_orders_none(monkeypatch):
    monkeypatch.setattr(db, "fetch_all", lambda sql, params=(): [])
    called = {"n": 0}
    monkeypatch.setattr(push, "send", lambda *a, **k: called.__setitem__("n", called["n"] + 1) or {})
    res = push.notify_new_orders(99, app="cp")
    assert res["new_orders"] == 0
    assert res["last_id"] == 99
    assert called["n"] == 0  # no push when nothing new


def test_notify_low_stock(monkeypatch):
    monkeypatch.setattr(db, "fetch_one", lambda sql, params=(): {"c": 4})
    monkeypatch.setattr(push, "send", lambda title, body, app=None, data=None: {"status": True, "sent": 1})
    res = push.notify_low_stock(app="cp")
    assert res["low_stock"] == 4


def test_worker_run_once_baseline(monkeypatch):
    from pyapi import worker
    monkeypatch.setattr(worker, "_max_order_id", lambda: 100)
    monkeypatch.setattr(worker, "_save_state", lambda s: None)
    calls = {"orders": 0, "low": 0}
    monkeypatch.setattr(push, "notify_new_orders", lambda since, app="cp": calls.__setitem__("orders", since) or {"status": True, "last_id": since, "new_orders": 0})
    monkeypatch.setattr(push, "notify_low_stock", lambda app="cp": calls.__setitem__("low", 1) or {"status": True, "low_stock": 0})
    state = {}
    out = worker.run_once(state, low_stock_every=3600)
    # First pass baselines to current max id (no backlog blast).
    assert state["last_order_id"] == 100
    assert calls["orders"] == 100
