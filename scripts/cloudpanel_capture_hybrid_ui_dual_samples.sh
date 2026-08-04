#!/usr/bin/env bash
# Capture ASP.NET hybrid UI (*-app / CP orders) samples for dual-sample compare.
# Default: write/refresh contract stubs. With admin/customer cookies, probe www/loopback HTML.
# Never prints cookies. Never claims PHP cutover. Tenant hosts are out of scope.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${ECOMAE_HYBRID_UI_SAMPLES_DIR:-$ROOT/docs/migration/evidence/hybrid-ui-dual-samples}"
BASE="${ECOMAE_ASPNET_BASE_URL:-${ECOMAE_ASPNET_LOOPBACK:-http://127.0.0.1:5100}}"
PUBLIC="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
UA='Mozilla/5.0 (compatible; EcomAE-HybridUiCapture/1.0)'

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

ADMIN_COOKIE="${ECOMAE_ADMIN_COOKIE_HEADER:-}"
CUSTOMER_COOKIE="${ECOMAE_CUSTOMER_COOKIE_HEADER:-}"
OVERWRITE="${ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES:-0}"
mkdir -p "$OUT_DIR"

printf '== Hybrid UI dual-sample capture ==\n'
printf 'Base: %s\n' "$BASE"
printf 'Out:  %s\n' "$OUT_DIR"
if [[ -n "$ADMIN_COOKIE" ]]; then
  printf 'Admin cookie: present (value not printed)\n'
else
  printf 'Admin cookie: missing — admin targets stay stubs unless overwritten\n'
fi
if [[ -n "$CUSTOMER_COOKIE" ]]; then
  printf 'Customer cookie: present (value not printed)\n'
else
  printf 'Customer cookie: missing — storefront targets stay stubs unless overwritten\n'
fi

export ECOMAE_HYBRID_UI_SAMPLES_DIR="$OUT_DIR"
export ECOMAE_ASPNET_BASE_URL="$BASE"
export ECOMAE_PUBLIC_BASE_URL="$PUBLIC"
export ECOMAE_ADMIN_COOKIE_PRESENT="$([ -n "$ADMIN_COOKIE" ] && echo 1 || echo 0)"
export ECOMAE_CUSTOMER_COOKIE_PRESENT="$([ -n "$CUSTOMER_COOKIE" ] && echo 1 || echo 0)"
export ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES="$OVERWRITE"
export ECOMAE_ADMIN_COOKIE_HEADER="$ADMIN_COOKIE"
export ECOMAE_CUSTOMER_COOKIE_HEADER="$CUSTOMER_COOKIE"

python3 - <<'PY'
import json, os, subprocess, tempfile, datetime
from pathlib import Path

out_dir = Path(os.environ["ECOMAE_HYBRID_UI_SAMPLES_DIR"])
base = os.environ.get("ECOMAE_ASPNET_BASE_URL", "http://127.0.0.1:5100").rstrip("/")
public = os.environ.get("ECOMAE_PUBLIC_BASE_URL", "https://www.ecomae.com").rstrip("/")
admin_cookie = os.environ.get("ECOMAE_ADMIN_COOKIE_HEADER") or ""
customer_cookie = os.environ.get("ECOMAE_CUSTOMER_COOKIE_HEADER") or ""
overwrite = os.environ.get("ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES", "0") == "1"
ua = "Mozilla/5.0 (compatible; EcomAE-HybridUiCapture/1.0)"
now = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")

