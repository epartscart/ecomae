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
    }

    for name, payload in samples.items():
        path = OUT / name
        path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        print(path)


if __name__ == "__main__":
    main()
