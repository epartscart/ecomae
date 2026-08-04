#!/usr/bin/env bash
# Capture/refresh module-function parity inventory from full PHP catalog + hybrid TARGETS.
# Enumerates every CP/ERP/BOS/storefront catalog entry as php-only (or hybrid when TARGET matches).
# aspnet-complete stays 0. Never invents pass/approval files.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="${ECOMAE_MODULE_FUNCTION_SAMPLES_DIR:-$ROOT/docs/migration/evidence/module-function-parity}"
OVERWRITE="${ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES:-0}"
mkdir -p "$OUT_DIR"

export ECOMAE_MODULE_FUNCTION_SAMPLES_DIR="$OUT_DIR"
export ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES="$OVERWRITE"
export ECOMAE_HYBRID_CAPTURE="$ROOT/scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh"
export ECOMAE_PHP_MODULE_CATALOG="${ECOMAE_PHP_MODULE_CATALOG:-$ROOT/aspnet/src/EcomAE.Platform/Presentation/Generated/php_module_catalog.json}"

python3 - <<'PY'
import datetime
import json
import os
import re
from pathlib import Path

out_dir = Path(os.environ["ECOMAE_MODULE_FUNCTION_SAMPLES_DIR"])
overwrite = os.environ.get("ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES", "0") == "1"
repo = Path.cwd()
capture = Path(os.environ["ECOMAE_HYBRID_CAPTURE"]).read_text(encoding="utf-8")
block = capture.split("TARGETS = [", 1)[1].split("]", 1)[0]
row_re = re.compile(
    r'\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]*)"\s*,\s*"([^"]+)"'
)
now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

hybrid_by_php: dict[str, dict] = {}
hybrid_by_app: dict[str, dict] = {}
hybrid_modules = []
for stem, surface, app_route, digest_route, php_path in row_re.findall(block):
    status = "digest-only+hybrid-deeplink" if digest_route else "hybrid-deeplink"
    row = {
        "id": stem,
        "surface": surface,
        "kind": "hybrid-preview",
        "aspnetRoute": app_route,
        "digestRoute": digest_route or None,
        "phpPath": php_path,
        "status": status,
        "aspnetComplete": False,
        "writesRemainPhp": True,
        "humanFunctionalEvidence": False,
        "note": "Hybrid preview/deeplink only — interactive product chrome remains PHP.",
    }
    hybrid_modules.append(row)
    hybrid_by_app[app_route.rstrip("/")] = row

# Fill php path index after normalize helper exists below.

catalog_path = Path(os.environ["ECOMAE_PHP_MODULE_CATALOG"])
if not catalog_path.is_file():
    raise SystemExit(f"FAIL: missing PHP module catalog: {catalog_path}")
catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
catalog_counts = catalog.get("counts") if isinstance(catalog.get("counts"), dict) else {}


from urllib.parse import parse_qs, urlsplit


def normalize_php(url: str) -> str:
    value = url.strip()
    # Strip scheme/host for storefront absolute URLs.
    if "://" in value:
        value = "/" + value.split("://", 1)[1].split("/", 1)[-1]
    return value.rstrip("/") or "/"


def to_erp_shell_key(url: str) -> str | None:
    """Canonicalize CP/ERP shell URLs to /ERP/?epc_erp_shell=1&area=&tab= form."""
    key = normalize_php(url)
    lower = key.lower()
    if "/shop/finance/erp" not in lower and not lower.startswith("/erp"):
        return None
    parts = urlsplit(key if "?" in key or key.startswith("/") else f"/{key}")
    qs = parse_qs(parts.query, keep_blank_values=True)
    area = (qs.get("area") or [""])[0].strip()
    tab = (qs.get("tab") or [""])[0].strip()
    if not area:
        return None
    if tab:
        return f"/ERP/?epc_erp_shell=1&area={area}&tab={tab}"
    return f"/ERP/?epc_erp_shell=1&area={area}"


BROAD_PHP_PATHS = {
    "/",
    "/CP",
    "/ERP",
    "/BOS",
    "/cp",
    "/erp",
    "/bos",
    "/storefront",
}

for hybrid in hybrid_modules:
    key = normalize_php(str(hybrid.get("phpPath") or ""))
    # Do not auto-upgrade whole surfaces when TARGET phpPath is a root chrome URL.
    if key in BROAD_PHP_PATHS:
        continue
    hybrid_by_php[key] = hybrid
    erp_key = to_erp_shell_key(key)
    if erp_key:
        hybrid_by_php[erp_key] = hybrid


# Storefront catalog ids → hybrid TARGET stems (path-only matching collides:
# part_search is shared by sf-search + sf-garage).
STOREFRONT_HYBRID_BY_ID = {
    "part_search": "sf-search",
    "cart": "sf-cart",
    "orders": "sf-orders",
    "garage": "sf-garage",
    "account": "sf-account-summary",
    "profile": "sf-profile",
    # Wave 7 — checkout/payments attach to CP payment-gateways digest (read-only handlers)
    "checkout": "cp-payment-gateways",
    "payments": "cp-payment-gateways",
    # Wave 10 — catalogue storefront id → product catalogue digest
    "catalog": "cp-product-catalogue",
    # Wave 13 — returns/support attach to returns-rma digest
    "returns": "cp-returns-rma",
    "support": "cp-returns-rma",
    # Wave 17 — near-complete storefront leftovers
    "home": "sf-account-summary",
    "vin_search": "sf-search",
}

