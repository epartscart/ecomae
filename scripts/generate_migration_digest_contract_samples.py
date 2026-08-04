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
            "orders": [
                {
                    "id": 1,
                    "timeUnix": 0,
                    "userId": 9,
                    "status": 0,
                    "paid": 0,
                    "paidType": 0,
                    "officeId": 0,
                    "successfullyCreated": 1,
                    "countItems": 0,
                    "orderSum": 0.0,
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": (
                "migration-mode contract sample; orders[] item-field sentinel locked; "
                "PHP OMS remains authoritative; cutoverAllowed=false"
            ),
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
        "cp-tenants.json": list_digest(
            "cp",
            "tenants",
            [
                {
                    "siteKey": "www",
                    "hostname": "www.ecomae.com",
                    "industryCode": "auto",
                    "status": "active",
                    "tradeName": "EcomAE",
                    "hubName": "hub",
                    "hostedOn": "cloudpanel",
                    "erpOnly": False,
                    "isActive": True,
                    "hasDb": True,
                }
            ],
        ),
        "cp-users.json": list_digest(
            "cp",
            "users",
            [
                {
                    "userId": 1,
                    "email": "migration@example.com",
                    "phone": "",
                    "unlocked": True,
                    "timeRegistered": 0,
                    "timeLastVisit": 0,
                }
            ],
        ),
        "cp-groups.json": list_digest(
            "cp",
            "groups",
            [
                {
                    "id": 1,
                    "value": "Administrators",
                    "forBackend": True,
                    "forGuests": False,
                    "forRegistrated": True,
                    "unblocked": True,
                    "parent": 0,
                    "level": 1,
                }
            ],
        ),
        "cp-modules.json": list_digest(
            "cp",
            "modules",
            [
                {
                    "id": 1,
                    "caption": "Migration module",
                    "activated": True,
                    "isFrontend": False,
                    "isPrototype": False,
                    "controlAvailable": True,
                }
            ],
        ),
        "cp-menus.json": {
            **list_digest(
                "cp",
                "menus",
                [
                    {
                        "id": 1,
                        "caption": "Main menu",
                        "isFrontend": True,
                        "menuUlClass": "nav",
                        "menuUlId": "main-menu",
                        "structurePresent": True,
                        "structureParseOk": True,
                        "nodeCount": 3,
                        "maxDepth": 2,
                        "urlLinkCount": 2,
                        "contentLinkCount": 1,
                        "unknownLinkCount": 0,
                    }
                ],
            ),
            "note": (
                "migration-mode contract sample; structure summary fields locked; "
                "raw structure JSON omitted; PHP remains authoritative"
            ),
        },
        "cp-pages.json": list_digest(
            "cp",
            "pages",
            [
                {
                    "id": 1,
                    "caption": "Home",
                    "url": "/",
                    "alias": "home",
                    "isFrontend": True,
                    "published": True,
                    "level": 1,
                    "sortOrder": 1,
                }
            ],
        ),
        "cp-currencies.json": list_digest(
            "cp",
            "currencies",
            [
                {
                    "id": 1,
                    "isoCode": "AED",
                    "isoName": "UAE Dirham",
                    "captionShort": "AED",
                    "rate": 1.0,
                    "available": True,
                    "sortOrder": 1,
                }
            ],
        ),
        "cp-api-clients.json": list_digest(
            "cp",
            "clients",
            [
                {
                    "id": 1,
                    "clientKeyPrefix": "mig_",
                    "product": "catalog",
                    "label": "Migration client",
                    "contactEmail": "migration@example.com",
                    "active": True,
                    "dailyLimit": 1000,
                    "callsToday": 0,
                    "timeCreated": 0,
                }
            ],
        ),
        "cp-config-items.json": list_digest(
            "cp",
            "items",
            [
                {
                    "name": "site_name",
                    "caption": "Site name",
                    "type": "string",
                    "configGroup": "general",
                    "visible": True,
                    "order": 1,
                }
            ],
        ),
        "cp-admin-sessions.json": list_digest(
            "cp",
            "sessions",
            [{"userId": 1, "email": "migration@example.com", "type": "admin", "sessionCount": 1}],
        ),
        "cp-storages.json": list_digest(
            "cp",
            "storages",
            [{"id": 1, "name": "Main warehouse", "shortName": "MAIN", "hidden": False}],
        ),
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
        "erp-suppliers.json": list_digest(
            "erp",
            "suppliers",
            [{"id": 1, "name": "Migration supplier", "storageId": 1, "balance": 0.0}],
        ),
        "erp-purchases.json": list_digest(
            "erp",
            "purchases",
            [
                {
                    "id": 1,
                    "supplierId": 1,
                    "supplierName": "Migration supplier",
                    "purchaseDate": "1970-01-01",
                    "invoiceNumber": "MIG-1",
                    "totalAmount": 0.0,
                    "status": "draft",
                    "orderId": 0,
                }
            ],
        ),
        "erp-cash-accounts.json": list_digest(
            "erp",
            "accounts",
            [
                {
                    "id": 1,
                    "name": "Cash",
                    "accountType": "cash",
                    "currencyCode": "AED",
                    "openingBalance": 0.0,
                    "balance": 0.0,
                }
            ],
        ),
        "erp-cash-entries.json": list_digest(
            "erp",
            "entries",
            [
                {
                    "id": 1,
                    "accountId": 1,
                    "accountName": "Cash",
                    "accountType": "cash",
                    "timeUnix": 0,
                    "direction": "in",
                    "amount": 0.0,
                    "reference": "migration",
                    "note": "sentinel",
                }
            ],
        ),
        "erp-coa-accounts.json": list_digest(
            "erp",
            "accounts",
            [
                {
                    "id": 1,
                    "code": "1000",
                    "name": "Cash",
                    "accountType": "asset",
                    "normalSide": "debit",
                    "parentId": 0,
                    "openingBalance": 0.0,
                    "active": True,
                }
            ],
        ),
        "erp-warehouses.json": list_digest(
            "erp",
            "warehouses",
            [
                {
                    "id": 1,
                    "storageId": 1,
                    "code": "MAIN",
                    "name": "Main warehouse",
                    "active": True,
                    "timeCreated": 0,
                }
            ],
        ),
        "erp-sales-orders.json": list_digest(
            "erp",
            "orders",
            [
                {
                    "id": 1,
                    "soNo": "SO-1",
                    "customerUserId": 9,
                    "totalAmount": 0.0,
                    "status": "draft",
                    "timeCreated": 0,
                }
            ],
        ),
        "erp-purchase-orders.json": list_digest(
            "erp",
            "orders",
            [
                {
                    "id": 1,
                    "poNo": "PO-1",
                    "supplierId": 1,
                    "title": "Migration PO",
                    "totalAmount": 0.0,
                    "status": "draft",
                    "timeCreated": 0,
                }
            ],
        ),
        "erp-invoices.json": list_digest(
            "erp",
            "invoices",
            [
                {
                    "id": 1,
                    "invoiceNumber": "INV-1",
                    "orderId": 1,
                    "userId": 9,
                    "customerEmail": "migration@example.com",
                    "issueDate": "1970-01-01",
                    "status": "draft",
                    "totalInclVat": 0.0,
                }
            ],
        ),
        "erp-gl-journals.json": list_digest(
            "erp",
            "journals",
            [
                {
                    "id": 1,
                    "journalNo": "J-1",
                    "journalDate": "1970-01-01",
                    "sourceType": "manual",
                    "sourceId": 0,
                    "status": "draft",
                    "totalDebit": 0.0,
                }
            ],
        ),
        "bos-tenants.json": list_digest(
            "bos",
            "tenants",
            [
                {
                    "siteKey": "www",
                    "hostname": "www.ecomae.com",
                    "industryCode": "auto",
                    "status": "active",
                    "tradeName": "EcomAE",
                    "hubName": "hub",
                    "hostedOn": "cloudpanel",
                    "erpOnly": False,
                    "isActive": True,
                    "hasDb": True,
                }
            ],
        ),
        "bos-audit-log.json": list_digest(
            "bos",
            "entries",
            [
                {
                    "id": 1,
                    "ts": 0,
                    "userId": 1,
                    "actor": "migration",
                    "area": "platform",
                    "action": "read",
                    "target": "sentinel",
                    "ip": "127.0.0.1",
                }
            ],
        ),
        "bos-fleet-health.json": {
            "ok": True,
            "surface": "bos",
            "summary": {
                "portalTenants": 1,
                "activePortalTenants": 1,
                "adminSessions": 0,
                "withDatabase": 1,
                "erpOnly": 0,
                "source": "migration",
                "message": "TenantRegistry DB is not configured.",
            },
            "sampleTenants": [
                {
                    "siteKey": "www",
                    "hostname": "www.ecomae.com",
                    "industryCode": "auto",
                    "status": "active",
                    "tradeName": "EcomAE",
                    "hubName": "hub",
                    "hostedOn": "cloudpanel",
                    "erpOnly": False,
                    "isActive": True,
                    "hasDb": True,
                }
            ],
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "session": {"kind": "Admin", "user_id": 1},
            "note": (
                "migration-mode contract sample; sampleTenants[] item-field sentinel locked; "
                "PHP remains authoritative"
            ),
        },
        "storefront-orders.json": {
            "ok": True,
            "surface": "storefront",
            "user_id": 9,
            "orders": [
                {
                    "id": 1,
                    "timeUnix": 0,
                    "paid": False,
                    "successfullyCreated": True,
                    "status": "new",
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "session": {"kind": "Customer", "user_id": 9},
            "note": "migration-mode contract sample; item-field sentinel; PHP remains authoritative",
        },
        "storefront-garage.json": {
            "ok": True,
            "surface": "storefront",
            "user_id": 9,
            "vehicles": [
                {
                    "id": 1,
                    "caption": "Migration vehicle",
                    "marka": "Demo",
                    "model": "Car",
                    "year": 2020,
                    "vin": "MIGRATIONVIN000001",
                    "active": True,
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "session": {"kind": "Customer", "user_id": 9},
            "note": "migration-mode contract sample; item-field sentinel; PHP remains authoritative",
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
        "storefront-search.json": {
            "ok": True,
            "surface": "storefront",
            "article": "0986424590",
            "rows": [
                {
                    "priceId": 1,
                    "priceList": "migration",
                    "manufacturer": "BOSCH",
                    "article": "0986424590",
                    "articleShow": "0 986 424 590",
                    "name": "Migration offer",
                    "price": 0.0,
                    "exist": 0,
                    "storage": "MAIN",
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "session": {"kind": "Customer", "user_id": 9},
            "note": (
                "migration-mode contract sample; rows[] item-field sentinel locked; "
                "PHP /shop/part_search remains authoritative; cutoverAllowed=false"
            ),
        },
        "storefront-cart.json": {
            "ok": True,
            "surface": "storefront",
            "user_id": 9,
            "summary": {
                "count": 1,
                "sum": 0.0,
                "source": "migration",
                "message": "TenantRegistry DB is not configured.",
            },
            "lines": [
                {
                    "id": 1,
                    "price": 0.0,
                    "countNeed": 1.0,
                    "checkedForOrder": False,
                    "productType": 1,
                    "manufacturer": "BOSCH",
                    "article": "0986424590",
                    "name": "Migration cart line",
                    "timeToExe": "0",
                    "timeToExeGuaranteed": "0",
                    "minOrder": 1.0,
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "session": {"kind": "Customer", "user_id": 9},
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": (
                "migration-mode contract sample; lines[] item-field sentinel locked; "
                "qty/guest/checkout remain PHP; cutoverAllowed=false"
            ),
        },
        "api-catalog-status.json": catalog_status(),
        "api-catalog-manufacturers.json": {
            "ok": True,
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": [
                {
                    "MFA_ID": 1,
                    "manufacturer": "Migration Make",
                    "manufacturer_ru": "Migration Make",
                    "type": "passenger",
                    "country": "AE",
                    "popular": 0,
                    "is_logo": 0,
                }
            ],
            "message": "migration-mode manufacturers item-field sentinel; PHP/cache remain authoritative",
        },
        "api-catalog-models.json": {
            "ok": True,
            "action": "models",
            "section": "passenger",
            "mfa_id": 1,
            "ms_id": None,
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": [
                {
                    "MFA_ID": 1,
                    "MS_ID": 10,
                    "model_series": "Migration Series",
                    "year_from": 2020,
                    "year_to": 2024,
                }
            ],
            "message": "migration-mode models item-field sentinel; PHP/cache remain authoritative",
        },
        "api-catalog-modifications.json": {
            "ok": True,
            "action": "modifications",
            "section": "passenger",
            "mfa_id": None,
            "ms_id": 1,
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": [
                {
                    "MS_ID": 10,
                    "modification_id": 100,
                    "title": "Migration Mod",
                    "year_from": 2020,
                    "year_to": 2024,
                    "power_kw": 100,
                    "capacity_lt": 2.0,
                    "fuel_type": "petrol",
                }
            ],
            "message": "migration-mode modifications item-field sentinel; PHP/cache remain authoritative",
        },
        "api-catalog-brands.json": {
            "ok": True,
            "action": "brands",
            "section": "all",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": [{"sup_id": 1, "brand": "BOSCH", "full_name": "Robert Bosch GmbH"}],
            "message": "migration-mode brands item-field sentinel; PHP/cache remain authoritative",
        },
        "api-catalog-suppliers.json": {
            "ok": True,
            "action": "suppliers",
            "section": "all",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": [{"sup_id": 1, "brand": "BOSCH", "full_name": "Robert Bosch GmbH"}],
            "message": "migration-mode suppliers item-field sentinel; PHP/cache remain authoritative",
        },
        "api-catalog-engines.json": {
            "ok": True,
            "action": "engines",
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "data": [
                    {
                        "ENG_ID": 1,
                        "ENGINE_CODE": "N47D20",
                        "POWER_KW": 0,
                        "CAPACITY_LT": 0.0,
                        "FUEL_TYPE": "Diesel",
                    }
                ]
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": (
                "migration-mode engines nested data.data[] item-field sentinel; "
                "OfflineCacheOk keeps object blob; PHP/UMAPI remain authoritative"
            ),
        },
        "api-catalog-analogs.json": {
            "ok": True,
            "action": "analogs",
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "data": [
                    {
                        "ART_ID": 1,
                        "BRAND": "BOSCH",
                        "ARTICLE_NR": "0986424590",
                        "TITLE": "Migration analog",
                    }
                ]
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": (
                "migration-mode analogs nested data.data[] item-field sentinel; "
                "OfflineCacheOk keeps object blob; PHP/UMAPI remain authoritative"
            ),
        },
        "api-catalog-article-brands.json": {
            "ok": True,
            "action": "brands",
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "rows": 1,
                "data": [
                    {
                        "BRAND": "BOSCH",
                        "SUP_BRAND": "BOSCH",
                        "MANUFACTURER": "BOSCH",
                        "DISPLAY_NR": "0986424590",
                        "SEARCH_NUMBER": "0986424590",
                        "ARTICLE": "0986424590",
                        "TITLE": "Migration brand refinement",
                        "DES": "Migration brand refinement",
                    }
                ],
                "source": "migration",
                "stale": True,
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": (
                "migration-mode article-brands nested data.data[] item-field sentinel "
                "(PHP epc_umapi_brands_offline_payload shape); OfflineCacheOk keeps object blob"
            ),
        },
        "api-catalog-categories.json": {
            "ok": True,
            "action": "categories",
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "data": [
                    {
                        "STR_ID": 1,
                        "CATEGORY_NAME": "Migration category",
                        "ORDER": 1,
                    }
                ]
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": (
                "migration-mode categories nested data.data[] item-field sentinel; "
                "OfflineCacheOk keeps object blob; PHP/UMAPI remain authoritative"
            ),
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
            "rows": 1,
            "source": "migration",
            "data": [
                {
                    "manufacturer": "BOSCH",
                    "article_show": "0 986 479 001",
                    "article": "0986479001",
                    "name": "Migration part",
                    "exist": 1,
                    "price": 0.0,
                    "time_to_exe": 0,
                    "storage": "MAIN",
                }
            ],
            "message": "migration-mode brand-parts item-field sentinel; PHP/cache remain authoritative",
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
