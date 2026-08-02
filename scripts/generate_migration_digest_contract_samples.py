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
        "erp-suppliers.json": list_digest("erp", "suppliers"),
        "erp-purchases.json": list_digest("erp", "purchases"),
        "erp-coa-accounts.json": list_digest("erp", "accounts"),
        "erp-warehouses.json": list_digest("erp", "warehouses"),
        "erp-sales-orders.json": list_digest("erp", "orders"),
        "erp-purchase-orders.json": list_digest("erp", "orders"),
        "erp-invoices.json": list_digest("erp", "invoices"),
        "erp-gl-journals.json": list_digest("erp", "journals"),
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
    }

    for name, payload in samples.items():
        path = OUT / name
        path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        print(path)


if __name__ == "__main__":
    main()