# Explicit catalog-id maps where PHP paths diverge from hybrid TARGET paths.
CP_FEATURE_HYBRID_BY_ID = {
    "user-manager": "cp-users",
    "super-cp-operator-console": "cp-dashboard-summary",
    # Prefer CP tenants digest for CP brochure rows; BOS module keeps /bos/tenants via path.
    "tenant-control-center": "cp-tenants",
    "epc-tenant-control-center": "cp-tenants",
    "power-bi": "cp-power-bi",
    "power-bi-guide": "cp-power-bi",
    "mobile-apps": "cp-mobile-apps",
    "epc-mobile-apps": "cp-mobile-apps",
    "metabase": "cp-metabase",
    "metabase-embed": "cp-metabase",
    "epc-metabase-embed": "cp-metabase",
    "nl-reporting": "cp-nl-reporting",
    "epc-nl-reporting": "cp-nl-reporting",
    "marketing-broadcast": "cp-marketing-broadcast",
    "epc-marketing-broadcast": "cp-marketing-broadcast",
    "demo-tenants": "cp-demo-tenants",
    "epc-demo-tenants-manage": "cp-demo-tenants",
    "ai-parts-agent-chat": "cp-parts-agent-chats",
    "ai-parts-expert-chats": "cp-parts-agent-chats",
    "parts-agent": "cp-parts-agent-chats",
    "epc-pos-tenant-manage": "cp-pos-overview",
    "pos-tenant-overview": "cp-pos-overview",
    "pos-terminal": "cp-pos-overview",
    "epc-tax-toolkit-manage": "cp-tax-toolkits",
    "tax-compliance-center": "cp-tax-toolkits",
    "tax-e-invoicing": "cp-einvoice-documents",
    "worldwide-tax-toolkit": "cp-tax-toolkits",
    "peppol-pint-ae-e-invoice": "cp-einvoice-documents",
    "sms-operators": "cp-sms-whatsapp",
    "whatsapp-order-sharing": "cp-sms-whatsapp",
    "whatsapp-sharing-guide": "cp-sms-whatsapp",
    "crm": "cp-crm-board",
    "customer-board": "cp-crm-board",
    "epc-super-cp-customer-board": "cp-crm-board",
    "customer-management": "cp-crm-board",
    "customer-management-hub": "cp-crm-board",
    "crm-pipeline-kanban": "cp-crm-board",
    "sales-and-marketing-crm": "cp-crm-board",
    "document-control": "cp-document-control",
    "document-library-archive": "cp-document-control",
    "document-templates": "cp-document-control",
    "print-documents-pdfs": "cp-document-control",
    "delivery-notes-pick-lists": "cp-document-control",
    "delivery-methods": "cp-delivery-methods",
    "delivery-methods-pickup": "cp-delivery-methods",
    "cross-references": "cp-crosses",
    "cross-reference-lookup": "cp-crosses",
    "crosses": "cp-crosses",
    "human-resources-hr": "cp-hr-overview",
    "human-resources-workers": "cp-hr-overview",
    "human-resources-hr-operations": "cp-hr-overview",
    "human-resources-performance-management": "cp-hr-overview",
    "human-resources-recruitment": "cp-hr-overview",
    "leave-and-absence-hr-operations": "cp-hr-overview",
    "payroll-payroll": "cp-hr-overview",
    "production-control-production": "cp-production-overview",
    "production-control-production-planning": "cp-production-overview",
    "production-control-quality-management": "cp-production-overview",
    "project-management-and-accounting-projects": "cp-projects-overview",
    "project-management-and-accounting-project-accounting": "cp-projects-overview",
    "service-management-contracts-e-sign": "cp-projects-overview",
    "service-management-after-sales-rma": "cp-projects-overview",
    "consultancy-services-template": "cp-projects-overview",
    "rental-operations-template": "cp-projects-overview",
    "industry-settings": "cp-industry-packs",
    "industry-settings-deploy": "cp-industry-packs",
    "cp-module-packs-per-industry": "cp-industry-packs",
    "packs-manager": "cp-industry-packs",
    "autoworkshop-guide": "cp-industry-packs",
    "auto-spare-parts-template": "cp-industry-packs",
    "fashion-beauty-template": "cp-industry-packs",
    "medical-supplies-template": "cp-industry-packs",
    "modular-erp-access": "cp-industry-packs",
    # Wave 6 — jewellery / retail (brochure ids without cp- prefix)
    "retail-and-commerce-retail-commerce": "cp-jewellery-retail",
    "sales-and-marketing-metal-sales-jw": "cp-jewellery-retail",
    "sales-and-marketing-pos-advance-jw": "cp-jewellery-retail",
    "jewellery-luxury-template": "cp-jewellery-retail",
    "general-ledger-journal-voucher-jewellery-jw": "cp-jewellery-retail",
    "general-ledger-petty-cash-jewellery-jw": "cp-jewellery-retail",
    "inventory-management-barcode-jewellery-jw": "cp-jewellery-retail",
    "system-administration-currency-master-jewellery-jw": "cp-jewellery-retail",
    "system-administration-sample-data-jewellery-jw": "cp-jewellery-retail",
    # Wave 6 — price lists (do not map manufacturer-synonyms / catalogue products)
    "price-lists": "cp-price-lists",
    "price-management": "cp-price-lists",
    "edit-price-rows": "cp-price-lists",
    "supplier-price-upload": "cp-price-lists",
    "bulk-csv-import-export": "cp-price-lists",
    "downloadable-price-lists": "cp-price-lists",
    "send-price-lists": "cp-price-lists",
    "price-upload-guide": "cp-price-lists",
    "multivendor-prices": "cp-price-lists",
    "live-supplier-price-search": "cp-price-lists",
    "price-configs": "cp-price-lists",
    "epc-super-cp-price-configs": "cp-price-lists",
    "price-lists-customer-margins": "cp-price-lists",
    # Wave 6 — auto-price
    "epc-auto-price-engine": "cp-auto-price",
    "auto-price-ai": "cp-auto-price",
    "auto-price-ai__cp-control-portal-epc-auto-price-engine": "cp-auto-price",
    # Wave 6 — UAE tax (do not remap toolkit/einvoice/peppol / governance)
    "uae-tax-compliance": "cp-uae-tax-compliance",
    "tax-vat-return": "cp-uae-tax-compliance",
    "tax-tax-compliance": "cp-uae-tax-compliance",
    "tax-tourist-vat-refunds": "cp-uae-tax-compliance",
    "tax-tourist-vat-refund-jewellery-jw": "cp-uae-tax-compliance",
    "tax-withholding-tax": "cp-uae-tax-compliance",
    "uae-vat-201-return-full-format": "cp-uae-tax-compliance",
    # Wave 7 — budgets / finance-gl
    "budgeting-budget-planning": "cp-budgets",
    "budgeting-budgeting": "cp-budgets",
    "erp_budgeting": "cp-budgets",
    # Wave 7 — carriers / shipping-logistics
    "carriers-shipments": "cp-carriers",
    "custom-shipping": "cp-carriers",
    "custom-shipping-declarations": "cp-carriers",
    "custom-shipping-guide": "cp-carriers",
    "epc-custom-shipping-guide": "cp-carriers",
    "custom-shipping-guide__cp-shop-finance-erp-custom-shipping-guide-epc-erp-shell-1": "cp-carriers",
    "logistics-customs-shipping": "cp-carriers",
    "logistics-guide": "cp-carriers",
    "logistics-hub": "cp-carriers",
    "logistics-reports": "cp-carriers",
    # Wave 7 — payment gateways / storefront-checkout
    "payment-gateways": "cp-payment-gateways",
    "payment-gateway-setup": "cp-payment-gateways",
    "payment-reconciliation": "cp-payment-gateways",
    "payments-guide": "cp-payment-gateways",
    # Wave 7 — workflows / ai-automation
    "system-administration-workflow-automation": "cp-workflows",
    "home-workflow": "cp-workflows",
    "cross-department-workflow": "cp-workflows",
    # Wave 8 — procurement-ap / purchase requests
    "procurement-and-sourcing-purchase-requisitions": "cp-purchase-requests",
    "procurement-and-sourcing-rfq": "cp-purchase-requests",
    "procurement-and-sourcing-categories-policies": "cp-purchase-requests",
    "rfq-supplier-quotes": "cp-purchase-requests",
    "procurement-workflows": "cp-purchase-requests",
    # Wave 8 — marketing-growth / promotions
    "promo-merchandising": "cp-promotions",
    "promos-discount-rules": "cp-promotions",
    # Wave 8 — crm-customers / opportunities
    "leads-opportunities": "cp-crm-opportunities",
    "sales-and-marketing-opportunities": "cp-crm-opportunities",
    "sales-and-marketing-prospects-leads": "cp-crm-opportunities",
    "activities-follow-ups": "cp-crm-activities",
    # Wave 8 — integrations-api
    "epc-integrations-hub": "cp-integrations",
    "integrations-hub": "cp-integrations",
    "channel-integrations-pack": "cp-integrations",
    "system-administration-data-integration": "cp-integrations",
    # Wave 9 — banking-cash / bank reconciliation
    "bank-reconciliation": "erp-bank-reconciliation",
    "cash-and-bank-management-bank-recon": "erp-bank-reconciliation",
    "cash-and-bank-management-bank-accounts": "erp-bank-reconciliation",
    "cash-and-bank-management-bank-instruments": "erp-bank-reconciliation",
    "cash-and-bank-management-cash-flow-forecast": "erp-bank-reconciliation",
    "cash-and-bank-management-payment-batches": "erp-bank-reconciliation",
    "payment-batches-erp-treasury": "erp-bank-reconciliation",
    "cash-book": "erp-bank-reconciliation",
    # Wave 9 — inventory-stock / stock transfers
    "inventory-management-inventory-stock-groups": "erp-stock-transfers",
    "master-planning-master-planning": "erp-stock-transfers",
    "product-information-management-product-information": "erp-stock-transfers",
    "inventory_forecast": "erp-stock-transfers",
    # Wave 9 — sales-oms / quotations
    "sales-and-marketing-sales-quotations": "erp-sales-quotations",
    "oms-daily-guide": "erp-sales-quotations",
    "order-fulfilment-guide": "erp-sales-quotations",
    "order-items": "erp-sales-quotations",
    "order-statuses": "erp-sales-quotations",
    "abandoned-carts": "erp-sales-quotations",
    "channel-order-routing": "erp-sales-quotations",
    "channels-orders-fleet-oms": "erp-sales-quotations",
    "sao-status-links": "erp-sales-quotations",
    "inventory-management-order-planning": "erp-sales-quotations",
    # Wave 9 — erp-workspace-misc / favorites
    "common-agenda": "erp-workspace-favorites",
    "common-contacts": "erp-workspace-favorites",
    "common-knowledge-base": "erp-workspace-favorites",
    "home-dashboard": "erp-workspace-favorites",
    "home-process-flow": "erp-workspace-favorites",
    "business-unit-financial-dimensions": "erp-workspace-favorites",
    "organization-administration-business-unit": "erp-workspace-favorites",
    "organization-administration-contracts-e-sign": "erp-workspace-favorites",
    "organization-administration-listing": "erp-workspace-favorites",
    "organization-administration-organization-administration": "erp-workspace-favorites",
    "system-administration-accounting-setup": "erp-workspace-favorites",
    "system-administration-data-import": "erp-workspace-favorites",
    "enterprise-modules-d365-style": "erp-workspace-favorites",
    # Wave 10 — finance-gl / fixed assets
    "asset-management-fixed-assets": "erp-fixed-assets",
    "fixed-assets-depreciation": "erp-fixed-assets",
    "fixed-assets-fixed-assets": "erp-fixed-assets",
    "erp_fixed_assets": "erp-fixed-assets",
    # Wave 10 — document-control / page builder content pages
    "epc-visual-page-editor": "cp-page-builder",
    "epc-visual-page-editor__cp-control-portal-epc-visual-page-editor": "cp-page-builder",
    "visual-page-editor": "cp-page-builder",
    "content-tree": "cp-page-builder",
    # Wave 10 — pricing-catalog-cp / product catalogue
    "product-catalogue": "cp-product-catalogue",
    "products": "cp-product-catalogue",
    "catalogue-tree": "cp-product-catalogue",
    "categories-attributes": "cp-product-catalogue",
    "homepage-products": "cp-product-catalogue",
    "related-products": "cp-product-catalogue",
    "variant-skus-size-colour-spec": "cp-product-catalogue",
    "line-lists": "cp-product-catalogue",
    "tree-lists": "cp-product-catalogue",
    "featured-collections-banners": "cp-product-catalogue",
    "special-searches": "cp-product-catalogue",
    "vehicle-fitment-catalogue": "cp-product-catalogue",
    # Wave 10 — platform-ops-bos / governance
    "system-administration-platform-services": "cp-platform-governance",
    "operator-guide": "cp-platform-governance",
    "epc-super-cp-operator-guide": "cp-platform-governance",
    "command-center": "cp-platform-governance",
    "operator-audit-platform-settings": "cp-platform-governance",
    # Wave 11 — pos-retail / jewellery repairs (CP brochure ids)
    "service-management-repair-jobs-jw": "cp-jewellery-repairs",
    "service-management-repair-receipt-jw": "cp-jewellery-repairs",
    "service-management-repair-transfer-jw": "cp-jewellery-repairs",
    "service-management-workshop-receive-jw": "cp-jewellery-repairs",
    "service-management-repair-delivery-jw": "cp-jewellery-repairs",
    "service-management-repair-sale-jw": "cp-jewellery-repairs",
    "service-management-repair-register-jw": "cp-jewellery-repairs",
    "service-management-repair-search-jw": "cp-jewellery-repairs",
    "inventory-management-metal-stock-master-jw": "cp-jewellery-retail",
    "inventory-management-rate-type-master-jw": "cp-jewellery-retail",
    # Wave 11 — crm-customers / tickets
    "customer-reviews": "cp-crm-tickets",
    "customer-groups-profiles": "cp-crm-tickets",
    "customer-approvals": "cp-crm-tickets",
    "customer-approval-workflows": "cp-crm-tickets",
    "customer-balance-credit-limits": "cp-crm-tickets",
    "accounts-receivable-customer-setup": "cp-crm-tickets",
    # Wave 11 — marketing-growth
    "marketing-growth": "cp-marketing-growth",
    "marketing-campaigns-hub": "cp-marketing-growth",
    "ten-growth-strategies": "cp-marketing-growth",
    "sales-and-marketing-marketing": "cp-marketing-growth",
    "epc-social-media-hub": "cp-marketing-growth",
    "social-media-hub": "cp-marketing-growth",
    # Wave 12 — soc2 / cost-models / fin-advanced / blockchain
    "cost-management-costing-value-models": "cp-cost-models",
    "cost-accounting-financial-depth": "cp-fin-advanced",
    "general-ledger-financial-depth": "cp-fin-advanced",
    "tax-blockchain-proofs": "cp-blockchain-proofs",
    "audit-workbench-blockchain-proofs": "cp-blockchain-proofs",
    # Wave 13 — procurement-ap / shipping-logistics / ai-automation / storefront-checkout
    "landed-cost-landed-cost": "cp-landed-cost",
    "expense-management-expense-reports": "cp-landed-cost",
    "procurement-and-sourcing-3-way-match": "cp-landed-cost",
    "procurement-and-sourcing-supplier-portal": "cp-landed-cost",
    "accounts-payable-vendor-setup": "cp-landed-cost",
    "vendor-sourcing-fleet": "cp-landed-cost",
    "procurement-suppliers": "cp-landed-cost",
    "supplier-directory": "cp-landed-cost",
    "supplier-payables": "cp-landed-cost",
    "material-handling-equipment-interface-warehouse-management": "cp-warehouse-wms",
    "warehouse-management-warehouse-management": "cp-warehouse-wms",
    "warehouse-inventory-fleet": "cp-warehouse-wms",
    "branch-offices": "cp-warehouse-wms",
    "branch-office-list": "cp-warehouse-wms",
    "stock-levels": "cp-warehouse-wms",
    "logistics-procurement-cp": "cp-warehouse-wms",
    "common-ai-advisor": "cp-ai-service",
    "system-administration-devin-ai-assistant": "cp-ai-service",
    "ai-vin-decode-assistance": "cp-ai-service",
    "returns-rma-handling": "cp-returns-rma",
    "returns-manager": "cp-returns-rma",
    "account-operations": "cp-returns-rma",
    "accounts-receivable": "cp-returns-rma",
    "accounts-receivable-receivables": "cp-returns-rma",
    "storefront-theme-packages": "cp-returns-rma",
    # Wave 14 — platform-ops / tax-compliance / pos-retail / finance-gl
    "commerce-isolation-audit": "cp-isolation-audit",
    "inventory-management-karat-master-jw": "cp-jewellery-masters",
    "inventory-management-colour-stone-master-jw": "cp-jewellery-masters",
    "inventory-management-design-master-jw": "cp-jewellery-masters",
    "inventory-management-diamond-master-jw": "cp-jewellery-masters",
    "inventory-management-pearl-master-jw": "cp-jewellery-masters",
    "inventory-management-retail-barcode": "cp-jewellery-masters",
    "consolidations-consolidation": "cp-consolidations",
    "consolidations-multi-entity": "cp-consolidations",
    # Wave 15 — crm / auth / tax / collections
    "cp-auth-settings": "cp-auth-mfa",
    "epc-cp-auth-settings": "cp-auth-mfa",
    "tax-electronic-reporting": "cp-electronic-reporting",
    "credit-and-collections-collections": "cp-collections-dunning",
    # Wave 16 — marketplace / demand / credit / insurance + near-complete leftover aliases
    "channels-guide": "cp-marketplace-channels",
    "marketplace-listing-sync": "cp-marketplace-channels",
    "omnichannel-hub": "cp-marketplace-channels",
    "accessories-marketplace": "cp-marketplace-channels",
    "demand-countries": "cp-demand-intelligence",
    "demand-intelligence": "cp-demand-intelligence",
    "compliance-insurance": "cp-insurance-compliance",
    "manufacturer-synonyms": "cp-product-catalogue",
    "cash-and-bank-management-petty-cash": "erp-cash-entries",
    "3-way-match-po-grn-invoice": "cp-landed-cost",
    "purchase-orders": "erp-purchase-orders",
    "sales-and-marketing-subscriptions": "erp-sales-quotations",
    "sales-and-marketing-delivery-notes": "cp-warehouse-wms",
    "sales-and-marketing-fulfilment": "erp-sales-quotations",
    "sales-and-marketing-revenue": "erp-sales-quotations",
    # Wave 17 — audit / doc-expiry / tenant-config / jewellery stock + near-complete leftovers
    "audit-workbench-audit-trail": "cp-audit-trail",
    "compliance-document-expiry": "cp-doc-expiry",
    "tax-document-control": "cp-doc-expiry",
    "system-administration-tenant-configuration": "cp-tenant-config",
    "tenant-features": "cp-tenant-config",
    "epc-tenant-features": "cp-tenant-config",
    "tenant-hub": "cp-tenant-config",
    "epc-tenant-email-settings": "cp-tenant-config",
    "tenant-e-mail-smtp": "cp-tenant-config",
    "multi-tenant-hostname-routing": "cp-tenant-config",
    "ssl-dns-onboarding-assist": "cp-tenant-config",
    "inventory-management-stock-verification-jw": "cp-jewellery-stock-verification",
    "system-administration-print-designer": "cp-document-control",
    "common-documents": "cp-document-control",
    "organization-administration-document-formats": "cp-document-control",
    "online-cash-registers": "cp-pos-overview",
    "api-key-settings": "cp-integrations",
    "packs-setup-upload": "cp-industry-packs",
    "system-administration-security-roles": "cp-platform-governance",
    "registration-fields": "cp-auth-mfa",
    "registration-options": "cp-auth-mfa",
    "vin-oem-number-search": "cp-product-catalogue",
    "electronics-retail-template": "cp-industry-packs",
    "sales-and-marketing-retail-sales-pos-jw": "cp-jewellery-retail",
    "sales-and-marketing-sales-analysis-weight-jw": "cp-jewellery-retail",
    "sales-and-marketing-sales-forming-jw": "cp-jewellery-retail",
    "sales-and-marketing-sales-return-jw": "cp-jewellery-retail",

    # Wave 18 — tax/crm/pos/finance closeout + singles
    "built-in-compliance-engine": "cp-tax-external-reporting",
    "business-valuation-report": "cp-tax-external-reporting",
    "corporate-tax-return-full-computation": "cp-tax-external-reporting",
    "drill-down-to-transaction-level": "cp-tax-external-reporting",
    "external-audit-report-isa-700-ifrs": "cp-tax-external-reporting",
    "financial-model-5-year-forecast": "cp-tax-external-reporting",
    "flexible-reporting-periods": "cp-tax-external-reporting",
    "fta-style-audit-file-schedules": "cp-tax-external-reporting",
    "off-system-excel-import-return": "cp-tax-external-reporting",
    "professional-pdf-commentary": "cp-tax-external-reporting",
    "statutory-reporting-centre": "cp-tax-external-reporting",
    "tax-external-reporting": "cp-tax-external-reporting",
    "home-approvals": "cp-po-approvals",
    "b2b-trade-accounts": "cp-po-approvals",
    "customer-approval-workflows": "cp-po-approvals",
    "customer-approvals": "cp-po-approvals",
    "general-ledger-opening-balances": "cp-finance-close",
    "general-ledger-year-end-closing": "cp-finance-close",
    "general-ledger-reports": "cp-finance-close",
    "general-ledger-profit-loss": "cp-finance-close",
    "general-ledger-balance-sheet": "erp-gl-journals",
    "general-ledger-trial-balance-reports": "erp-gl-journals",
    "p-l-balance-sheet": "erp-gl-journals",
    "aging-statements-of-account": "erp-gl-journals",
    "general-ledger-aging-ar-ap-inv": "erp-gl-journals",
    "period-as-of-date-reporting": "cp-finance-close",
    "sales-and-marketing-sales-fixing-jw": "cp-jewellery-fixing",
    "procurement-and-sourcing-diamond-purchase-jw": "cp-jewellery-fixing",
    "procurement-and-sourcing-metal-purchase-jw": "cp-jewellery-fixing",
    "procurement-and-sourcing-purchase-fixing-jw": "cp-jewellery-fixing",
    "procurement-and-sourcing-purchase-window-jw": "cp-jewellery-fixing",
    "general-ledger-dual-trial-balance-wt-val-jw": "cp-jewellery-fixing",
    "general-ledger-petty-cash-jewellery-jw": "cp-jewellery-fixing",
    "inventory-management-stock-balance-weight-jw": "cp-jewellery-retail",
    "add-user": "cp-users",
    "api-documentation-guide": "cp-integrations",
    "epc-api-documentation-guide": "cp-integrations",
    "human-resources-labour-law-compliance": "cp-hr-overview",
    "tax-advisory-template": "cp-uae-tax-compliance",
    "credit-notes-adjustments": "cp-returns-rma",

    # Wave 19 — CMS/platform leftovers (table-backed)
    "web-tracker": "cp-web-tracker",
    "shop-statistics": "cp-web-tracker",
    "quote-requests": "cp-quote-requests",
    "communication": "cp-platform-communication",
    "epc-super-cp-communication": "cp-platform-communication",
    "epc-super-cp-info-blocks": "cp-info-blocks",
    "info-blocks-cms": "cp-info-blocks",

    # Wave 20 — free-tools / sandbox / marketplace portal / notifications + ERP brochure aliases
    "free-tools-admin": "cp-free-tools",
    "notification-settings": "cp-notifications",
    "erp-dashboard-kpis": "erp-dashboard-summary",
    "multi-company-erp-access": "erp-dashboard-summary",
    "netsuite-dashboard-d365-forms": "erp-dashboard-summary",

    # Wave 21a — portal settings / data migrations + brochure aliases
    "data-migration": "cp-data-migrations",
    "erp-guide": "erp-dashboard-summary",
}