# stem, surface, appRoute, digestRoute, phpPath, blazorMarker, chromeShell, authKind
TARGETS = [
    ("cp-orders", "cp", "/cp/orders", "/cp/orders-digest", "/CP/shop/orders/orders", "CpOrdersApp", "PhpCpDesktopChrome", "admin"),
    ("cp-dashboard-summary", "cp", "/cp/dashboard-summary-app", "/cp/dashboard-summary", "/CP/", "CpDashboardSummaryApp", "PhpCpDesktopChrome", "admin"),
    ("cp-users", "cp", "/cp/users-app", "/cp/users", "/CP/control/users", "CpUsersApp", "PhpCpDesktopChrome", "admin"),
    ("cp-groups", "cp", "/cp/groups-app", "/cp/groups", "/CP/users/usergroups", "CpGroupsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-modules", "cp", "/cp/modules-app", "/cp/modules", "/CP/modules/modules_manager", "CpModulesApp", "PhpCpDesktopChrome", "admin"),
    ("cp-pages", "cp", "/cp/pages-app", "/cp/pages", "/CP/content/content_manager", "CpPagesApp", "PhpCpDesktopChrome", "admin"),
    ("cp-menus", "cp", "/cp/menus-app", "/cp/menus", "/CP/menu/menu_manager", "CpMenusApp", "PhpCpDesktopChrome", "admin"),
    ("cp-tenants", "cp", "/cp/tenants-app", "/cp/tenants", "/CP/control/portal/epc_tenant_control_center", "CpTenantsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-currencies", "cp", "/cp/currencies-app", "/cp/currencies", "/CP/shop/finance/nastrojka-kursov-valyut", "CpCurrenciesApp", "PhpCpDesktopChrome", "admin"),
    ("cp-storages", "cp", "/cp/storages-app", "/cp/storages", "/CP/shop/logistics/storages", "CpStoragesApp", "PhpCpDesktopChrome", "admin"),
    ("cp-admin-sessions", "cp", "/cp/admin-sessions-app", "/cp/admin-sessions", "/CP/control/users", "CpAdminSessionsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-api-clients", "cp", "/cp/api-clients-app", "/cp/api-clients", "/CP/control/portal/epc_api_clients_manage", "CpApiClientsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-power-bi", "cp", "/cp/power-bi-app", "/cp/power-bi", "/CP/control/portal/epc_power_bi", "CpPowerBiApp", "PhpCpDesktopChrome", "admin"),
    ("cp-mobile-apps", "cp", "/cp/mobile-apps-app", "/cp/mobile-apps", "/CP/control/portal/epc_mobile_apps", "CpMobileAppsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-metabase", "cp", "/cp/metabase-app", "/cp/metabase", "/CP/general_pages/epc_metabase_embed", "CpMetabaseApp", "PhpCpDesktopChrome", "admin"),
    ("cp-nl-reporting", "cp", "/cp/nl-reporting-app", "/cp/nl-reporting", "/CP/control/portal/epc_nl_reporting", "CpNlReportingApp", "PhpCpDesktopChrome", "admin"),
    ("cp-marketing-broadcast", "cp", "/cp/marketing-broadcast-app", "/cp/marketing-broadcast", "/CP/control/portal/epc_marketing_broadcast", "CpMarketingBroadcastApp", "PhpCpDesktopChrome", "admin"),
    ("cp-demo-tenants", "cp", "/cp/demo-tenants-app", "/cp/demo-tenants", "/CP/control/portal/epc_demo_tenants_manage", "CpDemoTenantsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-parts-agent-chats", "cp", "/cp/parts-agent-chats-app", "/cp/parts-agent-chats", "/CP/shop/parts_agent/parts_agent_chats", "CpPartsAgentChatsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-pos-overview", "cp", "/cp/pos-overview-app", "/cp/pos-overview", "/CP/control/shop/pos", "CpPosOverviewApp", "PhpCpDesktopChrome", "admin"),
    ("cp-tax-toolkits", "cp", "/cp/tax-toolkits-app", "/cp/tax-toolkits", "/CP/control/portal/epc_tax_toolkit_manage", "CpTaxToolkitsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-sms-whatsapp", "cp", "/cp/sms-whatsapp-app", "/cp/sms-whatsapp", "/CP/control/sms_turning", "CpSmsWhatsappApp", "PhpCpDesktopChrome", "admin"),
    ("cp-crm-board", "cp", "/cp/crm-board-app", "/cp/crm-board", "/CP/shop/crm/crm_main", "CpCrmBoardApp", "PhpCpDesktopChrome", "admin"),
    ("cp-document-control", "cp", "/cp/document-control-app", "/cp/document-control", "/CP/shop/document_control/document_control", "CpDocumentControlApp", "PhpCpDesktopChrome", "admin"),
    ("cp-delivery-methods", "cp", "/cp/delivery-methods-app", "/cp/delivery-methods", "/CP/shop/logistics/sposoby-polucheniya", "CpDeliveryMethodsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-crosses", "cp", "/cp/crosses-app", "/cp/crosses", "/CP/shop/crosses", "CpCrossesApp", "PhpCpDesktopChrome", "admin"),
    ("cp-hr-overview", "cp", "/cp/hr-overview-app", "/cp/hr-overview", "/CP/shop/finance/erp?area=people&tab=hr&epc_erp_shell=1", "CpHrOverviewApp", "PhpCpDesktopChrome", "admin"),
    ("cp-production-overview", "cp", "/cp/production-overview-app", "/cp/production-overview", "/CP/shop/finance/erp?area=production&tab=manufacturing&epc_erp_shell=1", "CpProductionOverviewApp", "PhpCpDesktopChrome", "admin"),
    ("cp-projects-overview", "cp", "/cp/projects-overview-app", "/cp/projects-overview", "/CP/shop/finance/erp?area=projects&tab=projects&epc_erp_shell=1", "CpProjectsOverviewApp", "PhpCpDesktopChrome", "admin"),
    ("cp-industry-packs", "cp", "/cp/industry-packs-app", "/cp/industry-packs", "/CP/control/portal/industry_settings", "CpIndustryPacksApp", "PhpCpDesktopChrome", "admin"),
    ("cp-jewellery-retail", "cp", "/cp/jewellery-retail-app", "/cp/jewellery-retail", "/CP/shop/finance/erp?area=retail&tab=retail_commerce&epc_erp_shell=1", "CpJewelleryRetailApp", "PhpCpDesktopChrome", "admin"),
    ("cp-price-lists", "cp", "/cp/price-lists-app", "/cp/price-lists", "/CP/shop/prices", "CpPriceListsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-auto-price", "cp", "/cp/auto-price-app", "/cp/auto-price", "/CP/control/portal/epc_auto_price_engine", "CpAutoPriceApp", "PhpCpDesktopChrome", "admin"),
    ("cp-uae-tax-compliance", "cp", "/cp/uae-tax-compliance-app", "/cp/uae-tax-compliance", "/CP/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1", "CpUaeTaxComplianceApp", "PhpCpDesktopChrome", "admin"),
    ("cp-budgets", "cp", "/cp/budgets-app", "/cp/budgets", "/CP/shop/finance/erp?area=budgeting&tab=budgeting&epc_erp_shell=1", "CpBudgetsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-carriers", "cp", "/cp/carriers-app", "/cp/carriers", "/CP/shop/logistics/carriers", "CpCarriersApp", "PhpCpDesktopChrome", "admin"),
    ("cp-payment-gateways", "cp", "/cp/payment-gateways-app", "/cp/payment-gateways", "/CP/shop/payments/payments", "CpPaymentGatewaysApp", "PhpCpDesktopChrome", "admin"),
    ("cp-workflows", "cp", "/cp/workflows-app", "/cp/workflows", "/CP/shop/finance/erp?area=setup&tab=workflow_automation&epc_erp_shell=1", "CpWorkflowsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-purchase-requests", "cp", "/cp/purchase-requests-app", "/cp/purchase-requests", "/CP/shop/finance/erp?area=purchasing&tab=purchase_requisitions&epc_erp_shell=1", "CpPurchaseRequestsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-promotions", "cp", "/cp/promotions-app", "/cp/promotions", "/CP/control/portal/epc_promotions_engine", "CpPromotionsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-crm-opportunities", "cp", "/cp/crm-opportunities-app", "/cp/crm-opportunities", "/CP/shop/finance/erp?area=sales&tab=opportunities&epc_erp_shell=1", "CpCrmOpportunitiesApp", "PhpCpDesktopChrome", "admin"),
    ("cp-integrations", "cp", "/cp/integrations-app", "/cp/integrations", "/CP/control/portal/epc_integrations_hub", "CpIntegrationsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-config-items", "cp", "/cp/config-items-app", "/cp/config-items", "/CP/control/config_edit", "CpConfigItemsApp", "PhpCpDesktopChrome", "admin"),
    ("erp-sales-orders", "erp", "/erp/sales-orders-app", "/erp/sales-orders", "/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders", "ErpSalesOrdersApp", "PhpErpDesktopChrome", "admin"),
    ("erp-purchase-orders", "erp", "/erp/purchase-orders-app", "/erp/purchase-orders", "/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_orders", "ErpPurchaseOrdersApp", "PhpErpDesktopChrome", "admin"),
    ("erp-invoices", "erp", "/erp/invoices-app", "/erp/invoices", "/ERP/?epc_erp_shell=1&area=sales&tab=invoices", "ErpInvoicesApp", "PhpErpDesktopChrome", "admin"),
    ("erp-cash-accounts", "erp", "/erp/cash-accounts-app", "/erp/cash-accounts", "/ERP/?epc_erp_shell=1&area=banking&tab=cash_bank", "ErpCashAccountsApp", "PhpErpDesktopChrome", "admin"),
    ("erp-cash-entries", "erp", "/erp/cash-entries-app", "/erp/cash-entries", "/ERP/?epc_erp_shell=1&area=banking&tab=cash_bank", "ErpCashEntriesApp", "PhpErpDesktopChrome", "admin"),
    ("erp-coa-accounts", "erp", "/erp/coa-accounts-app", "/erp/coa-accounts", "/ERP/?epc_erp_shell=1&area=finance&tab=coa", "ErpCoaAccountsApp", "PhpErpDesktopChrome", "admin"),
    ("erp-gl-journals", "erp", "/erp/gl-journals-app", "/erp/gl-journals", "/ERP/?epc_erp_shell=1&area=finance&tab=gl", "ErpGlJournalsApp", "PhpErpDesktopChrome", "admin"),
    ("erp-warehouses", "erp", "/erp/warehouses-app", "/erp/warehouses", "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=inventory", "ErpWarehousesApp", "PhpErpDesktopChrome", "admin"),
    ("erp-suppliers", "erp", "/erp/suppliers-app", "/erp/suppliers", "/ERP/?epc_erp_shell=1&area=ap&tab=payables", "ErpSuppliersApp", "PhpErpDesktopChrome", "admin"),
    ("erp-purchases", "erp", "/erp/purchases-app", "/erp/purchases", "/ERP/?epc_erp_shell=1&area=purchasing&tab=purchases", "ErpPurchasesApp", "PhpErpDesktopChrome", "admin"),
    ("erp-inventory-stock", "erp", "/erp/inventory-stock-app", "/erp/inventory-stock", "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=inventory", "ErpInventoryStockApp", "PhpErpDesktopChrome", "admin"),
    ("erp-bank-reconciliation", "erp", "/erp/bank-reconciliation-app", "/erp/bank-reconciliation", "/ERP/?epc_erp_shell=1&area=banking&tab=bank_recon", "ErpBankReconciliationApp", "PhpErpDesktopChrome", "admin"),
    ("erp-stock-transfers", "erp", "/erp/stock-transfers-app", "/erp/stock-transfers", "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=inv_groups", "ErpStockTransfersApp", "PhpErpDesktopChrome", "admin"),
    ("erp-sales-quotations", "erp", "/erp/sales-quotations-app", "/erp/sales-quotations", "/ERP/?epc_erp_shell=1&area=sales&tab=proposals", "ErpSalesQuotationsApp", "PhpErpDesktopChrome", "admin"),
    ("erp-workspace-favorites", "erp", "/erp/workspace-favorites-app", "/erp/workspace-favorites", "/ERP/?epc_erp_shell=1&area=overview&tab=dashboard", "ErpWorkspaceFavoritesApp", "PhpErpDesktopChrome", "admin"),
    ("erp-fixed-assets", "erp", "/erp/fixed-assets-app", "/erp/fixed-assets", "/ERP/?epc_erp_shell=1&area=fixed_assets&tab=fixed_assets", "ErpFixedAssetsApp", "PhpErpDesktopChrome", "admin"),
    ("cp-page-builder", "cp", "/cp/page-builder-app", "/cp/page-builder", "/CP/control/portal/epc_visual_page_editor", "CpPageBuilderApp", "PhpCpDesktopChrome", "admin"),
    ("cp-product-catalogue", "cp", "/cp/product-catalogue-app", "/cp/product-catalogue", "/CP/shop/catalogue/catalogue_editor", "CpProductCatalogueApp", "PhpCpDesktopChrome", "admin"),
    ("cp-platform-governance", "cp", "/cp/platform-governance-app", "/cp/platform-governance", "/CP/control/portal/epc_platform_governance", "CpPlatformGovernanceApp", "PhpCpDesktopChrome", "admin"),
    ("cp-einvoice-documents", "cp", "/cp/einvoice-documents-app", "/cp/einvoice-documents", "/ERP/?epc_erp_shell=1&area=tax&tab=einvoice", "CpEinvoiceDocumentsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-jewellery-repairs", "cp", "/cp/jewellery-repairs-app", "/cp/jewellery-repairs", "/ERP/?epc_erp_shell=1&area=service_mgmt&tab=jw_repairs", "CpJewelleryRepairsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-crm-tickets", "cp", "/cp/crm-tickets-app", "/cp/crm-tickets", "/ERP/?epc_erp_shell=1&area=sales&tab=crm", "CpCrmTicketsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-marketing-growth", "cp", "/cp/marketing-growth-app", "/cp/marketing-growth", "/CP/shop/marketing/marketing", "CpMarketingGrowthApp", "PhpCpDesktopChrome", "admin"),
    ("cp-soc2-compliance", "cp", "/cp/soc2-compliance-app", "/cp/soc2-compliance", "/CP/control/portal/epc_soc2_compliance", "CpSoc2ComplianceApp", "PhpCpDesktopChrome", "admin"),
    ("cp-cost-models", "cp", "/cp/cost-models-app", "/cp/cost-models", "/ERP/?epc_erp_shell=1&area=cost_mgmt&tab=cost_models", "CpCostModelsApp", "PhpCpDesktopChrome", "admin"),
    ("cp-fin-advanced", "cp", "/cp/fin-advanced-app", "/cp/fin-advanced", "/ERP/?epc_erp_shell=1&area=cost_acct&tab=fin_advanced", "CpFinAdvancedApp", "PhpCpDesktopChrome", "admin"),
    ("cp-blockchain-proofs", "cp", "/cp/blockchain-proofs-app", "/cp/blockchain-proofs", "/ERP/?epc_erp_shell=1&area=tax&tab=blockchain_proofs", "CpBlockchainProofsApp", "PhpCpDesktopChrome", "admin"),
    ("erp-accounts-summary", "erp", "/erp/accounts-summary-app", "/erp/accounts-summary", "/ERP/?epc_erp_shell=1&area=banking&tab=cash_bank", "ErpAccountsSummaryApp", "PhpErpDesktopChrome", "admin"),
    ("erp-dashboard-summary", "erp", "/erp/dashboard-summary-app", "/erp/dashboard-summary", "/ERP/?epc_erp_shell=1&area=overview", "ErpDashboardSummaryApp", "PhpErpDesktopChrome", "admin"),
    ("bos-audit-log", "bos", "/bos/audit-log-app", "/bos/audit-log", "/CP/control/portal/epc_boc_audit_log", "BosAuditLogApp", "PhpBosDesktopChrome", "admin"),
    ("bos-tenants", "bos", "/bos/tenants-app", "/bos/tenants", "/CP/control/portal/epc_tenant_control_center", "BosTenantsApp", "PhpBosDesktopChrome", "admin"),
    ("bos-fleet-health", "bos", "/bos/fleet-health-app", "/bos/fleet-health", "/CP/control/portal/epc_platform_health_checkup", "BosFleetHealthApp", "PhpBosDesktopChrome", "admin"),
    ("bos-fleet-readiness", "bos", "/bos/fleet-readiness-app", "/bos/fleet-readiness", "/CP/control/portal/epc_platform_health_checkup", "BosFleetReadinessApp", "PhpBosDesktopChrome", "admin"),
    ("bos-fleet-summary", "bos", "/bos/fleet-summary-app", "/bos/fleet-summary", "/BOS/", "BosFleetSummaryApp", "PhpBosDesktopChrome", "admin"),
    ("sf-search", "storefront", "/storefront/search-app", "/storefront/search", "https://epartscart.com/shop/part_search", "StorefrontSearchApp", "PhpStorefrontDesktopChrome", "customer"),
    ("sf-cart", "storefront", "/storefront/cart-app", "/storefront/cart", "https://epartscart.com/shop/cart", "StorefrontCartApp", "PhpStorefrontDesktopChrome", "customer"),
    ("sf-orders", "storefront", "/storefront/orders-app", "/storefront/orders", "https://epartscart.com/shop/orders", "StorefrontOrdersApp", "PhpStorefrontDesktopChrome", "customer"),
    ("sf-garage", "storefront", "/storefront/garage-app", "/storefront/garage", "https://epartscart.com/shop/part_search", "StorefrontGarageApp", "PhpStorefrontDesktopChrome", "customer"),
    ("sf-profile", "storefront", "/storefront/profile-app", "/storefront/profile", "https://epartscart.com/users/profile", "StorefrontProfileApp", "PhpStorefrontDesktopChrome", "customer"),
    ("sf-account-summary", "storefront", "/storefront/account-summary-app", "/storefront/account-summary", "https://epartscart.com/users/", "StorefrontAccountSummaryApp", "PhpStorefrontDesktopChrome", "customer"),
]

