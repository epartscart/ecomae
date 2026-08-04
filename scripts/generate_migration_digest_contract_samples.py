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
        "cp-power-bi.json": {
            **summary(
                "cp",
                {
                    "siteKey": "__platform__",
                    "workspaceId": "ws-migration",
                    "azureTenantId": "tenant-migration",
                    "defaultReportId": "rpt-1",
                    "defaultDatasetId": "ds-1",
                    "embedUrl": "https://app.powerbi.com/reportEmbed?reportId=rpt-1",
                    "embedMode": "report",
                    "notes": "migration golden",
                    "active": True,
                    "reportCount": 1,
                },
            ),
            "reports": [
                {
                    "id": 1,
                    "siteKey": "__platform__",
                    "reportId": "rpt-1",
                    "reportName": "Migration Finance",
                    "datasetId": "ds-1",
                    "category": "finance",
                    "embedUrl": "https://app.powerbi.com/reportEmbed?reportId=rpt-1",
                    "active": True,
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": (
                "migration-mode contract sample; reports[] item-field sentinel locked; "
                "PHP epc_power_bi remains authoritative; cutoverAllowed=false"
            ),
        },
        "cp-mobile-apps.json": summary(
            "cp",
            {
                "enabled": True,
                "appName": "eParts Cart",
                "bundleId": "com.epartscart.app",
                "deepLinkScheme": "epartscart://",
                "deepLinkDomain": "epartscart.com",
                "apiBaseUrl": "https://www.ecomae.com",
                "playStoreUrl": "https://play.google.com/store/apps/details?id=com.epartscart.app",
                "appStoreUrl": "https://apps.apple.com/app/id000000000",
                "pwaEnabled": True,
                "firebaseProjectId": "epartscart-migration",
                "pushEnabled": False,
            },
        ),
        "cp-metabase.json": {
            **summary(
                "cp",
                {
                    "siteKey": "__platform__",
                    "metabaseUrl": "https://metabase.example.com",
                    "active": True,
                    "dashboardCount": 1,
                },
            ),
            "dashboards": [
                {
                    "id": 1,
                    "siteKey": "__platform__",
                    "dashboardId": 10,
                    "dashboardName": "Migration Ops",
                    "category": "ops",
                    "active": True,
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": (
                "migration-mode contract sample; dashboards[] item-field sentinel locked; "
                "PHP epc_metabase_embed remains authoritative; cutoverAllowed=false"
            ),
        },
        "cp-nl-reporting.json": list_digest(
            "cp",
            "definitions",
            [
                {
                    "id": 1,
                    "siteKey": "__platform__",
                    "name": "Migration daily sales",
                    "description": "migration golden",
                    "reportType": "sales",
                    "schedule": "daily",
                    "format": "csv",
                    "active": True,
                    "createdBy": 1,
                }
            ],
        ),
        "cp-marketing-broadcast.json": {
            **summary(
                "cp",
                {
                    "campaigns": 1,
                    "emailsSent": 10,
                    "whatsappSent": 2,
                },
            ),
            "campaigns": [
                {
                    "id": 1,
                    "createdAt": 0,
                    "channel": "email",
                    "templateKey": "welcome",
                    "subject": "Migration campaign",
                    "preview": "Hello",
                    "audienceMode": "all",
                    "audienceMeta": "",
                    "totalTargets": 10,
                    "sentOk": 10,
                    "sentFail": 0,
                    "status": "sent",
                    "operatorId": 1,
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": (
                "migration-mode contract sample; campaigns[] item-field sentinel locked; "
                "PHP epc_marketing_broadcast remains authoritative; cutoverAllowed=false"
            ),
        },
        "cp-demo-tenants.json": list_digest(
            "cp",
            "tenants",
            [
                {
                    "siteKey": "demo-auto",
                    "hostname": "demo.example.com",
                    "industryCode": "auto",
                    "status": "active",
                    "tradeName": "Demo Auto Parts",
                    "hubName": "demo-hub",
                    "hostedOn": "www.ecomae.com",
                    "erpOnly": False,
                    "isActive": True,
                    "demoExpiresAt": 0,
                    "demoContactEmail": "demo@example.com",
                }
            ],
        ),

        "cp-parts-agent-chats.json": {
            **summary(
                "cp",
                {
                    "totalSessions": 2,
                    "sessionsToday": 1,
                    "messagesToday": 3,
                    "loggedInSessions": 1,
                    "guestSessions": 1,
                    "enabled": True,
                    "agentName": "Parts Expert",
                    "domain": "auto",
                },
            ),
            "sessions": [
                {
                    "sessionId": "sess-migration-1",
                    "updatedAt": 0,
                    "messageCount": 2,
                    "countryCode": "AE",
                    "countryName": "United Arab Emirates",
                    "userId": 1,
                    "ipHash": "abc",
                    "lastUserText": "brake pads",
                    "lastAgentText": "Here are options",
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": "migration-mode contract sample; sessions[] sentinel; PHP parts_agent remains authoritative; cutoverAllowed=false",
        },
        "cp-pos-overview.json": {
            **summary(
                "cp",
                {
                    "posEnabled": True,
                    "registerName": "Register 1",
                    "openSessions": 1,
                    "salesToday": 1,
                    "salesTotalToday": 10.5,
                },
            ),
            "sales": [
                {
                    "id": 1,
                    "saleNo": "POS-1",
                    "sessionId": 1,
                    "customerLabel": "Walk-in guest",
                    "subtotalEx": 10.0,
                    "vatAmount": 0.5,
                    "totalAmount": 10.5,
                    "paymentMethod": "cash",
                    "taxKitCode": "ae_vat",
                    "status": "completed",
                    "timeCreated": 0,
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": "migration-mode contract sample; sales[] sentinel; PHP POS remains authoritative; cutoverAllowed=false",
        },
        "cp-tax-toolkits.json": {
            **summary(
                "cp",
                {
                    "toolkitCount": 1,
                    "installCount": 1,
                    "tenantCountry": "AE",
                    "tenantKitCode": "ae_vat",
                },
            ),
            "toolkits": [
                {
                    "id": 1,
                    "kitCode": "ae_vat",
                    "name": "UAE VAT",
                    "jurisdiction": "AE",
                    "taxType": "vat",
                    "isSystem": True,
                    "active": True,
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": "migration-mode contract sample; toolkits[] sentinel; PHP tax toolkit remains authoritative; cutoverAllowed=false",
        },
        "cp-sms-whatsapp.json": {
            **summary(
                "cp",
                {
                    "smsOperators": 1,
                    "activeOperator": "sms_ru",
                    "whatsappSent": 1,
                    "whatsappFailed": 0,
                },
            ),
            "operators": [
                {
                    "id": 1,
                    "name": "sms.ru",
                    "handler": "sms_ru",
                    "description": "migration golden",
                    "active": True,
                    "controlAvailable": True,
                }
            ],
            "whatsappLog": [
                {
                    "id": 1,
                    "createdAt": 0,
                    "notifyName": "order_status",
                    "phoneMasked": "****1234",
                    "status": 1,
                    "messagePreview": "Order update",
                }
            ],
            "count": 1,
            "source": "migration",
            "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "note": "migration-mode contract sample; operators[] sentinel; PHP sms/whatsapp remains authoritative; cutoverAllowed=false",
        },

        "cp-crm-board.json": {
            **summary("cp", {"leads": 1, "opportunities": 0, "activities": 0, "ticketsOpen": 0}),
            "leads": [{"id": 1, "title": "Migration Lead", "status": "new", "source": "web", "ownerId": 1, "amount": 100.0, "updatedAt": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; leads[] sentinel; PHP crm_main authoritative; cutoverAllowed=false",
        },
        "cp-document-control.json": {
            **summary("cp", {"companyName": "Migration Co", "templateCount": 1, "attachmentCount": 0}),
            "templates": [{"id": 1, "code": "tax_invoice", "title": "Tax Invoice", "category": "sales", "active": True, "sortOrder": 1}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; templates[] sentinel; PHP document_control authoritative; cutoverAllowed=false",
        },
        "cp-delivery-methods.json": {
            **summary("cp", {"methods": 1, "available": 1}),
            "modes": [{"id": 1, "caption": "Courier", "handler": "epc_carriers", "available": True, "controlAvailable": True, "sortOrder": 1}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; modes[] sentinel; PHP obtaining modes authoritative; cutoverAllowed=false",
        },
        "cp-crosses.json": {
            **summary("cp", {"totalPairs": 1, "brands": 1}),
            "pairs": [{"id": 1, "manufacturer": "BOSCH", "article": "0986424590", "crossManufacturer": "OEM", "crossArticle": "123"}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; pairs[] sentinel; PHP crosses authoritative; cutoverAllowed=false",
        },
        "cp-hr-overview.json": {
            **summary("cp", {"activeEmployees": 1, "pendingLeave": 0, "payrollRuns": 0, "attendanceRows": 0}),
            "employees": [{"id": 1, "code": "E001", "name": "Migration Employee", "department": "Ops", "status": "active", "joinDate": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; employees[] sentinel; salary/PII omitted; PHP people/HR authoritative; cutoverAllowed=false",
        },
        "cp-production-overview.json": {
            **summary("cp", {"bomCount": 1, "openWorkOrders": 1, "completedWorkOrders": 0}),
            "workOrders": [{"id": 1, "woNo": "WO-1", "status": "planned", "qtyPlanned": 10.0, "qtyProduced": 0.0, "updatedAt": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; workOrders[] sentinel; cost columns omitted; PHP production authoritative; cutoverAllowed=false",
        },
        "cp-projects-overview.json": {
            **summary("cp", {"openProjects": 1, "taskCount": 0, "contractCount": 0}),
            "projects": [{"id": 1, "code": "PRJ-1", "name": "Migration Project", "status": "open", "billingType": "fixed", "contractValue": 1000.0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; projects[] sentinel; timesheet rates omitted; PHP projects authoritative; cutoverAllowed=false",
        },
        "cp-industry-packs.json": {
            **summary("cp", {"packCount": 1, "activePacks": 1, "assignments": 0}),
            "packs": [{"id": 1, "packKey": "auto_parts", "name": "Auto Parts", "description": "Migration pack", "icon": "car", "active": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; packs[] sentinel; JSON blobs omitted; PHP industry_settings authoritative; cutoverAllowed=false",
        },
        "cp-jewellery-retail.json": {
            **summary("cp", {"voucherCount": 1, "openVouchers": 1, "tagCount": 0, "metalStockRows": 0}),
            "vouchers": [{"id": 1, "vocType": "sales", "vocDate": "2026-01-01", "vocNo": 1, "partyName": "Migration Party", "status": "draft", "netAmount": 100.0, "vatAmount": 5.0, "totalWithVat": 105.0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; vouchers[] sentinel; PII/cost omitted; PHP retail/jewellery authoritative; cutoverAllowed=false",
        },
        "cp-price-lists.json": {
            **summary("cp", {"activeLists": 1, "priceRows": 0, "uploadCount": 0}),
            "lists": [{"id": 1, "code": "MAIN", "name": "Migration List", "currency": "AED", "customerId": 0, "priority": 1, "active": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; lists[] sentinel; stats_json omitted; PHP /CP/shop/prices authoritative; cutoverAllowed=false",
        },
        "cp-auto-price.json": {
            **summary("cp", {"activeRules": 1, "activeSources": 0, "compareRuns": 0}),
            "rules": [{"id": 1, "siteKey": "www", "ruleKey": "default", "minMarginPercent": 15.0, "autoUpdatePrices": False, "scheduleHours": 24, "active": True, "updatedAt": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; rules[] sentinel; config_json/notes omitted; PHP auto-price authoritative; cutoverAllowed=false",
        },
        "cp-uae-tax-compliance.json": {
            **summary("cp", {"legislationCount": 1, "vatAdvanceRows": 0, "vatRefundRows": 0}),
            "items": [{"id": 1, "slug": "migration-item", "title": "Migration legislation", "issueDate": "2026-01-01", "category": "vat", "taxCategory": "standard", "isNew": True, "isUpdated": False, "timeSynced": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; items[] sentinel; erp_summary/pdf omitted; PHP uae-tax-compliance authoritative; cutoverAllowed=false",
        },
        "cp-budgets.json": {
            **summary("cp", {"budgetCount": 1, "activeBudgets": 1, "budgetLineCount": 0, "dimensionCount": 0}),
            "budgets": [{"id": 1, "code": "FY26", "name": "Migration Budget", "fiscalYear": "2026", "businessUnitId": 0, "isMaster": True, "active": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; budgets[] sentinel; note omitted; PHP budgeting authoritative; cutoverAllowed=false",
        },
        "cp-carriers.json": {
            **summary("cp", {"carrierCount": 1, "activeCarriers": 1, "rateCount": 0, "openShipments": 0}),
            "carriers": [{"id": 1, "code": "DHL", "name": "Migration Carrier", "mode": "courier", "currency": "AED", "rating": 0.0, "active": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; carriers[] sentinel; contact PII omitted; PHP carriers authoritative; cutoverAllowed=false",
        },
        "cp-payment-gateways.json": {
            **summary("cp", {"gatewayCount": 1, "activeGateways": 1, "selectableGateways": 1, "accountCount": 0}),
            "gateways": [{"id": 1, "name": "Migration Gateway", "handler": "cash", "active": True, "isSelectable": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; gateways[] sentinel; parameters/credentials omitted; PHP payments authoritative; cutoverAllowed=false",
        },
        "cp-workflows.json": {
            **summary("cp", {"workflowCount": 1, "activeWorkflows": 1, "runCount": 0, "failedRuns": 0}),
            "workflows": [{"id": 1, "siteKey": "www", "name": "Migration Workflow", "triggerType": "manual", "active": True, "version": 1, "runCount": 0, "lastRunStatus": ""}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; workflows[] sentinel; trigger_config omitted; PHP workflow automation authoritative; cutoverAllowed=false",
        },
        "cp-purchase-requests.json": {
            **summary("cp", {"reqCount": 1, "draftCount": 1, "pendingApproval": 0, "lineCount": 0, "categoryCount": 0}),
            "requests": [{"id": 1, "companyId": 0, "reqNumber": "PR-1", "requester": "Migration", "businessUnitId": 0, "status": "draft", "total": 0.0, "requiresApproval": False, "poRef": "", "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; requests[] sentinel; justification omitted; PHP purchase requisitions authoritative; cutoverAllowed=false",
        },
        "cp-promotions.json": {
            **summary("cp", {"promotionCount": 1, "activePromotions": 1, "percentPromotions": 1, "loyaltyAccounts": 0}),
            "promotions": [{"id": 1, "code": "MIG10", "name": "Migration Promo", "type": "percent", "value": 10.0, "minSpend": 0.0, "validFrom": 0, "validTo": 0, "active": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; promotions[] sentinel; PHP epc_promotions_engine authoritative; cutoverAllowed=false",
        },
        "cp-crm-opportunities.json": {
            **summary("cp", {"opportunityCount": 1, "openOpportunities": 1, "wonOpportunities": 0, "pipelineAmount": 1000.0}),
            "opportunities": [{"id": 1, "title": "Migration Opportunity", "stage": "prospect", "amount": 1000.0, "probability": 10, "closeDate": 0, "ownerUserId": 0, "leadId": 0, "active": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; opportunities[] sentinel; notes omitted; PHP CRM opportunities authoritative; cutoverAllowed=false",
        },
        "cp-integrations.json": {
            **summary("cp", {"webhookCount": 1, "activeWebhooks": 1, "deliveryCount": 0, "failedDeliveries": 0}),
            "integrations": [{"id": 1, "tenantKey": "__platform__", "url": "https://example.com/hook", "active": True, "description": "Migration webhook", "createdAt": "2026-01-01T00:00:00"}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; integrations[] sentinel; secrets/events omitted; PHP integrations hub authoritative; cutoverAllowed=false",
        },
        "erp-bank-reconciliation.json": {
            **summary("erp", {"lineCount": 1, "unmatchedCount": 1, "matchedCount": 0, "creditTotal": 100.0, "debitTotal": 0.0}),
            "lines": [{"id": 1, "accountId": 1, "lineDate": 0, "description": "Migration line", "reference": "MIG-1", "amount": 100.0, "direction": 1, "matchedEntryId": 0, "importBatch": "MIG", "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; lines[] sentinel; PHP bank_recon authoritative; cutoverAllowed=false",
        },
        "erp-stock-transfers.json": {
            **summary("erp", {"transferCount": 1, "draftCount": 1, "inTransitCount": 0, "receivedCount": 0, "totalQty": 1.0}),
            "transfers": [{"id": 1, "companyId": 0, "transferNo": "TRF-1", "fromWarehouseId": 1, "toWarehouseId": 2, "reason": "rebalance", "status": "draft", "totalItems": 1, "totalQty": 1.0, "shippedAt": "", "receivedAt": "", "createdBy": 0, "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; transfers[] sentinel; notes omitted; PHP warehouse transfers authoritative; cutoverAllowed=false",
        },
        "erp-sales-quotations.json": {
            **summary("erp", {"quoteCount": 1, "draftCount": 1, "sentCount": 0, "acceptedCount": 0, "subtotalSum": 0.0}),
            "quotations": [{"id": 1, "opportunityId": 0, "leadId": 0, "customerUserId": 0, "quoteNumber": "Q-1", "status": "draft", "currencyCode": "AED", "subtotal": 0.0, "shopOrderId": 0, "timeCreated": 0, "active": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; quotations[] sentinel; notes omitted; PHP sales proposals authoritative; cutoverAllowed=false",
        },
        "erp-workspace-favorites.json": {
            **summary("erp", {"shortcutCount": 1, "pinnedCount": 1, "userCount": 1, "erpSurfaceCount": 1}),
            "favorites": [{"id": 1, "companyId": 0, "userId": 1, "surface": "erp", "shortcutKey": "dashboard", "label": "Dashboard", "iconClass": "fa fa-star", "targetUrl": "/ERP/?epc_erp_shell=1&area=overview&tab=dashboard", "targetTab": "dashboard", "sortOrder": 0, "isPinned": True, "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; favorites[] sentinel; PHP dashboard shortcuts authoritative; cutoverAllowed=false",
        },
        "erp-fixed-assets.json": {
            **summary("erp", {"assetCount": 1, "activeCount": 1, "disposedCount": 0, "costTotal": 1000.0, "bookValueTotal": 800.0}),
            "assets": [{"id": 1, "assetCode": "FA-1", "name": "Migration asset", "categoryId": 1, "acquisitionDate": "2024-01-01", "cost": 1000.0, "salvageValue": 0.0, "usefulLifeMonths": 60, "depreciationMethod": "straight_line", "accumulatedDepreciation": 200.0, "bookValue": 800.0, "location": "HQ", "status": "active", "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; assets[] sentinel; note omitted; PHP fixed_assets authoritative; cutoverAllowed=false",
        },
        "cp-page-builder.json": {
            **summary("cp", {"layoutCount": 1, "publishedCount": 0, "draftCount": 1, "siteCount": 1}),
            "layouts": [{"id": 1, "siteKey": "platform", "pageKey": "homepage", "isPublished": False, "updatedAt": 0, "publishedAt": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; layouts[] sentinel; layout_json/brand_json omitted; PHP visual page editor authoritative; cutoverAllowed=false",
        },
        "cp-product-catalogue.json": {
            **summary("cp", {"productCount": 1, "publishedCount": 1, "unpublishedCount": 0, "categoryCount": 1}),
            "products": [{"id": 1, "categoryId": 1, "caption": "Migration product", "alias": "mig-1", "publishedFlag": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; products[] sentinel; PHP catalogue editor authoritative; cutoverAllowed=false",
        },
        "cp-platform-governance.json": {
            **summary("cp", {"ruleCount": 1, "activeCount": 1, "requiredCount": 1, "categoryCount": 1}),
            "rules": [{"id": 1, "ruleKey": "mig_rule", "category": "tenant", "title": "Migration rule", "enforcement": "required", "scope": "all_tenants", "moduleLink": "", "active": True, "timeUpdated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; rules[] sentinel; description/config_json omitted; PHP platform governance authoritative; cutoverAllowed=false",
        },
        "cp-einvoice-documents.json": {
            **summary("cp", {"documentCount": 1, "openCount": 1, "submittedCount": 0, "totalInclVat": 105.0}),
            "documents": [{"id": 1, "uuid": "00000000-0000-0000-0000-000000000001", "invoiceNumber": "INV-1", "orderId": 0, "userId": 1, "docCategory": "tax_invoice", "issueDate": 0, "currencyCode": "AED", "status": "draft", "totalInclVat": 105.0, "validationOk": False, "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; documents[] sentinel; payloads omitted; PHP tax einvoice authoritative; cutoverAllowed=false",
        },
        "cp-jewellery-repairs.json": {
            **summary("cp", {"repairCount": 1, "openCount": 1, "authorizedCount": 0, "itemCount": 1}),
            "repairs": [{"id": 1, "companyId": 0, "branch": "HO", "vocType": "REP", "vocDate": "2024-01-01", "vocNo": 1, "customerName": "Migration customer", "status": "received", "currency": "AED", "deliveryDate": "", "authorized": False, "createdAt": ""}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; repairs[] sentinel; PII omitted; PHP jw_repairs authoritative; cutoverAllowed=false",
        },
        "cp-crm-tickets.json": {
            **summary("cp", {"ticketCount": 1, "openCount": 1, "highPriorityCount": 0, "messageCount": 0}),
            "tickets": [{"id": 1, "customerUserId": 1, "orderId": 0, "subject": "Migration ticket", "status": "open", "priority": "normal", "assignedUserId": 0, "timeCreated": 0, "timeUpdated": 0, "active": True}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; tickets[] sentinel; message bodies omitted; PHP CRM authoritative; cutoverAllowed=false",
        },
        "cp-soc2-compliance.json": {
            **summary("cp", {"controlCount": 1, "implementedCount": 0, "evidenceCount": 0, "policyCount": 0}),
            "controls": [{"id": 1, "controlId": "CC1.1", "category": "security", "title": "Migration control", "status": "not_started", "owner": "", "frequency": "annual", "riskLevel": "medium"}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; controls[] sentinel; description/implementation omitted; PHP soc2 authoritative; cutoverAllowed=false",
        },
        "cp-cost-models.json": {
            **summary("cp", {"itemCount": 1, "txnCount": 0, "closeCount": 0, "modelCount": 1}),
            "items": [{"id": 1, "companyId": 0, "itemId": 1, "model": "moving_avg", "stdCost": 0.0, "timeUpdated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; items[] sentinel; detail_json omitted; PHP cost_models authoritative; cutoverAllowed=false",
        },
        "cp-fin-advanced.json": {
            **summary("cp", {"periodCount": 1, "openPeriodCount": 1, "allocRuleCount": 0, "accrualCount": 0}),
            "periods": [{"id": 1, "companyId": 0, "fy": 2024, "periodNo": 1, "startDate": 0, "endDate": 0, "status": "open", "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; periods[] sentinel; JSON payloads omitted; PHP fin_advanced authoritative; cutoverAllowed=false",
        },
        "cp-blockchain-proofs.json": {
            **summary("cp", {"proofCount": 1, "pendingCount": 1, "anchoredCount": 0, "batchCount": 0}),
            "proofs": [{"id": 1, "proofUid": "proof-1", "tenantKey": "mig", "recordType": "invoice", "recordId": "1", "payloadHash": "0"*64, "status": "pending", "batchId": None, "anchorRef": "", "createdAt": ""}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; proofs[] sentinel; payload/merkle omitted; PHP blockchain_proofs authoritative; cutoverAllowed=false",
        },
        "cp-landed-cost.json": {
            **summary("cp", {"sheetCount": 1, "postedCount": 0, "expenseCount": 0, "lineCount": 0}),
            "sheets": [{"id": 1, "companyId": 0, "sheetNo": "LC-1", "poReference": "PO-1", "grnReference": "", "supplierId": 1, "supplierName": "Migration supplier", "goodsValue": 100.0, "totalExpenses": 10.0, "distributionMethod": "value", "currency": "AED", "status": "draft", "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; sheets[] sentinel; notes omitted; PHP landed_cost authoritative; cutoverAllowed=false",
        },
        "cp-warehouse-wms.json": {
            **summary("cp", {"locationCount": 1, "lpCount": 0, "waveCount": 0, "openWorkCount": 1}),
            "work": [{"id": 1, "companyId": 0, "workType": "pick", "reference": "SO-1", "waveId": 0, "item": "SKU-1", "qty": 1.0, "status": "open", "assignedTo": "", "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; work[] sentinel; PHP WMS authoritative; cutoverAllowed=false",
        },
        "cp-ai-service.json": {
            **summary("cp", {"queryCount": 1, "successCount": 1, "blockedCount": 0, "providerCount": 0}),
            "queries": [{"id": 1, "siteKey": "mig", "userId": 1, "service": "copilot", "intent": "orders", "tokensUsed": 0, "executionMs": 0, "piiStripped": 0, "status": "success", "createdAt": ""}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; queries[] sentinel; input/output omitted; PHP ai_service authoritative; cutoverAllowed=false",
        },
        "cp-returns-rma.json": {
            **summary("cp", {"rmaCount": 1, "openCount": 1, "activeWarrantyCount": 0, "itemCount": 0}),
            "requests": [{"id": 1, "siteKey": "mig", "rmaNumber": "RMA-1", "warrantyId": None, "customerId": 1, "customerName": "Migration customer", "reason": "defective", "status": "pending", "resolutionType": "none", "createdAt": ""}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; requests[] sentinel; description/notes omitted; PHP returns-manager authoritative; cutoverAllowed=false",
        },
        "cp-isolation-audit.json": {
            **summary("cp", {"runCount": 1, "failedRunCount": 0, "violationCount": 0, "siteCount": 0}),
            "runs": [{"id": 1, "runAt": "2026-01-01 00:00:00", "totalTenants": 1, "passed": 1, "failed": 0, "warnings": 0, "triggeredBy": "manual"}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; runs[] sentinel; report_json omitted; PHP commerce isolation audit authoritative; cutoverAllowed=false",
        },
        "cp-aml-compliance.json": {
            **summary("cp", {"kycCount": 1, "pendingKycCount": 1, "flaggedTxnCount": 0, "activeRuleCount": 0}),
            "kyc": [{"id": 1, "companyId": 0, "customerId": 1, "customerName": "Migration customer", "idType": "emirates_id", "riskLevel": "low", "pepStatus": 0, "verificationStatus": "pending", "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; kyc[] sentinel; notes/document paths omitted; PHP aml_compliance authoritative; cutoverAllowed=false",
        },
        "cp-jewellery-masters.json": {
            **summary("cp", {"karatCount": 1, "rateTypeCount": 0, "barcodeCount": 0, "diamondCount": 0}),
            "karats": [{"id": 1, "companyId": 0, "karatCode": "22K", "stdPurity": 0.916, "rangeFrom": 0.91, "rangeTo": 0.92, "spGravity": 0.0, "division": "G", "createdAt": ""}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; karats[] sentinel; description omitted; PHP jewellery masters authoritative; cutoverAllowed=false",
        },
        "cp-crm-activities.json": {
            **summary("cp", {"activityCount": 1, "openCount": 1, "overdueCount": 0, "doneCount": 0}),
            "activities": [{"id": 1, "activityType": "task", "relatedType": "lead", "relatedId": 1, "dueDate": 0, "done": 0, "ownerUserId": 1, "timeCreated": 0, "active": 1}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; activities[] sentinel; notes omitted; PHP CRM activities authoritative; cutoverAllowed=false",
        },
        "cp-auth-mfa.json": {
            **summary("cp", {"secretCount": 1, "confirmedCount": 1, "backupUnusedCount": 0, "policyCount": 1}),
            "secrets": [{"id": 1, "userId": 1, "method": "totp", "confirmed": 1, "label": "default", "createdAt": "", "lastUsedAt": ""}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; secrets[] sentinel; secret/webauthn omitted; PHP auth settings authoritative; cutoverAllowed=false",
        },
        "cp-electronic-reporting.json": {
            **summary("cp", {"formatCount": 1, "fieldCount": 0, "runCount": 0, "outputTypeCount": 1}),
            "formats": [{"id": 1, "companyId": 0, "code": "VENDLIST", "name": "Vendor list", "outputType": "csv", "rootElement": "rows", "rowElement": "row", "active": 1, "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; formats[] sentinel; preview omitted; PHP tax elec_reporting authoritative; cutoverAllowed=false",
        },
        "cp-collections-dunning.json": {
            **summary("cp", {"queueCount": 1, "openCount": 1, "profileCount": 1, "logCount": 0}),
            "queue": [{"id": 1, "siteKey": "demo", "customerId": 1, "customerName": "Acme", "invoiceRef": "INV-1", "invoiceAmount": 100.0, "amountDue": 100.0, "dueDate": "2026-01-01", "daysOverdue": 10, "dunningStep": 1, "status": "open", "updatedAt": ""}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; queue[] sentinel; notes omitted; PHP collections/dunning authoritative; cutoverAllowed=false",
        },
        "cp-consolidations.json": {
            **summary("cp", {"entityCount": 1, "figureCount": 0, "icCount": 0, "openIcCount": 0}),
            "entities": [{"id": 1, "code": "HOME", "name": "Migration entity", "currencyCode": "AED", "ownershipPct": 100.0, "isHome": 1, "parentCode": "", "active": 1, "timeCreated": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; entities[] sentinel; PHP consolidations authoritative; cutoverAllowed=false",
        },
        "cp-marketing-growth.json": {
            **summary("cp", {"taskCount": 1, "tasksDone": 0, "kpiLogCount": 0, "reviewCount": 1}),
            "reviews": [{"id": 1, "strategyKey": "growth", "reviewType": "weekly", "score": 3, "createdAt": 0, "createdBy": 0}],
            "count": 1, "source": "migration", "message": "TenantRegistry DB is not configured.",
            "dualSampleBaseline": "migration-contract-golden", "cutoverAllowed": False, "readyForPhpRemoval": False,
            "note": "migration-mode; reviews[] sentinel; notes omitted; PHP marketing growth authoritative; cutoverAllowed=false",
        },
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
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "data": [
                    {
                        "ART_ARTICLE_NR": "0986424590",
                        "SUP_BRAND": "BOSCH",
                        "ART_PRODUCT_NAME": "Migration product",
                    }
                ]
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": "migration-mode products nested data.data[] item-field sentinel; OfflineCacheOk object blob",
        },
        "api-catalog-engine-search.json": {
            "ok": True,
            "action": "engine_search",
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "code": "N47D20",
                "rows": 1,
                "data": [
                    {
                        "ENG_ID": 1,
                        "ENGINE_CODE": "N47D20",
                        "POWER_KW": 0,
                        "CAPACITY_LT": 0.0,
                        "FUEL_TYPE": "Diesel",
                    }
                ],
                "scanned_brands": 0,
                "truncated": False,
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": "migration-mode engine-search nested data.data[] item-field sentinel; OfflineCacheOk object blob",
        },
        "api-catalog-article-links.json": {
            "ok": True,
            "action": "article_links",
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "PC": [
                    {
                        "CI_FROM": 0,
                        "CI_TO": 0,
                        "POWER_KW": 0,
                        "FUEL_TYPE": "Petrol",
                    }
                ],
                "CV": [],
                "Motorcycle": [],
                "source": "migration",
                "stale": True,
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": "migration-mode article-links PC[] fitment item-field sentinel; OfflineCacheOk object blob",
        },
        "api-catalog-article.json": {
            "ok": True,
            "action": "article",
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "ART_ID": 1,
                "TITLE": "Migration article",
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": "migration-mode article object item-field sentinel; OfflineCacheOk object blob",
        },
        "api-catalog-articles.json": {
            "ok": True,
            "action": "articles",
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "data": [
                    {
                        "ART_ARTICLE_NR": "0986424590",
                        "SUP_BRAND": "BOSCH",
                        "ART_PRODUCT_NAME": "Migration articles row",
                    }
                ]
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": "migration-mode articles nested data.data[] item-field sentinel; OfflineCacheOk object blob",
        },
        "api-catalog-engine.json": {
            "ok": True,
            "action": "engine",
            "section": "passenger",
            "rows": 1,
            "source": "migration",
            "stale": True,
            "data": {
                "ENG_ID": 1,
                "ENGINE_CODE": "N47D20",
            },
            "dualSampleBaseline": "migration-contract-golden",
            "cutoverAllowed": False,
            "readyForPhpRemoval": False,
            "message": "migration-mode engine object item-field sentinel; OfflineCacheOk object blob",
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