ERP_AREA_HYBRID_BY_ID = {
    "audit_wb": "cp-audit-trail",

    "cost_acct": "cp-fin-advanced",
    "cost_mgmt": "cp-cost-models",
    "landed_cost_area": "cp-landed-cost",
    "expense": "cp-landed-cost",
    "mhei": "cp-warehouse-wms",
    "ar": "cp-returns-rma",
    "consolidations": "cp-consolidations",
    "credit_coll": "cp-collections-dunning",
    "banking": "erp-accounts-summary",
    "finance": "erp-coa-accounts",
    "sales": "erp-sales-orders",
    "purchasing": "erp-purchase-orders",
    "inventory_mgmt": "erp-inventory-stock",
    "warehouse": "erp-warehouses",
    "ap": "erp-suppliers",
    "people": "cp-hr-overview",
    "leave_abs": "cp-hr-overview",
    "payroll_area": "cp-hr-overview",
    "production": "cp-production-overview",
    "projects": "cp-projects-overview",
    "service_mgmt": "cp-projects-overview",
    "retail": "cp-jewellery-retail",
    "tax": "cp-uae-tax-compliance",
    "budgeting": "cp-budgets",
    "logistics": "cp-carriers",

    "master_planning_area": "erp-stock-transfers",
    "pim": "erp-stock-transfers",
    "common": "erp-workspace-favorites",
    "enterprise": "erp-workspace-favorites",
    "setup": "erp-workspace-favorites",
    "fixed_assets": "erp-fixed-assets",
    "asset_mgmt": "erp-fixed-assets",
    "risk": "cp-insurance-compliance",

}
ERP_TAB_HYBRID_BY_ID = {
    ("overview", "dashboard"): "erp-dashboard-summary",
    ("banking", "petty_cash"): "erp-cash-entries",
    ("people", "hr"): "cp-hr-overview",
    ("people", "hr_ops"): "cp-hr-overview",
    ("people", "staff"): "cp-hr-overview",
    ("people", "recruitment"): "cp-hr-overview",
    ("people", "performance"): "cp-hr-overview",
    ("leave_abs", "hr_ops"): "cp-hr-overview",
    ("payroll_area", "payroll"): "cp-hr-overview",
    ("production", "manufacturing"): "cp-production-overview",
    ("production", "mfg_planning"): "cp-production-overview",
    ("production", "quality"): "cp-production-overview",
    ("projects", "projects"): "cp-projects-overview",
    ("projects", "project_accounting"): "cp-projects-overview",
    ("service_mgmt", "contracts"): "cp-projects-overview",
    ("service_mgmt", "aftersales"): "cp-projects-overview",
    ("retail", "retail_commerce"): "cp-jewellery-retail",
    ("sales", "jw_retail_sales"): "cp-jewellery-retail",
    ("sales", "jw_metal_sales"): "cp-jewellery-retail",
    ("sales", "jw_sales_fixing"): "cp-jewellery-retail",
    ("sales", "jw_sales_return"): "cp-jewellery-retail",
    ("sales", "jw_pos_advance"): "cp-jewellery-retail",
    ("sales", "jw_sales_analysis"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_karat"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_rate_type"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_metal_stock"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_design"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_diamond"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_pearl"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_color_stone"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_stock_verification"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_stock_balance"): "cp-jewellery-retail",
    ("inventory_mgmt", "jw_barcode"): "cp-jewellery-retail",
    ("inventory_mgmt", "jewellery_tag"): "cp-jewellery-retail",
    ("purchasing", "jw_metal_purchase"): "cp-jewellery-retail",
    ("purchasing", "jw_diamond_purchase"): "cp-jewellery-retail",
    ("purchasing", "jw_purchase_fixing"): "cp-jewellery-retail",
    ("purchasing", "jw_purchase_window"): "cp-jewellery-retail",
    ("service_mgmt", "jw_repairs"): "cp-jewellery-repairs",
    ("service_mgmt", "jw_repair_receipt"): "cp-jewellery-repairs",
    ("service_mgmt", "jw_repair_transfer"): "cp-jewellery-repairs",
    ("service_mgmt", "jw_workshop_receive"): "cp-jewellery-repairs",
    ("service_mgmt", "jw_repair_delivery"): "cp-jewellery-repairs",
    ("service_mgmt", "jw_repair_sale"): "cp-jewellery-repairs",
    ("service_mgmt", "jw_repair_register"): "cp-jewellery-repairs",
    ("service_mgmt", "jw_repair_search"): "cp-jewellery-repairs",
    ("setup", "jw_seed_data"): "cp-jewellery-retail",
    ("setup", "jw_currency"): "cp-jewellery-retail",
    ("tax", "vat_return"): "cp-uae-tax-compliance",
    ("tax", "tax_compliance"): "cp-uae-tax-compliance",
    ("tax", "vat_refund"): "cp-uae-tax-compliance",
    ("tax", "withholding"): "cp-uae-tax-compliance",
    ("tax", "compliance"): "cp-uae-tax-compliance",
    ("tax", "jw_tourist_vat"): "cp-uae-tax-compliance",
    ("tax", "einvoice"): "cp-einvoice-documents",

    ("cost_acct", "fin_advanced"): "cp-fin-advanced",
    ("finance", "fin_advanced"): "cp-fin-advanced",
    ("cost_mgmt", "cost_models"): "cp-cost-models",
    ("tax", "blockchain_proofs"): "cp-blockchain-proofs",
    ("audit_wb", "blockchain_proofs"): "cp-blockchain-proofs",
    ("landed_cost_area", "landed_cost"): "cp-landed-cost",
    ("expense", "expense_reports"): "cp-landed-cost",
    ("purchasing", "three_way_match"): "cp-landed-cost",
    ("purchasing", "supplier_portal"): "cp-landed-cost",
    ("ap", "ap_setup"): "cp-landed-cost",
    ("mhei", "wms"): "cp-warehouse-wms",
    ("warehouse", "wms"): "cp-warehouse-wms",
    ("sales", "delivery_notes"): "cp-warehouse-wms",
    ("common", "ai_advisor"): "cp-ai-service",
    ("finance", "accounting_automation"): "cp-ai-service",
    ("setup", "ai_assistant"): "cp-ai-service",
    ("setup", "accounting_automation"): "cp-ai-service",
    ("ar", "receivables"): "cp-returns-rma",
    ("ar", "ar_setup"): "cp-returns-rma",
    ("tax", "aml_compliance"): "cp-aml-compliance",
    ("inventory_mgmt", "retail_barcode"): "cp-jewellery-masters",
    ("consolidations", "consolidation_bu"): "cp-consolidations",
    ("consolidations", "multi_entity"): "cp-consolidations",
    ("tax", "elec_reporting"): "cp-electronic-reporting",
    ("credit_coll", "collections"): "cp-collections-dunning",
    ("risk", "insurance"): "cp-insurance-compliance",
    ("audit_wb", "audit"): "cp-audit-trail",
    ("risk", "doc_expiry"): "cp-doc-expiry",
    ("tax", "document_control"): "cp-doc-expiry",
    ("setup", "tenant_config"): "cp-tenant-config",
    ("setup", "data_migration"): "cp-data-migrations",
    ("setup", "print_designer"): "cp-document-control",
    ("inventory_mgmt", "jw_stock_verification"): "cp-jewellery-stock-verification",
    ("setup", "security_roles"): "cp-platform-governance",
    ("sales", "gold_scheme"): "cp-jewellery-retail",
    ("common", "documents"): "cp-document-control",
    ("enterprise", "doc_formats"): "cp-document-control",
    ("sales", "marketing"): "cp-marketing-growth",

    ("budgeting", "budgeting"): "cp-budgets",
    ("budgeting", "budget_planning"): "cp-budgets",
    ("logistics", "custom_shipping"): "cp-carriers",
    ("logistics", "procurement_link"): "cp-carriers",
    ("overview", "workflow"): "cp-workflows",
    ("overview", "workflow_automation"): "cp-workflows",
    ("setup", "workflow_automation"): "cp-workflows",
    ("purchasing", "purchase_requisitions"): "cp-purchase-requests",
    ("purchasing", "rfq"): "cp-purchase-requests",
    ("purchasing", "procurement_categories"): "cp-purchase-requests",
    ("sales", "opportunities"): "cp-crm-opportunities",
    ("sales", "leads"): "cp-crm-opportunities",
    ("sales", "crm"): "cp-crm-tickets",
    ("setup", "integration"): "cp-integrations",
    ("banking", "bank_recon"): "erp-bank-reconciliation",
    ("banking", "bank_setup"): "erp-bank-reconciliation",
    ("banking", "bank_instruments"): "erp-bank-reconciliation",
    ("banking", "cash_forecast"): "erp-bank-reconciliation",
    ("banking", "payment_batches"): "erp-bank-reconciliation",
    ("inventory_mgmt", "inv_groups"): "erp-stock-transfers",
    ("inventory_mgmt", "gold_rate"): "erp-stock-transfers",
    ("master_planning_area", "master_planning"): "erp-stock-transfers",
    ("pim", "product_info"): "erp-stock-transfers",
    ("sales", "proposals"): "erp-sales-quotations",
    ("sales", "subscriptions"): "erp-sales-quotations",
    ("sales", "revenue"): "erp-sales-quotations",
    ("sales", "fulfilment"): "erp-sales-quotations",
    ("inventory_mgmt", "order_planning"): "erp-sales-quotations",
    ("common", "agenda"): "erp-workspace-favorites",
    ("common", "contacts"): "erp-workspace-favorites",
    ("common", "knowledge_base"): "erp-workspace-favorites",
    ("overview", "processflow"): "erp-workspace-favorites",
    ("enterprise", "business_units"): "erp-workspace-favorites",
    ("enterprise", "contracts"): "erp-workspace-favorites",
    ("enterprise", "listing"): "erp-workspace-favorites",
    ("enterprise", "org_admin"): "erp-workspace-favorites",
    ("setup", "data_import"): "cp-data-migrations",
    ("setup", "erp_setup"): "erp-workspace-favorites",
    ("fixed_assets", "fixed_assets"): "erp-fixed-assets",
    ("asset_mgmt", "fixed_assets"): "erp-fixed-assets",
    ("setup", "platform"): "cp-platform-governance",
    ("tax", "ext_reports"): "cp-tax-external-reporting",
    ("overview", "approvals"): "cp-po-approvals",
    ("finance", "opening_balances"): "cp-finance-close",
    ("finance", "year_end"): "cp-finance-close",
    ("finance", "reports"): "cp-finance-close",
    ("finance", "pl"): "cp-finance-close",
    ("finance", "balance_sheet"): "erp-gl-journals",
    ("finance", "enterprise_reports"): "erp-gl-journals",
    ("finance", "aging"): "erp-gl-journals",
    ("finance", "jw_trial_balance"): "cp-jewellery-fixing",
    ("finance", "jw_petty_cash"): "cp-jewellery-fixing",
    ("finance", "jw_journal_voucher"): "cp-jewellery-retail",
    ("purchasing", "jw_purchase_fixing"): "cp-jewellery-fixing",
    ("purchasing", "jw_purchase_window"): "cp-jewellery-fixing",
    ("purchasing", "jw_metal_purchase"): "cp-jewellery-fixing",
    ("purchasing", "jw_diamond_purchase"): "cp-jewellery-fixing",
    ("sales", "jw_sales_fixing"): "cp-jewellery-fixing",
    ("people", "hr_law"): "cp-hr-overview",

}
ERP_CATEGORY_HYBRID_BY_ID = {
    "cash_treasury": "erp-cash-accounts",
    "record_to_report": "erp-coa-accounts",
    "procure_to_pay": "erp-purchase-orders",
    "order_to_cash": "erp-sales-orders",
    "inventory_fulfillment": "erp-inventory-stock",
    "hr_payroll": "cp-hr-overview",
    "compliance_tax": "cp-uae-tax-compliance",
}
BOS_SECTION_HYBRID_BY_ID = {
    "logistics": "cp-warehouse-wms",
    "marketing_cp": "cp-marketing-growth",
    "fleet": "bos-fleet-health",
    "tenants": "bos-tenants",
    "platform": "bos-fleet-readiness",
    "erp": "erp-dashboard-summary",
    "tax_advisory": "cp-uae-tax-compliance",
    "catalogue": "cp-product-catalogue",
    "auto_parts": "cp-product-catalogue",
    "commerce": "cp-marketplace-channels",
    "professional": "cp-industry-packs",

}
BOS_MODULE_HYBRID_BY_ID = {
    "seo": "cp-marketing-growth",

    "soc2_compliance": "cp-soc2-compliance",
    "ai_copilot": "cp-ai-service",
    "ai_service": "cp-ai-service",
    "import_orchestrator": "cp-ai-service",
    "isolation_anomaly": "cp-ai-service",
    "isolation_audit": "cp-isolation-audit",
    "industry_consol": "cp-consolidations",
    "multi_entity": "cp-consolidations",
    "license_trends": "cp-consolidations",
    "modern_auth": "cp-auth-mfa",
    "collections_dunning": "cp-collections-dunning",
    "channels": "cp-marketplace-channels",
    "demand": "cp-demand-intelligence",
    "credit_limit": "cp-credit-limits",
    "tenant_features": "cp-tenant-config",
    "tenant_hub": "cp-tenant-config",
    "tenant_email": "cp-tenant-config",

    "social": "cp-marketing-growth",
    "fleet_cp": "bos-fleet-summary",
    "fleet_erp": "bos-fleet-summary",
    "erp_cash": "erp-cash-accounts",
    "erp_warehouse": "erp-warehouses",
    "power_bi": "cp-power-bi",
    "power_bi_guide": "cp-power-bi",
    "metabase_embed": "cp-metabase",
    "nl_reporting": "cp-nl-reporting",
    "marketing": "cp-marketing-broadcast",
    "broadcast": "cp-marketing-broadcast",
    "demo_tenants": "cp-demo-tenants",
    "parts_agent": "cp-parts-agent-chats",
    "pos": "cp-pos-overview",
    "tax_toolkit": "cp-tax-toolkits",
    "sms_turning": "cp-sms-whatsapp",
    "crm": "cp-crm-board",
    "customer_board": "cp-crm-board",
    "customers": "cp-crm-board",
    "documents": "cp-document-control",
    "document_vault": "cp-document-control",
    "crosses": "cp-crosses",
    "erp_hr": "cp-hr-overview",
    "erp_payroll": "cp-hr-overview",
    "wps_payroll": "cp-hr-overview",
    "erp_production": "cp-production-overview",
    "erp_projects": "cp-projects-overview",
    "industry_packs": "cp-industry-packs",
    "pricing": "cp-price-lists",
    "prices_upload": "cp-price-lists",
    "prices_edit": "cp-price-lists",
    "prices_send": "cp-price-lists",
    "prices_guide": "cp-price-lists",
    "multivendor": "cp-price-lists",
    "auto_price": "cp-auto-price",
    "erp_tax": "cp-uae-tax-compliance",
    "erp_budgeting": "cp-budgets",
    "logistics": "cp-carriers",
    "fulfillment_queue": "cp-carriers",
    "payments": "cp-payment-gateways",
    "promotions_engine": "cp-promotions",
    "integrations": "cp-integrations",
    "erp_ap": "cp-purchase-requests",
    "procurement": "cp-purchase-requests",
    "order_erp_pipeline": "erp-sales-quotations",
    "orders": "erp-sales-quotations",
    "subscription_billing": "erp-sales-quotations",
    "inventory_forecast": "erp-stock-transfers",
    "erp_fixed_assets": "erp-fixed-assets",
    "products": "cp-product-catalogue",
    "section-catalogue": "cp-product-catalogue",
    "sku_media": "cp-product-catalogue",
    "synonyms": "cp-product-catalogue",
    "data_policy": "cp-platform-governance",
    "operator_guide": "cp-platform-governance",
    "command_center": "cp-platform-governance",
    "platform_health": "cp-platform-governance",
    "po_approval": "cp-po-approvals",
    "erp_gl": "erp-gl-journals",
    "api_docs": "cp-integrations",
    "multi_currency_gl": "cp-currencies",
    "quotes": "cp-crm-opportunities",
    "returns": "cp-returns-rma",
    "warranty_rma": "cp-returns-rma",
    "free_tools": "cp-free-tools",
    "config_sandbox": "cp-config-sandbox",
    "marketplace": "cp-marketplace-apps",
    "statistics": "cp-platform-governance",
    "communication": "cp-platform-communication",
    "portal_settings": "cp-portal-settings",
}

hybrid_by_stem = {str(h.get("id")): h for h in hybrid_modules}


def match_hybrid(
    php_href: str | None,
    app_hint: str | None = None,
    *,
    storefront_id: str | None = None,
    cp_feature_id: str | None = None,
    erp_area_id: str | None = None,
    erp_tab_id: str | None = None,
    erp_category_id: str | None = None,
    bos_section_id: str | None = None,
    bos_module_id: str | None = None,
) -> dict | None:
    # Storefront surfaces only match via explicit id → TARGET stem.
    # Path-only matching is unsafe (part_search is shared by search + garage).
    if storefront_id is not None:
        stem = STOREFRONT_HYBRID_BY_ID.get(storefront_id)
        return hybrid_by_stem.get(stem) if stem else None
    if cp_feature_id is not None and cp_feature_id in CP_FEATURE_HYBRID_BY_ID:
        hit = hybrid_by_stem.get(CP_FEATURE_HYBRID_BY_ID[cp_feature_id])
        if hit:
            return hit
    if erp_category_id is not None and erp_category_id in ERP_CATEGORY_HYBRID_BY_ID:
        hit = hybrid_by_stem.get(ERP_CATEGORY_HYBRID_BY_ID[erp_category_id])
        if hit:
            return hit
    if bos_section_id is not None and bos_section_id in BOS_SECTION_HYBRID_BY_ID:
        hit = hybrid_by_stem.get(BOS_SECTION_HYBRID_BY_ID[bos_section_id])
        if hit:
            return hit
    if erp_area_id is not None and erp_tab_id is not None:
        stem = ERP_TAB_HYBRID_BY_ID.get((erp_area_id, erp_tab_id))
        if stem:
            hit = hybrid_by_stem.get(stem)
            if hit:
                return hit
    if erp_area_id is not None and erp_area_id in ERP_AREA_HYBRID_BY_ID and erp_tab_id is None:
        hit = hybrid_by_stem.get(ERP_AREA_HYBRID_BY_ID[erp_area_id])
        if hit:
            return hit
    if bos_module_id is not None and bos_module_id in BOS_MODULE_HYBRID_BY_ID:
        hit = hybrid_by_stem.get(BOS_MODULE_HYBRID_BY_ID[bos_module_id])
        if hit:
            return hit
    # Never match catalog rows by aspnetRoute alone — only concrete PHP paths.
    if not php_href:
        return None
    key = normalize_php(php_href)
    if key in BROAD_PHP_PATHS:
        return None
    hit = hybrid_by_php.get(key)
    if hit:
        return hit
    erp_key = to_erp_shell_key(php_href)
    if erp_key:
        hit = hybrid_by_php.get(erp_key)
        if hit:
            return hit
        # Area-only ERP shell URLs (no tab): attach area digest when mapped.
        qs = parse_qs(urlsplit(erp_key).query, keep_blank_values=True)
        area = (qs.get("area") or [""])[0].strip()
        tab = (qs.get("tab") or [""])[0].strip()
        if area and not tab and area in ERP_AREA_HYBRID_BY_ID:
            return hybrid_by_stem.get(ERP_AREA_HYBRID_BY_ID[area])
    return None


def entry(
    *,
    entry_id: str,
    surface: str,
    kind: str,
    label: str,
    php_path: str,
    aspnet_route: str | None = None,
    digest_route: str | None = None,
    extra: dict | None = None,
    storefront_id: str | None = None,
    cp_feature_id: str | None = None,
    erp_area_id: str | None = None,
    erp_tab_id: str | None = None,
    erp_category_id: str | None = None,
    bos_section_id: str | None = None,
    bos_module_id: str | None = None,
) -> dict:
    hybrid = match_hybrid(
        php_path,
        aspnet_route,
        storefront_id=storefront_id,
        cp_feature_id=cp_feature_id,
        erp_area_id=erp_area_id,
        erp_tab_id=erp_tab_id,
        erp_category_id=erp_category_id,
        bos_section_id=bos_section_id,
        bos_module_id=bos_module_id,
    )
    if hybrid:
        status = hybrid["status"]
        aspnet_route = hybrid.get("aspnetRoute") or aspnet_route
        digest_route = hybrid.get("digestRoute") or digest_route
        note = (
            f"Catalog {kind} matched hybrid TARGET {hybrid['id']}. "
            "Interactive product chrome remains PHP."
        )
    else:
        status = "php-only"
        note = (
            f"Full PHP catalog {kind} — no ASP.NET interactive parity yet; "
            "hybrid directory may deeplink to PHP."
        )
    row = {
        "id": entry_id,
        "surface": surface,
        "kind": kind,
        "label": label,
        "aspnetRoute": aspnet_route,
        "digestRoute": digest_route,
        "phpPath": php_path,
        "status": status,
        "aspnetComplete": False,
        "writesRemainPhp": True,
        "humanFunctionalEvidence": False,
        "note": note,
    }
    if extra:
        row.update(extra)
    return row


modules: list[dict] = []

for area in catalog.get("erpAreas") or []:
    if not isinstance(area, dict):
        continue
    area_id = str(area.get("id") or "")
    area_href = str(area.get("href") or f"/ERP/?epc_erp_shell=1&area={area_id}")
    modules.append(
        entry(
            entry_id=f"erp-area-{area_id}",
            surface="erp",
            kind="erp-area",
            label=str(area.get("label") or area_id),
            php_path=area_href,
            erp_area_id=area_id,
            extra={"areaId": area_id},
        )
    )
    for tab in area.get("tabs") or []:
        if not isinstance(tab, dict):
            continue
        tab_id = str(tab.get("id") or "")
        tab_href = str(
            tab.get("href")
            or f"/ERP/?epc_erp_shell=1&area={area_id}&tab={tab_id}"
        )
        modules.append(
            entry(
                entry_id=f"erp-tab-{area_id}-{tab_id}",
                surface="erp",
                kind="erp-tab",
                label=str(tab.get("label") or tab_id),
                php_path=tab_href,
                erp_area_id=area_id,
                erp_tab_id=tab_id,
                extra={"areaId": area_id, "tabId": tab_id},
            )
        )

for cat in catalog.get("erpCategories") or []:
    if not isinstance(cat, dict):
        continue
    cat_id = str(cat.get("id") or "")
    modules.append(
        entry(
            entry_id=f"erp-category-{cat_id}",
            surface="erp",
            kind="erp-category",
            label=str(cat.get("label") or cat_id),
            php_path=str(cat.get("href") or f"/ERP/?epc_erp_shell=1&category={cat_id}"),
            erp_category_id=cat_id,
            extra={"categoryId": cat_id},
        )
    )

for sec in catalog.get("bosSections") or []:
    if not isinstance(sec, dict):
        continue
    sec_id = str(sec.get("id") or "")
    modules.append(
        entry(
            entry_id=f"bos-section-{sec_id}",
            surface="bos",
            kind="bos-section",
            label=str(sec.get("label") or sec_id),
            php_path=str(sec.get("href") or f"/BOS/?section={sec_id}"),
            bos_section_id=sec_id,
            extra={"sectionKey": sec.get("key")},
        )
    )

for bos in catalog.get("bosModules") or []:
    if not isinstance(bos, dict):
        continue
    bos_id = str(bos.get("id") or "")
    modules.append(
        entry(
            entry_id=f"bos-{bos_id}",
            surface="bos",
            kind="bos-module",
            label=str(bos.get("label") or bos_id),
            php_path=str(bos.get("href") or f"/BOS/"),
            bos_module_id=bos_id,
            extra={"path": bos.get("path"), "section": bos.get("section")},
        )
    )

for cp in catalog.get("cpBrochureFeatures") or []:
    if not isinstance(cp, dict):
        continue
    cp_id = str(cp.get("id") or "")
    modules.append(
        entry(
            entry_id=f"cp-{cp_id}",
            surface="cp",
            kind="cp-feature",
            label=str(cp.get("name") or cp_id),
            php_path=str(cp.get("href") or "/CP/"),
            cp_feature_id=cp_id,
            extra={
                "category": cp.get("category"),
                "scope": cp.get("scope"),
                "does": cp.get("does"),
            },
        )
    )

for sf in catalog.get("storefrontSurfaces") or []:
    if not isinstance(sf, dict):
        continue
    sf_id = str(sf.get("id") or "")
    modules.append(
        entry(
            entry_id=f"storefront-{sf_id}",
            surface="storefront",
            kind="storefront-surface",
            label=str(sf.get("label") or sf_id),
            php_path=str(sf.get("href") or "https://epartscart.com/"),
            storefront_id=sf_id,
        )
    )

# Ensure every hybrid TARGET appears even if catalog href matching missed it.
seen_ids = {m["id"] for m in modules}
for hybrid in hybrid_modules:
    if hybrid["id"] not in seen_ids and f"hybrid-{hybrid['id']}" not in seen_ids:
        row = dict(hybrid)
        row["id"] = f"hybrid-{hybrid['id']}"
        modules.append(row)

hybrid_preview_count = sum(1 for m in modules if m.get("status") != "php-only")
php_only_count = sum(1 for m in modules if m.get("status") == "php-only")

inventory_path = out_dir / "module-function-inventory.json"
if inventory_path.exists() and not overwrite:
    print(f"keep existing {inventory_path}")
else:
    doc = {
        "role": "module-function-inventory",
        "capturedAt": now,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspnetCompleteCount": 0,
        "moduleCount": len(modules),
        "hybridPreviewCount": hybrid_preview_count,
        "phpOnlyCount": php_only_count,
        "phpCatalogCounts": catalog_counts,
        "phpCatalogScopeNote": (
            "modules[] enumerates the full PHP catalog (ERP areas/tabs/categories, BOS modules, "
            "CP brochure features, storefront surfaces). Hybrid TARGET matches upgrade status; "
            "everything else remains php-only until human MODULE_FUNCTION_TEST_PASS.md exists."
        ),
        "source": (
            "aspnet/.../Generated/php_module_catalog.json + "
            "scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh TARGETS"
        ),
        "note": (
            "Full PHP module/menu/function inventory contract. "
            "aspnet-complete remains 0 until human MODULE_FUNCTION_TEST_PASS.md exists."
        ),
        "modules": modules,
    }
    inventory_path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(
        f"wrote {inventory_path} moduleCount={len(modules)} "
        f"hybridPreviewCount={hybrid_preview_count} phpOnlyCount={php_only_count} "
        f"phpCatalogCounts={catalog_counts}"
    )

readme = out_dir / "README.md"
if overwrite or not readme.exists():
    readme.write_text(
        "# Module function parity evidence\n\n"
        "Full PHP catalog inventory (CP/ERP/BOS/storefront). Hybrid TARGETS upgrade a subset "
        "to deeplink/digest status; all other entries stay `php-only`. "
        "`aspnet-complete` count stays **0** until a human attaches "
        "`docs/migration/evidence/presentation/MODULE_FUNCTION_TEST_PASS.md` containing "
        "`MODULE_FUNCTION_PARITY_PASS`.\n\n"
        "Never invent that pass file or `RELEASE_OWNER_APPROVAL.md`. "
        "`cutoverAllowed` always false.\n\n"
        "Operator:\n\n"
        "```bash\n"
        "bash scripts/cloudpanel_run_module_function_parity_operator.sh\n"
        "```\n",
        encoding="utf-8",
    )
    print(f"wrote {readme}")
PY