def base_doc(stem, surface, app_route, digest_route, php_path, marker, chrome, auth):
    return {
        "role": "aspnet-hybrid-ui-sample",
        "stem": stem,
        "surface": surface,
        "appRoute": app_route,
        "digestRoute": digest_route or None,
        "phpAuthoritativePath": php_path,
        "blazorMarker": marker,
        "chromeShell": chrome,
        "authKind": auth,
        "httpStatus": None,
        "markersFound": [],
        "phpDeeplinkFound": False,
        "phpAuthoritative": True,
        "wwwPreviewOnly": True,
        "tenantChromePhp": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "capturedAt": now,
        "baseUrl": base,
        "publicBaseUrl": public,
        "note": "Contract stub. Re-run on CloudPanel with cookies + ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES=1.",
    }

def write_stub(target):
    stem, surface, app_route, digest_route, php_path, marker, chrome, auth = target
    path = out_dir / f"aspnet-{stem}-hybrid-ui.json"
    if path.exists() and not overwrite:
        print(f"keep existing {path.name}")
        return
    path.write_text(json.dumps(base_doc(*target), indent=2) + "\n", encoding="utf-8")
    print(f"wrote stub {path.name}")

def write_php_inventory():
    path = out_dir / "php-hybrid-authoritative-inventory.json"
    if path.exists() and not overwrite:
        print(f"keep existing {path.name}")
        return
    doc = {
        "role": "php-hybrid-authoritative-inventory",
        "phpAuthoritative": True,
        "wwwPreviewOnly": True,
        "tenantChromePhp": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "capturedAt": now,
        "surfaces": {
            "cp": "/CP/",
            "erp": "/ERP/",
            "bos": "/BOS/",
            "storefront": "https://epartscart.com/",
        },
        "hybridUiTargets": [
            {
                "stem": t[0],
                "appRoute": t[2],
                "digestRoute": t[3] or None,
                "phpAuthoritativePath": t[4],
            }
            for t in TARGETS
        ],
        "note": "ASP.NET *-app routes are www exact-route previews only. Live product chrome and writes remain PHP.",
    }
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {path.name}")

