#!/usr/bin/env python3
"""Generate migration-mode digest envelopes that satisfy field contracts (no DB/secrets)."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs/migration/evidence/surface-parity/samples/migration"


def summary(surface: str, fields: dict) -> dict:
    return {
        "ok": True,
        "surface": surface,
        "summary": {**fields, "source": "migration", "message": "TenantRegistry DB is not configured."},
        "session": {"kind": "Admin", "user_id": 1},
        "note": "migration-mode contract sample; PHP remains authoritative",
    }


def readiness(fields: dict) -> dict:
    return {
        "ok": True,
        "surface": "bos",
        "readiness": {**fields, "source": "migration", "message": "TenantRegistry DB is not configured."},
        "session": {"kind": "Admin", "user_id": 1},
        "note": "migration-mode contract sample; PHP remains authoritative",
    }


def list_digest(surface: str, key: str, items: list | None = None) -> dict:
    rows = items or []
    return {
        "ok": True,
        "surface": surface,
        key: rows,
        "count": len(rows),
        "source": "migration",
        "message": "TenantRegistry DB is not configured.",
        "session": {"kind": "Admin", "user_id": 1},
        "note": "migration-mode contract sample; PHP remains authoritative",
    }


def catalog_status() -> dict:
    return {
        "connected": False,
        "message": "migration placeholder",
        "last_checked": None,
        "last_success": None,
        "last_error": None,
        "status_code": 0,
        "counts": {
            "manufacturers": 0,
            "models": 0,
            "modifications": 0,
            "brands": 0,
            "vins": 0,
        },
        "sections": [],
        "cache_rows": 0,
        "offline_ready": False,
        "action_required": "Configure TenantRegistry MySQL",
        "source": "migration",
        "note": "migration-mode contract sample; PHP remains authoritative",
    }


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    samples = {
        "cp-dashboard-summary.json": summary(
            "cp",
            {"users": 0, "adminSessions": 0, "portalTenants": 0, "activePortalTenants": 0},
        ),
        "cp-orders-digest.json": {
            **summary("cp", {"open": 0, "today": 0, "pendingShip": 0}),
            "orders": [],
            "count": 0,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": "migration-mode contract sample; PHP OMS remains authoritative; cutoverAllowed=false",
        },
        "erp-dashboard-summary.json": summary(
            "erp",
            {
                "cashPosition": 0,
                "supplierCredit": 0,
                "supplierDebit": 0,
                "supplierNet": 0,
                "cashAccounts": 0,
                "activeSuppliers": 0,
                "activePurchases": 0,
            },
        ),
        "bos-fleet-summary.json": summary(
            "bos",
            {
                "portalTenants": 0,
                "activePortalTenants": 0,
                "adminSessions": 0,
                "withDatabase": 0,
                "erpOnly": 0,
            },
        ),
        "storefront-account-summary.json": summary(
            "storefront",
            {"userId": 9, "orders": 0, "sessions": 0, "garageVehicles": 0},
        ),
        "erp-inventory-stock.json": summary(
            "erp",
            {
                "rowCount": 0,
                "qtyOnHand": 0,
                "stockValue": 0,
                "warehouseCount": 0,
                "itemCount": 0,
            },
        ),
        "bos-fleet-readiness.json": readiness(
            {
                "tenants": 0,
                "pass": 0,
                "warn": 0,
                "fail": 0,
                "active": 0,
                "withDatabase": 0,
                "erpOnly": 0,
            }
        ),
        "cp-tenants.json": list_digest("cp", "tenants"),
        "cp-users.json": list_digest("cp", "users"),
        "cp-groups.json": list_digest("cp", "groups"),
        "cp-modules.json": list_digest("cp", "modules"),
        "cp-menus.json": list_digest("cp", "menus"),
        "cp-pages.json": list_digest("cp", "pages"),
        "cp-currencies.json": list_digest("cp", "currencies"),
        "cp-api-clients.json": list_digest("cp", "clients"),
        "cp-config-items.json": list_digest("cp", "items"),
        "cp-admin-sessions.json": list_digest("cp", "sessions"),
        "cp-storages.json": list_digest("cp", "storages"),
        "erp-accounts-summary.json": summary(
            "erp",
            {
                "cashPosition": 0,
                "supplierCredit": 0,
                "supplierDebit": 0,
                "supplierNet": 0,
                "cashAccounts": 0,
                "activeSuppliers": 0,
                "activePurchases": 0,
            },
        )
        | {"source": "migration", "message": "TenantRegistry DB is not configured."},
        "erp-suppliers.json": list_digest("erp", "suppliers"),
        "erp-purchases.json": list_digest("erp", "purchases"),
        "erp-cash-accounts.json": list_digest("erp", "accounts"),
        "erp-cash-entries.json": list_digest("erp", "entries"),
        "erp-coa-accounts.json": list_digest("erp", "accounts"),
        "erp-warehouses.json": list_digest("erp", "warehouses"),
        "erp-sales-orders.json": list_digest("erp", "orders"),
        "erp-purchase-orders.json": list_digest("erp", "orders"),
        "erp-invoices.json": list_digest("erp", "invoices"),
        "erp-gl-journals.json": list_digest("erp", "journals"),
        "bos-tenants.json": list_digest("bos", "tenants"),
        "bos-audit-log.json": list_digest("bos", "entries"),
        "bos-fleet-health.json": {
            "ok": True,
            "surface": "bos",
            "summary": {
                "portalTenants": 0,
                "activePortalTenants": 0,
                "adminSessions": 0,
                "withDatabase": 0,
                "erpOnly": 0,
                "source": "migration",
                "message": "TenantRegistry DB is not configured.",
            },
            "sampleTenants": [],
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "session": {"kind": "Admin", "user_id": 1},
            "note": "migration-mode contract sample; PHP remains authoritative",
        },
        "storefront-orders.json": {
            "ok": True,
            "surface": "storefront",
            "user_id": 9,
            "orders": [],
            "count": 0,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "session": {"kind": "Customer", "user_id": 9},
            "note": "migration-mode contract sample; PHP remains authoritative",
        },
        "storefront-garage.json": {
            "ok": True,
            "surface": "storefront",
            "user_id": 9,
            "vehicles": [],
            "count": 0,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "session": {"kind": "Customer", "user_id": 9},
            "note": "migration-mode contract sample; PHP remains authoritative",
        },
        "storefront-profile.json": {
            "ok": True,
            "surface": "storefront",
            "user_id": 9,
            "email": "migration@example.com",
            "email_confirmed": False,
            "phone": "",
            "phone_confirmed": False,
            "reg_variant": "email",
            "profile_fields": {},
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "session": {"kind": "Customer", "user_id": 9},
            "note": "migration-mode contract sample; PHP remains authoritative",
        },
        "api-catalog-status.json": catalog_status(),
        "api-catalog-manufacturers.json": {
            "ok": True,
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": [],
            "message": "TenantRegistry DB is not configured.",
        },
        "api-catalog-models.json": {
            "ok": True,
            "action": "models",
            "section": "passenger",
            "mfa_id": 1,
            "ms_id": None,
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": [],
            "message": "TenantRegistry DB is not configured.",
        },
        "api-catalog-modifications.json": {
            "ok": True,
            "action": "modifications",
            "section": "passenger",
            "mfa_id": None,
            "ms_id": 1,
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": [],
            "message": "TenantRegistry DB is not configured.",
        },
        "api-catalog-brands.json": {
            "ok": True,
            "action": "brands",
            "section": "all",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": [],
            "message": "TenantRegistry DB is not configured.",
        },
        "api-catalog-suppliers.json": {
            "ok": True,
            "action": "suppliers",
            "section": "all",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": [],
            "message": "TenantRegistry DB is not configured.",
        },
        "api-catalog-engines.json": {
            "ok": True,
            "action": "engines",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-analogs.json": {
            "ok": True,
            "action": "analogs",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-article-brands.json": {
            "ok": True,
            "action": "brands",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-categories.json": {
            "ok": True,
            "action": "categories",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-products.json": {
            "ok": True,
            "action": "products",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-engine-search.json": {
            "ok": True,
            "action": "engine_search",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-article-links.json": {
            "ok": True,
            "action": "article_links",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-article.json": {
            "ok": True,
            "action": "article",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-articles.json": {
            "ok": True,
            "action": "articles",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-engine.json": {
            "ok": True,
            "action": "engine",
            "section": "passenger",
            "rows": 0,
            "source": "migration",
            "stale": True,
            "data": {},
        },
        "api-catalog-vin.json": {
            "ok": True,
            "source": "migration",
            "stale": True,
            "cached_at": 0,
            "vin": "WBAXG1103CDW29096",
            "language": "en",
            "region": "WWW",
            "vehicle_count": 0,
            "manufacturer": None,
            "model_label": None,
            "payload": {},
        },
        "api-catalog-brand-parts.json": {
            "ok": True,
            "brand": "BOSCH",
            "rows": 0,
            "source": "migration",
            "data": [],
            "message": "TenantRegistry DB is not configured.",
        },
    }

    for name, payload in samples.items():
        if not isinstance(payload, dict):
            raise SystemExit(f"{name}: payload must be object")
        # Every golden must explicitly refuse cutover/PHP removal.
        payload.setdefault("dualSampleBaseline", "migration-contract-golden")
        payload["cutoverAllowed"] = False
        payload["readyForPhpRemoval"] = False
        path = OUT / name
        path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        print(path)
    print(f"generated={len(samples)} cutoverAllowed=false readyForPhpRemoval=false")


if __name__ == "__main__":
    main()
