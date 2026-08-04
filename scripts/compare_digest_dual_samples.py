#!/usr/bin/env python3
"""Walk dual PHP/ASP.NET digest samples and compare against locked field contracts.

Pair resolution order per stem:
  1) php-{stem}.json + aspnet-{stem}.json (full dual, unless --contract-only)
  2) migration/{stem}.json + aspnet-{stem}.json (contract-only baseline; used when
     public digests are already exact-route shadowed so PHP JSON is unavailable)
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMPARE = ROOT / "scripts" / "compare_surface_payload_parity.py"

# route -> (summary path key, required summary fields)
# Covers every surface/storefront digest KPI + /cp/orders-digest (presentation shadow).
SUMMARY_CONTRACTS = {
    "cp-dashboard-summary": (
        "summary",
        "users,adminSessions,portalTenants,activePortalTenants,source,message",
    ),
    "erp-dashboard-summary": (
        "summary",
        "cashPosition,supplierCredit,supplierDebit,supplierNet,cashAccounts,activeSuppliers,activePurchases,source,message",
    ),
    "erp-accounts-summary": (
        "summary",
        "cashPosition,supplierCredit,supplierDebit,supplierNet,cashAccounts,activeSuppliers,activePurchases,source,message",
    ),
    "bos-fleet-summary": (
        "summary",
        "portalTenants,activePortalTenants,adminSessions,withDatabase,erpOnly,source,message",
    ),
    "bos-fleet-health": (
        "summary",
        "portalTenants,activePortalTenants,adminSessions,withDatabase,erpOnly,source,message",
    ),
    "storefront-account-summary": (
        "summary",
        "userId,orders,sessions,garageVehicles,source,message",
    ),
    "cp-orders-digest": (
        "summary",
        "open,today,pendingShip,source,message",
    ),
    "storefront-cart": (
        "summary",
        "count,sum,source,message",
    ),
    "cp-power-bi": (
        "summary",
        "siteKey,workspaceId,azureTenantId,defaultReportId,defaultDatasetId,embedUrl,embedMode,notes,active,reportCount,source,message",
    ),
    "cp-mobile-apps": (
        "summary",
        "enabled,appName,bundleId,deepLinkScheme,deepLinkDomain,apiBaseUrl,playStoreUrl,appStoreUrl,pwaEnabled,firebaseProjectId,pushEnabled,source,message",
    ),
    "cp-metabase": (
        "summary",
        "siteKey,metabaseUrl,active,dashboardCount,source,message",
    ),
    "cp-marketing-broadcast": (
        "summary",
        "campaigns,emailsSent,whatsappSent,source,message",
    ),
    "cp-parts-agent-chats": (
        "summary",
        "totalSessions,sessionsToday,messagesToday,loggedInSessions,guestSessions,enabled,agentName,domain,source,message",
    ),
    "cp-pos-overview": (
        "summary",
        "posEnabled,registerName,openSessions,salesToday,salesTotalToday,source,message",
    ),
    "cp-tax-toolkits": (
        "summary",
        "toolkitCount,installCount,tenantCountry,tenantKitCode,source,message",
    ),
    "cp-sms-whatsapp": (
        "summary",
        "smsOperators,activeOperator,whatsappSent,whatsappFailed,source,message",
    ),
    "cp-crm-board": (
        "summary",
        "leads,opportunities,activities,ticketsOpen,source,message",
    ),
    "cp-document-control": (
        "summary",
        "companyName,templateCount,attachmentCount,source,message",
    ),
    "cp-delivery-methods": (
        "summary",
        "methods,available,source,message",
    ),
    "cp-crosses": (
        "summary",
        "totalPairs,brands,source,message",
    ),
    "cp-hr-overview": (
        "summary",
        "activeEmployees,pendingLeave,payrollRuns,attendanceRows,source,message",
    ),
    "cp-production-overview": (
        "summary",
        "bomCount,openWorkOrders,completedWorkOrders,source,message",
    ),
    "cp-projects-overview": (
        "summary",
        "openProjects,taskCount,contractCount,source,message",
    ),
    "cp-industry-packs": (
        "summary",
        "packCount,activePacks,assignments,source,message",
    ),
    "cp-jewellery-retail": (
        "summary",
        "voucherCount,openVouchers,tagCount,metalStockRows,source,message",
    ),
    "cp-price-lists": (
        "summary",
        "activeLists,priceRows,uploadCount,source,message",
    ),
    "cp-auto-price": (
        "summary",
        "activeRules,activeSources,compareRuns,source,message",
    ),
    "cp-uae-tax-compliance": (
        "summary",
        "legislationCount,vatAdvanceRows,vatRefundRows,source,message",
    ),
    "cp-budgets": (
        "summary",
        "budgetCount,activeBudgets,budgetLineCount,dimensionCount,source,message",
    ),
    "cp-carriers": (
        "summary",
        "carrierCount,activeCarriers,rateCount,openShipments,source,message",
    ),
    "cp-payment-gateways": (
        "summary",
        "gatewayCount,activeGateways,selectableGateways,accountCount,source,message",
    ),
    "cp-workflows": (
        "summary",
        "workflowCount,activeWorkflows,runCount,failedRuns,source,message",
    ),
    "cp-purchase-requests": (
        "summary",
        "reqCount,draftCount,pendingApproval,lineCount,categoryCount,source,message",
    ),
    "cp-promotions": (
        "summary",
        "promotionCount,activePromotions,percentPromotions,loyaltyAccounts,source,message",
    ),
    "cp-crm-opportunities": (
        "summary",
        "opportunityCount,openOpportunities,wonOpportunities,pipelineAmount,source,message",
    ),
    "cp-integrations": (
        "summary",
        "webhookCount,activeWebhooks,deliveryCount,failedDeliveries,source,message",
    ),
    "erp-bank-reconciliation": (
        "summary",
        "lineCount,unmatchedCount,matchedCount,creditTotal,debitTotal,source,message",
    ),
    "erp-stock-transfers": (
        "summary",
        "transferCount,draftCount,inTransitCount,receivedCount,totalQty,source,message",
    ),
    "erp-sales-quotations": (
        "summary",
        "quoteCount,draftCount,sentCount,acceptedCount,subtotalSum,source,message",
    ),
    "erp-workspace-favorites": (
        "summary",
        "shortcutCount,pinnedCount,userCount,erpSurfaceCount,source,message",
    ),
    "erp-fixed-assets": (
        "summary",
        "assetCount,activeCount,disposedCount,costTotal,bookValueTotal,source,message",
    ),
    "cp-page-builder": (
        "summary",
        "layoutCount,publishedCount,draftCount,siteCount,source,message",
    ),
    "cp-product-catalogue": (
        "summary",
        "productCount,publishedCount,unpublishedCount,categoryCount,source,message",
    ),
    "cp-platform-governance": (
        "summary",
        "ruleCount,activeCount,requiredCount,categoryCount,source,message",
    ),
    "cp-einvoice-documents": (
        "summary",
        "documentCount,openCount,submittedCount,totalInclVat,source,message",
    ),
    "cp-jewellery-repairs": (
        "summary",
        "repairCount,openCount,authorizedCount,itemCount,source,message",
    ),
    "cp-crm-tickets": (
        "summary",
        "ticketCount,openCount,highPriorityCount,messageCount,source,message",
    ),
    "cp-marketing-growth": (
        "summary",
        "taskCount,tasksDone,kpiLogCount,reviewCount,source,message",
    ),
    "cp-soc2-compliance": (
        "summary",
        "controlCount,implementedCount,evidenceCount,policyCount,source,message",
    ),
    "cp-cost-models": (
        "summary",
        "itemCount,txnCount,closeCount,modelCount,source,message",
    ),
    "cp-fin-advanced": (
        "summary",
        "periodCount,openPeriodCount,allocRuleCount,accrualCount,source,message",
    ),
    "cp-blockchain-proofs": (
        "summary",
        "proofCount,pendingCount,anchoredCount,batchCount,source,message",
    ),
    "cp-landed-cost": (
        "summary",
        "sheetCount,postedCount,expenseCount,lineCount,source,message",
    ),
    "cp-warehouse-wms": (
        "summary",
        "locationCount,lpCount,waveCount,openWorkCount,source,message",
    ),
    "cp-ai-service": (
        "summary",
        "queryCount,successCount,blockedCount,providerCount,source,message",
    ),
    "cp-returns-rma": (
        "summary",
        "rmaCount,openCount,activeWarrantyCount,itemCount,source,message",
    ),
    "cp-isolation-audit": (
        "summary",
        "runCount,failedRunCount,violationCount,siteCount,source,message",
    ),
    "cp-aml-compliance": (
        "summary",
        "kycCount,pendingKycCount,flaggedTxnCount,activeRuleCount,source,message",
    ),
    "cp-jewellery-masters": (
        "summary",
        "karatCount,rateTypeCount,barcodeCount,diamondCount,source,message",
    ),
    "cp-consolidations": (
        "summary",
        "entityCount,figureCount,icCount,openIcCount,source,message",
    ),
    "cp-crm-activities": (
        "summary",
        "activityCount,openCount,overdueCount,doneCount,source,message",
    ),
    "cp-auth-mfa": (
        "summary",
        "secretCount,confirmedCount,backupUnusedCount,policyCount,source,message",
    ),
    "cp-electronic-reporting": (
        "summary",
        "formatCount,fieldCount,runCount,outputTypeCount,source,message",
    ),
    "cp-collections-dunning": (
        "summary",
        "queueCount,openCount,profileCount,logCount,source,message",
    ),
    "erp-inventory-stock": (
        "summary",
        "rowCount,qtyOnHand,stockValue,warehouseCount,itemCount,source,message",
    ),
    "bos-fleet-readiness": (
        "readiness",
        "tenants,pass,warn,fail,active,withDatabase,erpOnly,source,message",
    ),
}

# List digests: stem -> collection key (envelope ok/surface/key/count/source/message)
LIST_CONTRACTS = {
    "cp-tenants": "tenants",
    "cp-users": "users",
    "cp-groups": "groups",
    "cp-modules": "modules",
    "cp-menus": "menus",
    "cp-pages": "pages",
    "cp-currencies": "currencies",
    "cp-api-clients": "clients",
    "cp-config-items": "items",
    "cp-admin-sessions": "sessions",
    "cp-storages": "storages",
    "cp-nl-reporting": "definitions",
    "cp-demo-tenants": "tenants",
    "erp-suppliers": "suppliers",
    "erp-purchases": "purchases",
    "erp-cash-accounts": "accounts",
    "erp-cash-entries": "entries",
    "erp-coa-accounts": "accounts",
    "erp-warehouses": "warehouses",
    "erp-sales-orders": "orders",
    "erp-purchase-orders": "orders",
    "erp-invoices": "invoices",
    "erp-gl-journals": "journals",
    "bos-tenants": "tenants",
    "bos-audit-log": "entries",
    "storefront-orders": "orders",
    "storefront-garage": "vehicles",
    "storefront-search": "rows",
}

# Item-field contracts mirrored from SurfacePayloadContractCatalog / www-surface-field-parity.json.
# Migration goldens listed in LIST_NONEMPTY_MIGRATION must ship a sentinel row (empty fails).
LIST_ITEM_FIELDS = {
    "cp-tenants": [
        "siteKey",
        "hostname",
        "industryCode",
        "status",
        "tradeName",
        "hubName",
        "hostedOn",
        "erpOnly",
        "isActive",
        "hasDb",
    ],
    "cp-users": [
        "userId",
        "email",
        "phone",
        "unlocked",
        "timeRegistered",
        "timeLastVisit",
    ],
    "cp-groups": [
        "id",
        "value",
        "forBackend",
        "forGuests",
        "forRegistrated",
        "unblocked",
        "parent",
        "level",
    ],
    "cp-modules": [
        "id",
        "caption",
        "activated",
        "isFrontend",
        "isPrototype",
        "controlAvailable",
    ],
    "cp-menus": [
        "id",
        "caption",
        "isFrontend",
        "menuUlClass",
        "menuUlId",
        "structurePresent",
        "structureParseOk",
        "nodeCount",
        "maxDepth",
        "urlLinkCount",
        "contentLinkCount",
        "unknownLinkCount",
    ],
    "cp-pages": [
        "id",
        "caption",
        "url",
        "alias",
        "isFrontend",
        "published",
        "level",
        "sortOrder",
    ],
    "cp-currencies": [
        "id",
        "isoCode",
        "isoName",
        "captionShort",
        "rate",
        "available",
        "sortOrder",
    ],
    "cp-api-clients": [
        "id",
        "clientKeyPrefix",
        "product",
        "label",
        "contactEmail",
        "active",
        "dailyLimit",
        "callsToday",
        "timeCreated",
    ],
    "cp-config-items": ["name", "caption", "type", "configGroup", "visible", "order"],
    "cp-admin-sessions": ["userId", "email", "type", "sessionCount"],
    "cp-storages": ["id", "name", "shortName", "hidden"],
    "cp-nl-reporting": [
        "id",
        "siteKey",
        "name",
        "description",
        "reportType",
        "schedule",
        "format",
        "active",
        "createdBy",
    ],
    "cp-demo-tenants": [
        "siteKey",
        "hostname",
        "industryCode",
        "status",
        "tradeName",
        "hubName",
        "hostedOn",
        "erpOnly",
        "isActive",
        "demoExpiresAt",
        "demoContactEmail",
    ],
    "erp-suppliers": ["id", "name", "storageId", "balance"],
    "erp-purchases": [
        "id",
        "supplierId",
        "supplierName",
        "purchaseDate",
        "invoiceNumber",
        "totalAmount",
        "status",
        "orderId",
    ],
    "erp-cash-accounts": [
        "id",
        "name",
        "accountType",
        "currencyCode",
        "openingBalance",
        "balance",
    ],
    "erp-cash-entries": [
        "id",
        "accountId",
        "accountName",
        "accountType",
        "timeUnix",
        "direction",
        "amount",
        "reference",
        "note",
    ],
    "erp-coa-accounts": [
        "id",
        "code",
        "name",
        "accountType",
        "normalSide",
        "parentId",
        "openingBalance",
        "active",
    ],
    "erp-warehouses": ["id", "storageId", "code", "name", "active", "timeCreated"],
    "erp-sales-orders": [
        "id",
        "soNo",
        "customerUserId",
        "totalAmount",
        "status",
        "timeCreated",
    ],
    "erp-purchase-orders": [
        "id",
        "poNo",
        "supplierId",
        "title",
        "totalAmount",
        "status",
        "timeCreated",
    ],
    "erp-invoices": [
        "id",
        "invoiceNumber",
        "orderId",
        "userId",
        "customerEmail",
        "issueDate",
        "status",
        "totalInclVat",
    ],
    "erp-gl-journals": [
        "id",
        "journalNo",
        "journalDate",
        "sourceType",
        "sourceId",
        "status",
        "totalDebit",
    ],
    "bos-tenants": [
        "siteKey",
        "hostname",
        "industryCode",
        "status",
        "tradeName",
        "hubName",
        "hostedOn",
        "erpOnly",
        "isActive",
        "hasDb",
    ],
    "bos-audit-log": ["id", "ts", "userId", "actor", "area", "action", "target", "ip"],
    "storefront-orders": ["id", "timeUnix", "paid", "successfullyCreated", "status"],
    "storefront-garage": ["id", "caption", "marka", "model", "year", "vin", "active"],
    "storefront-search": [
        "priceId",
        "priceList",
        "manufacturer",
        "article",
        "articleShow",
        "name",
        "price",
        "exist",
        "storage",
    ],
}
LIST_NONEMPTY_MIGRATION = frozenset(LIST_ITEM_FIELDS)

# Summary+list hybrid digests: KPI summary already in SUMMARY_CONTRACTS; also lock list item fields.
HYBRID_LIST_ITEM_FIELDS = {
    "cp-power-bi": (
        "reports",
        [
            "id",
            "siteKey",
            "reportId",
            "reportName",
            "datasetId",
            "category",
            "embedUrl",
            "active",
        ],
    ),
    "cp-metabase": (
        "dashboards",
        [
            "id",
            "siteKey",
            "dashboardId",
            "dashboardName",
            "category",
            "active",
        ],
    ),
    "cp-marketing-broadcast": (
        "campaigns",
        [
            "id",
            "createdAt",
            "channel",
            "templateKey",
            "subject",
            "preview",
            "audienceMode",
            "audienceMeta",
            "totalTargets",
            "sentOk",
            "sentFail",
            "status",
            "operatorId",
        ],
    ),
    "cp-parts-agent-chats": (
        "sessions",
        [
            "sessionId",
            "updatedAt",
            "messageCount",
            "countryCode",
            "countryName",
            "userId",
            "ipHash",
            "lastUserText",
            "lastAgentText",
        ],
    ),
    "cp-pos-overview": (
        "sales",
        [
            "id",
            "saleNo",
            "sessionId",
            "customerLabel",
            "subtotalEx",
            "vatAmount",
            "totalAmount",
            "paymentMethod",
            "taxKitCode",
            "status",
            "timeCreated",
        ],
    ),
    "cp-tax-toolkits": (
        "toolkits",
        [
            "id",
            "kitCode",
            "name",
            "jurisdiction",
            "taxType",
            "isSystem",
            "active",
        ],
    ),
    "cp-sms-whatsapp": (
        "operators",
        [
            "id",
            "name",
            "handler",
            "description",
            "active",
            "controlAvailable",
        ],
    ),
    "cp-crm-board": (
        "leads",
        ["id", "title", "status", "source", "ownerId", "amount", "updatedAt"],
    ),
    "cp-document-control": (
        "templates",
        ["id", "code", "title", "category", "active", "sortOrder"],
    ),
    "cp-delivery-methods": (
        "modes",
        ["id", "caption", "handler", "available", "controlAvailable", "sortOrder"],
    ),
    "cp-crosses": (
        "pairs",
        ["id", "manufacturer", "article", "crossManufacturer", "crossArticle"],
    ),
    "cp-hr-overview": (
        "employees",
        ["id", "code", "name", "department", "status", "joinDate"],
    ),
    "cp-production-overview": (
        "workOrders",
        ["id", "woNo", "status", "qtyPlanned", "qtyProduced", "updatedAt"],
    ),
    "cp-projects-overview": (
        "projects",
        ["id", "code", "name", "status", "billingType", "contractValue"],
    ),
    "cp-industry-packs": (
        "packs",
        ["id", "packKey", "name", "description", "icon", "active"],
    ),
    "cp-jewellery-retail": (
        "vouchers",
        ["id", "vocType", "vocDate", "vocNo", "partyName", "status", "netAmount", "vatAmount", "totalWithVat"],
    ),
    "cp-price-lists": (
        "lists",
        ["id", "code", "name", "currency", "customerId", "priority", "active"],
    ),
    "cp-auto-price": (
        "rules",
        ["id", "siteKey", "ruleKey", "minMarginPercent", "autoUpdatePrices", "scheduleHours", "active", "updatedAt"],
    ),
    "cp-uae-tax-compliance": (
        "items",
        ["id", "slug", "title", "issueDate", "category", "taxCategory", "isNew", "isUpdated", "timeSynced"],
    ),
    "cp-budgets": (
        "budgets",
        ["id", "code", "name", "fiscalYear", "businessUnitId", "isMaster", "active"],
    ),
    "cp-carriers": (
        "carriers",
        ["id", "code", "name", "mode", "currency", "rating", "active"],
    ),
    "cp-payment-gateways": (
        "gateways",
        ["id", "name", "handler", "active", "isSelectable"],
    ),
    "cp-workflows": (
        "workflows",
        ["id", "siteKey", "name", "triggerType", "active", "version", "runCount", "lastRunStatus"],
    ),
    "cp-purchase-requests": (
        "requests",
        ["id", "companyId", "reqNumber", "requester", "businessUnitId", "status", "total", "requiresApproval", "poRef", "timeCreated"],
    ),
    "cp-promotions": (
        "promotions",
        ["id", "code", "name", "type", "value", "minSpend", "validFrom", "validTo", "active"],
    ),
    "cp-crm-opportunities": (
        "opportunities",
        ["id", "title", "stage", "amount", "probability", "closeDate", "ownerUserId", "leadId", "active"],
    ),
    "cp-integrations": (
        "integrations",
        ["id", "tenantKey", "url", "active", "description", "createdAt"],
    ),
    "erp-bank-reconciliation": (
        "lines",
        ["id", "accountId", "lineDate", "description", "reference", "amount", "direction", "matchedEntryId", "importBatch", "timeCreated"],
    ),
    "erp-stock-transfers": (
        "transfers",
        ["id", "companyId", "transferNo", "fromWarehouseId", "toWarehouseId", "reason", "status", "totalItems", "totalQty", "shippedAt", "receivedAt", "createdBy", "timeCreated"],
    ),
    "erp-sales-quotations": (
        "quotations",
        ["id", "opportunityId", "leadId", "customerUserId", "quoteNumber", "status", "currencyCode", "subtotal", "shopOrderId", "timeCreated", "active"],
    ),
    "erp-workspace-favorites": (
        "favorites",
        ["id", "companyId", "userId", "surface", "shortcutKey", "label", "iconClass", "targetUrl", "targetTab", "sortOrder", "isPinned", "timeCreated"],
    ),
    "erp-fixed-assets": (
        "assets",
        ["id", "assetCode", "name", "categoryId", "acquisitionDate", "cost", "salvageValue", "usefulLifeMonths", "depreciationMethod", "accumulatedDepreciation", "bookValue", "location", "status", "timeCreated"],
    ),
    "cp-page-builder": (
        "layouts",
        ["id", "siteKey", "pageKey", "isPublished", "updatedAt", "publishedAt"],
    ),
    "cp-product-catalogue": (
        "products",
        ["id", "categoryId", "caption", "alias", "publishedFlag"],
    ),
    "cp-platform-governance": (
        "rules",
        ["id", "ruleKey", "category", "title", "enforcement", "scope", "moduleLink", "active", "timeUpdated"],
    ),
    "cp-einvoice-documents": (
        "documents",
        ["id", "uuid", "invoiceNumber", "orderId", "userId", "docCategory", "issueDate", "currencyCode", "status", "totalInclVat", "validationOk", "timeCreated"],
    ),
    "cp-jewellery-repairs": (
        "repairs",
        ["id", "companyId", "branch", "vocType", "vocDate", "vocNo", "customerName", "status", "currency", "deliveryDate", "authorized", "createdAt"],
    ),
    "cp-crm-tickets": (
        "tickets",
        ["id", "customerUserId", "orderId", "subject", "status", "priority", "assignedUserId", "timeCreated", "timeUpdated", "active"],
    ),
    "cp-marketing-growth": (
        "reviews",
        ["id", "strategyKey", "reviewType", "score", "createdAt", "createdBy"],
    ),
    "cp-soc2-compliance": (
        "controls",
        ["id", "controlId", "category", "title", "status", "owner", "frequency", "riskLevel"],
    ),
    "cp-cost-models": (
        "items",
        ["id", "companyId", "itemId", "model", "stdCost", "timeUpdated"],
    ),
    "cp-fin-advanced": (
        "periods",
        ["id", "companyId", "fy", "periodNo", "startDate", "endDate", "status", "timeCreated"],
    ),
    "cp-blockchain-proofs": (
        "proofs",
        ["id", "proofUid", "tenantKey", "recordType", "recordId", "payloadHash", "status", "batchId", "anchorRef", "createdAt"],
    ),
    "cp-landed-cost": (
        "sheets",
        ["id", "companyId", "sheetNo", "poReference", "grnReference", "supplierId", "supplierName", "goodsValue", "totalExpenses", "distributionMethod", "currency", "status", "timeCreated"],
    ),
    "cp-warehouse-wms": (
        "work",
        ["id", "companyId", "workType", "reference", "waveId", "item", "qty", "status", "assignedTo", "timeCreated"],
    ),
    "cp-ai-service": (
        "queries",
        ["id", "siteKey", "userId", "service", "intent", "tokensUsed", "executionMs", "piiStripped", "status", "createdAt"],
    ),
    "cp-returns-rma": (
        "requests",
        ["id", "siteKey", "rmaNumber", "warrantyId", "customerId", "customerName", "reason", "status", "resolutionType", "createdAt"],
    ),
    "cp-isolation-audit": (
        "runs",
        ["id", "runAt", "totalTenants", "passed", "failed", "warnings", "triggeredBy"],
    ),
    "cp-aml-compliance": (
        "kyc",
        ["id", "companyId", "customerId", "customerName", "idType", "riskLevel", "pepStatus", "verificationStatus", "timeCreated"],
    ),
    "cp-jewellery-masters": (
        "karats",
        ["id", "companyId", "karatCode", "stdPurity", "rangeFrom", "rangeTo", "spGravity", "division", "createdAt"],
    ),
    "cp-consolidations": (
        "entities",
        ["id", "code", "name", "currencyCode", "ownershipPct", "isHome", "parentCode", "active", "timeCreated"],
    ),
    "cp-crm-activities": (
        "activities",
        ["id", "activityType", "relatedType", "relatedId", "dueDate", "done", "ownerUserId", "timeCreated", "active"],
    ),
    "cp-auth-mfa": (
        "secrets",
        ["id", "userId", "method", "confirmed", "label", "createdAt", "lastUsedAt"],
    ),
    "cp-electronic-reporting": (
        "formats",
        ["id", "companyId", "code", "name", "outputType", "rootElement", "rowElement", "active", "timeCreated"],
    ),
    "cp-collections-dunning": (
        "queue",
        ["id", "siteKey", "customerId", "customerName", "invoiceRef", "invoiceAmount", "amountDue", "dueDate", "daysOverdue", "dunningStep", "status", "updatedAt"],
    ),
    "cp-orders-digest": (
        "orders",
        [
            "id",
            "timeUnix",
            "userId",
            "status",
            "paid",
            "paidType",
            "officeId",
            "successfullyCreated",
            "countItems",
            "orderSum",
        ],
    ),
    "bos-fleet-health": (
        "sampleTenants",
        [
            "siteKey",
            "hostname",
            "industryCode",
            "status",
            "tradeName",
            "hubName",
            "hostedOn",
            "erpOnly",
            "isActive",
            "hasDb",
        ],
    ),
    "storefront-cart": (
        "lines",
        [
            "id",
            "price",
            "countNeed",
            "checkedForOrder",
            "productType",
            "manufacturer",
            "article",
            "name",
            "timeToExe",
            "timeToExeGuaranteed",
            "minOrder",
        ],
    ),
}

# Object digests without a collection array (top-level envelope fields).
OBJECT_CONTRACTS = {
    "storefront-profile": [
        "ok",
        "surface",
        "user_id",
        "email",
        "email_confirmed",
        "phone",
        "phone_confirmed",
        "reg_variant",
        "profile_fields",
        "source",
        "message",
        "session",
        "note",
    ],
}


def is_migration_baseline(path: Path) -> bool:
    """True for migration/ goldens or php-* seeded from those goldens after cutover."""
    if path.parent.name == "migration":
        return True
    try:
        doc = json.loads(path.read_text(encoding="utf-8"))
    except Exception:  # noqa: BLE001
        return False
    if not isinstance(doc, dict):
        return False
    if doc.get("dualSampleBaseline") == "migration-contract-golden":
        return True
    # Captured migration-mode payloads use source=migration (not live DB).
    summary = doc.get("summary") if isinstance(doc.get("summary"), dict) else {}
    readiness = doc.get("readiness") if isinstance(doc.get("readiness"), dict) else {}
    return summary.get("source") == "migration" or readiness.get("source") == "migration"


def resolve_left(samples: Path, stem: str) -> tuple[Path | None, bool]:
    """Return (left_path, used_migration_baseline).

    Prefer a real php-* capture when present and not a seeded migration baseline.
    Otherwise use migration/{stem}.json (or a seeded php-* baseline) for contract-only.
    """
    php = samples / f"php-{stem}.json"
    if php.exists() and not is_migration_baseline(php):
        return php, False
    mig = samples / "migration" / f"{stem}.json"
    if mig.exists():
        return mig, True
    if php.exists() and is_migration_baseline(php):
        return php, True
    return None, False


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--samples-dir",
        default=str(ROOT / "docs/migration/evidence/surface-parity/samples"),
        help="Directory containing php-*.json / aspnet-*.json / migration/",
    )
    parser.add_argument("--contract-only", action="store_true", help="Compare field presence only")
    parser.add_argument(
        "--out",
        type=Path,
        default=None,
        help="Optional compare-result JSON path (always cutoverAllowed=false)",
    )
    args = parser.parse_args()
    samples = Path(args.samples_dir)
    if not samples.is_dir():
        print(f"No samples directory: {samples}")
        return 0

    pairs = 0
    failed = 0
    used_migration = 0
    checked_stems: set[str] = set()

    def check_list_envelope(path: Path, key: str, label: str, stem: str = "") -> None:
        nonlocal pairs, failed
        pairs += 1
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            failed += 1
            print(f"FAIL {label}: {ex}")
            return
        required = ["ok", "surface", key, "count", "source", "message", "session", "note"]
        missing = [k for k in required if k not in doc]
        if missing:
            failed += 1
            print(f"FAIL {label}: missing {missing}")
            return

        rows = doc.get(key)
        item_fields = LIST_ITEM_FIELDS.get(stem) or []
        require_nonempty = (
            stem in LIST_NONEMPTY_MIGRATION
            and ("migration" in label or path.parent.name == "migration")
        )
        if require_nonempty and (not isinstance(rows, list) or len(rows) < 1):
            failed += 1
            print(f"FAIL {label}: expected non-empty {key}[] sentinel for item-field floor")
            return
        if item_fields and isinstance(rows, list) and rows:
            first = rows[0]
            if not isinstance(first, dict):
                failed += 1
                print(f"FAIL {label}: {key}[0] must be object")
                return
            item_missing = [f for f in item_fields if f not in first]
            if item_missing:
                failed += 1
                print(f"FAIL {label}: missing item fields {item_missing}")
                return
        print(f"PASS {label}")

    def check_object_envelope(path: Path, required: list[str], label: str) -> None:
        nonlocal pairs, failed
        pairs += 1
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            failed += 1
            print(f"FAIL {label}: {ex}")
            return
        missing = [k for k in required if k not in doc]
        if missing:
            failed += 1
            print(f"FAIL {label}: missing {missing}")
        else:
            print(f"PASS {label}")

    def check_hybrid_list_items(path: Path, stem: str, label: str) -> None:
        nonlocal pairs, failed
        spec = HYBRID_LIST_ITEM_FIELDS.get(stem)
        if not spec:
            return
        key, item_fields = spec
        pairs += 1
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except Exception as ex:  # noqa: BLE001
            failed += 1
            print(f"FAIL {label}: {ex}")
            return
        rows = doc.get(key)
        if not isinstance(rows, list) or len(rows) < 1:
            failed += 1
            print(f"FAIL {label}: expected non-empty {key}[] item-field sentinel")
            return
        first = rows[0]
        if not isinstance(first, dict):
            failed += 1
            print(f"FAIL {label}: {key}[0] must be object")
            return
        missing = [f for f in item_fields if f not in first]
        if missing:
            failed += 1
            print(f"FAIL {label}: missing item fields {missing}")
            return
        print(f"PASS {label}")

    for stem, (path_key, require) in SUMMARY_CONTRACTS.items():
        left, from_mig = resolve_left(samples, stem)
        asp = samples / f"aspnet-{stem}.json"
        if left is None or not asp.exists():
            continue
        pairs += 1
        checked_stems.add(stem)
        if from_mig:
            used_migration += 1
        contract_only = args.contract_only or from_mig
        cmd = [
            sys.executable,
            str(COMPARE),
            "--left",
            str(left),
            "--right",
            str(asp),
            "--path",
            path_key,
            "--require",
            require,
        ]
        if contract_only:
            cmd.append("--contract-only")
        proc = subprocess.run(cmd, capture_output=True, text=True)
        label = f"migration+aspnet/{stem}" if from_mig else stem
        if proc.returncode == 0:
            print(f"PASS {label}")
        else:
            failed += 1
            print(f"FAIL {label}")
            print(proc.stdout or proc.stderr)
        if stem in HYBRID_LIST_ITEM_FIELDS and left is not None:
            check_hybrid_list_items(
                left, stem, f"{'migration' if from_mig else 'php'}-{stem}-list-items"
            )
            if asp.exists():
                check_hybrid_list_items(asp, stem, f"aspnet-{stem}-list-items")

    for stem, key in LIST_CONTRACTS.items():
        left, from_mig = resolve_left(samples, stem)
        asp = samples / f"aspnet-{stem}.json"
        if left is not None and asp.exists():
            if from_mig:
                used_migration += 1
            check_list_envelope(
                left, key, f"{'migration' if from_mig else 'php'}-{stem}", stem=stem
            )
            check_list_envelope(asp, key, f"aspnet-{stem}", stem=stem)
            checked_stems.add(stem)

    for stem, required in OBJECT_CONTRACTS.items():
        left, from_mig = resolve_left(samples, stem)
        asp = samples / f"aspnet-{stem}.json"
        if left is not None and asp.exists():
            if from_mig:
                used_migration += 1
            check_object_envelope(left, required, f"{'migration' if from_mig else 'php'}-{stem}")
            check_object_envelope(asp, required, f"aspnet-{stem}")
            checked_stems.add(stem)

    # Contract-only floor: validate every registered migration golden that lacked an aspnet pair.
    mig = samples / "migration"
    if args.contract_only and mig.is_dir():
        for stem, (path_key, require) in SUMMARY_CONTRACTS.items():
            if stem in checked_stems:
                continue
            path = mig / f"{stem}.json"
            if not path.exists():
                continue
            pairs += 1
            checked_stems.add(stem)
            cmd = [
                sys.executable,
                str(COMPARE),
                "--left",
                str(path),
                "--right",
                str(path),
                "--path",
                path_key,
                "--require",
                require,
                "--contract-only",
            ]
            proc = subprocess.run(cmd, capture_output=True, text=True)
            if proc.returncode == 0:
                print(f"PASS migration/{stem}")
            else:
                failed += 1
                print(f"FAIL migration/{stem}")
                print(proc.stdout or proc.stderr)
            if stem in HYBRID_LIST_ITEM_FIELDS:
                check_hybrid_list_items(path, stem, f"migration/{stem}-list-items")
        for stem, key in LIST_CONTRACTS.items():
            if stem in checked_stems:
                continue
            path = mig / f"{stem}.json"
            if path.exists():
                check_list_envelope(path, key, f"migration/{stem}", stem=stem)
                checked_stems.add(stem)
        for stem, required in OBJECT_CONTRACTS.items():
            if stem in checked_stems:
                continue
            path = mig / f"{stem}.json"
            if path.exists():
                check_object_envelope(path, required, f"migration/{stem}")
                checked_stems.add(stem)

    report = {
        "pairsChecked": pairs,
        "failed": failed,
        "migrationBaselinePairs": used_migration,
        "contractsRegistered": (
            len(SUMMARY_CONTRACTS) + len(LIST_CONTRACTS) + len(OBJECT_CONTRACTS)
        ),
        "stemsChecked": len(checked_stems),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "contractOnly": bool(args.contract_only) or used_migration > 0,
        "listItemFieldStems": sorted(LIST_ITEM_FIELDS),
        "listNonemptyMigrationStems": sorted(LIST_NONEMPTY_MIGRATION),
        "hybridListItemFieldStems": sorted(HYBRID_LIST_ITEM_FIELDS),
        "note": (
            "Digest dual-sample contract floor. All list digests require non-empty "
            "migration item-field sentinels; hybrid summary digests also lock "
            "cp-orders-digest orders[] and bos-fleet-health sampleTenants[]. "
            "Never invents RELEASE_OWNER_APPROVAL.md."
        ),
    }
    text = json.dumps(report, indent=2) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
    print(text, end="")
    if pairs == 0:
        print("No dual php-/aspnet- digest sample pairs found (not a failure).")
        print("Capture ASP.NET samples: bash scripts/cloudpanel_capture_digest_dual_samples.sh")
        return 0
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