def curl_html(route: str, cookie: str) -> tuple[int, str]:
    body = tempfile.NamedTemporaryFile(delete=False)
    body.close()
    headers = ["-A", ua, "-H", "Accept: text/html"]
    if cookie:
        headers += ["-H", f"Cookie: {cookie}"]
    status = 0
    html = ""
    for host in (base, public):
        proc = subprocess.run(
            ["curl", "-sS", "-m", "45", "-L", "-o", body.name, "-w", "%{http_code}", *headers, f"{host}{route}"],
            capture_output=True,
            text=True,
        )
        code_s = (proc.stdout or "").strip() or "000"
        try:
            status = int(code_s)
        except ValueError:
            status = 0
        html = Path(body.name).read_text(encoding="utf-8", errors="replace")
        if status in (200, 302, 401, 403):
            return status, html
    return status, html

def capture_live(target):
    stem, surface, app_route, digest_route, php_path, marker, chrome, auth = target
    path = out_dir / f"aspnet-{stem}-hybrid-ui.json"
    cookie = admin_cookie if auth == "admin" else customer_cookie
    if not cookie:
        write_stub(target)
        return
    status, html = curl_html(app_route, cookie)
    markers = []
    if marker and marker in html:
        markers.append(marker)
    if chrome and chrome in html:
        markers.append(chrome)
    php_hint = php_path.split("?")[0]
    if php_hint.startswith("https://"):
        php_needle = php_hint.replace("https://epartscart.com", "") or php_hint
    else:
        php_needle = php_hint
    php_found = bool(php_needle and php_needle in html)
    doc = base_doc(*target)
    doc["httpStatus"] = status
    doc["markersFound"] = markers
    doc["phpDeeplinkFound"] = php_found
    doc["note"] = (
        "Live HTML capture on www/loopback preview only. "
        "Does not authorize tenant cutover or PHP removal."
    )
    if status != 200:
        doc["note"] += f" WARN: httpStatus={status}."
    if marker not in markers:
        doc["note"] += f" WARN: blazor marker {marker!r} not found."
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"captured {path.name} http={status} markers={markers}")

write_php_inventory()
for target in TARGETS:
    stem = target[0]
    auth = target[7]
    cookie = admin_cookie if auth == "admin" else customer_cookie
    path = out_dir / f"aspnet-{stem}-hybrid-ui.json"
    if path.exists() and not overwrite:
        print(f"keep existing {path.name}")
        continue
    if cookie:
        capture_live(target)
    else:
        write_stub(target)

print("Done. Compare with: python3 scripts/compare_hybrid_ui_dual_samples.py")
PY
