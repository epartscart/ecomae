"""pyapi unit tests — DB layer mocked, no MySQL needed.

Run: python3 -m pytest tests/pyapi/ -q
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from fastapi.testclient import TestClient  # noqa: E402

from pyapi import db, services  # noqa: E402
from pyapi.main import app  # noqa: E402


@pytest.fixture()
def client():
    return TestClient(app, raise_server_exceptions=False)


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


def test_prices_requires_key(monkeypatch, client):
    monkeypatch.setattr("pyapi.main.settings.__class__.tech_key", property(lambda self: "sekret"))
    r = client.get("/pyapi/v1/prices")
    assert r.status_code == 403
    r2 = client.get("/pyapi/v1/prices", params={"key": "wrong"})
    assert r2.status_code == 403


def test_prices_qty_fallback(monkeypatch, client):
    monkeypatch.setattr("pyapi.main.settings.__class__.tech_key", property(lambda self: "sekret"))

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
