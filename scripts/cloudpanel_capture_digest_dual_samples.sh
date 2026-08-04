#!/usr/bin/env bash
# Capture authenticated ASP.NET digest JSON samples for dual-sample parity.
# Writes docs/migration/evidence/surface-parity/samples/aspnet-*.json
#
# When ECOMAE_PHP_DIGEST_BASE_URL is unset (typical after exact-route shadows),
# compare uses migration/ contract goldens as the left baseline (contract-only)
# against live aspnet-*.json. Do not invent live PHP JSON after shadows land.
#
# Requires admin cookie (CP/ERP/BOS digests) and customer cookie (storefront-*):
#   set -a; source /etc/ecomae-aspnet/platform.env; set +a
#   # ECOMAE_ADMIN_COOKIE_HEADER=admin_session=...; admin_u_id=...
#   # ECOMAE_CUSTOMER_COOKIE_HEADER=session=...; u_id=...
#   bash scripts/cloudpanel_capture_digest_dual_samples.sh
#
# Then compare (auto unless ECOMAE_DIGEST_DUAL_COMPARE=0):
#   python3 scripts/compare_digest_dual_samples.py
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${ECOMAE_DIGEST_SAMPLES_DIR:-$ROOT/docs/migration/evidence/surface-parity/samples}"
ASPNET_BASE="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"
PHP_BASE="${ECOMAE_PHP_DIGEST_BASE_URL:-}"
ADMIN_COOKIE="${ECOMAE_ADMIN_COOKIE_HEADER:-}"
CUSTOMER_COOKIE="${ECOMAE_CUSTOMER_COOKIE_HEADER:-}"
RUN_COMPARE="${ECOMAE_DIGEST_DUAL_COMPARE:-1}"

if [[ -z "$ADMIN_COOKIE" ]]; then
  printf 'ERROR: set ECOMAE_ADMIN_COOKIE_HEADER (admin session cookie)\n' >&2
  printf 'Hint: ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n' >&2
  printf '        bash scripts/cloudpanel_issue_smoke_credentials.sh\n' >&2
  exit 2
fi
if [[ -z "$CUSTOMER_COOKIE" ]]; then
  printf 'WARN: ECOMAE_CUSTOMER_COOKIE_HEADER unset — storefront-* digests will FAIL (need session=...; u_id=...)\n' >&2
fi

mkdir -p "$OUT"

# stem -> path (matches compare_digest_dual_samples.py contracts; full surface+storefront allowlist)
declare -A ROUTES=(
  [cp-dashboard-summary]="/cp/dashboard-summary"
  [cp-tenants]="/cp/tenants?limit=5"
  [cp-users]="/cp/users?limit=5"
  [cp-groups]="/cp/groups?limit=5"
  [cp-modules]="/cp/modules?limit=5"
  [cp-menus]="/cp/menus?limit=5"
  [cp-pages]="/cp/pages?limit=5"
  [cp-currencies]="/cp/currencies?limit=5"
  [cp-api-clients]="/cp/api-clients?limit=5"
  [cp-power-bi]="/cp/power-bi?limit=5"
  [cp-mobile-apps]="/cp/mobile-apps"
  [cp-metabase]="/cp/metabase?limit=5"
  [cp-nl-reporting]="/cp/nl-reporting?limit=5"
  [cp-marketing-broadcast]="/cp/marketing-broadcast?limit=5"
  [cp-demo-tenants]="/cp/demo-tenants?limit=5"
  [cp-parts-agent-chats]="/cp/parts-agent-chats?limit=5"
  [cp-pos-overview]="/cp/pos-overview?limit=5"
  [cp-tax-toolkits]="/cp/tax-toolkits?limit=5"
  [cp-sms-whatsapp]="/cp/sms-whatsapp?limit=5"
  [cp-crm-board]="/cp/crm-board?limit=5"
  [cp-document-control]="/cp/document-control?limit=5"
  [cp-delivery-methods]="/cp/delivery-methods?limit=5"
  [cp-crosses]="/cp/crosses?limit=5"
  [cp-hr-overview]="/cp/hr-overview?limit=5"
  [cp-production-overview]="/cp/production-overview?limit=5"
  [cp-projects-overview]="/cp/projects-overview?limit=5"
  [cp-industry-packs]="/cp/industry-packs?limit=5"
  [cp-jewellery-retail]="/cp/jewellery-retail?limit=5"
  [cp-price-lists]="/cp/price-lists?limit=5"
  [cp-auto-price]="/cp/auto-price?limit=5"
  [cp-uae-tax-compliance]="/cp/uae-tax-compliance?limit=5"
  [cp-budgets]="/cp/budgets?limit=5"
  [cp-carriers]="/cp/carriers?limit=5"
  [cp-payment-gateways]="/cp/payment-gateways?limit=5"
  [cp-workflows]="/cp/workflows?limit=5"
  [cp-purchase-requests]="/cp/purchase-requests?limit=5"
  [cp-promotions]="/cp/promotions?limit=5"
  [cp-crm-opportunities]="/cp/crm-opportunities?limit=5"
  [cp-integrations]="/cp/integrations?limit=5"
  [erp-bank-reconciliation]="/erp/bank-reconciliation?limit=5"
  [erp-stock-transfers]="/erp/stock-transfers?limit=5"
  [erp-sales-quotations]="/erp/sales-quotations?limit=5"
  [erp-workspace-favorites]="/erp/workspace-favorites?limit=5"
  [erp-fixed-assets]="/erp/fixed-assets?limit=5"
  [erp-process-flow-tasks]="/erp/process-flow-tasks?limit=5"
  [cp-page-builder]="/cp/page-builder?limit=5"
  [cp-product-catalogue]="/cp/product-catalogue?limit=5"
  [cp-platform-governance]="/cp/platform-governance?limit=5"
  [cp-marketing-growth]="/cp/marketing-growth?limit=5"
  [cp-crm-tickets]="/cp/crm-tickets?limit=5"
  [cp-jewellery-repairs]="/cp/jewellery-repairs?limit=5"
  [cp-soc2-compliance]="/cp/soc2-compliance?limit=5"
  [cp-cost-models]="/cp/cost-models?limit=5"
  [cp-fin-advanced]="/cp/fin-advanced?limit=5"
  [cp-blockchain-proofs]="/cp/blockchain-proofs?limit=5"
  [cp-landed-cost]="/cp/landed-cost?limit=5"
  [cp-warehouse-wms]="/cp/warehouse-wms?limit=5"
  [cp-ai-service]="/cp/ai-service?limit=5"
  [cp-returns-rma]="/cp/returns-rma?limit=5"
  [cp-isolation-audit]="/cp/isolation-audit?limit=5"
  [cp-aml-compliance]="/cp/aml-compliance?limit=5"
  [cp-jewellery-masters]="/cp/jewellery-masters?limit=5"
  [cp-consolidations]="/cp/consolidations?limit=5"
  [cp-crm-activities]="/cp/crm-activities?limit=5"
  [cp-auth-mfa]="/cp/auth-mfa?limit=5"
  [cp-electronic-reporting]="/cp/electronic-reporting?limit=5"
  [cp-collections-dunning]="/cp/collections-dunning?limit=5"
  [cp-marketplace-channels]="/cp/marketplace-channels?limit=5"
  [cp-demand-intelligence]="/cp/demand-intelligence?limit=5"
  [cp-credit-limits]="/cp/credit-limits?limit=5"
  [cp-insurance-compliance]="/cp/insurance-compliance?limit=5"
  [cp-audit-trail]="/cp/audit-trail?limit=5"
  [cp-doc-expiry]="/cp/doc-expiry?limit=5"
  [cp-tenant-config]="/cp/tenant-config?limit=5"
  [cp-jewellery-stock-verification]="/cp/jewellery-stock-verification?limit=5"
  [cp-tax-external-reporting]="/cp/tax-external-reporting?limit=5"
  [cp-po-approvals]="/cp/po-approvals?limit=5"
  [cp-finance-close]="/cp/finance-close?limit=5"
  [cp-jewellery-fixing]="/cp/jewellery-fixing?limit=5"
  [cp-web-tracker]="/cp/web-tracker?limit=5"
  [cp-abandoned-carts]="/cp/abandoned-carts?limit=5"
  [cp-quote-requests]="/cp/quote-requests?limit=5"
  [cp-platform-communication]="/cp/platform-communication?limit=5"
  [cp-info-blocks]="/cp/info-blocks?limit=5"
  [cp-free-tools]="/cp/free-tools?limit=5"
  [cp-config-sandbox]="/cp/config-sandbox?limit=5"
  [cp-marketplace-apps]="/cp/marketplace-apps?limit=5"
  [cp-notifications]="/cp/notifications?limit=5"
  [cp-portal-settings]="/cp/portal-settings?limit=5"
  [cp-data-migrations]="/cp/data-migrations?limit=5"
  [cp-geo-regions]="/cp/geo-regions?limit=5"
  [cp-product-filters]="/cp/product-filters?limit=5"
  [cp-search-tabs]="/cp/search-tabs?limit=5"
  [cp-system-requests]="/cp/system-requests?limit=5"
  [cp-additional-texts]="/cp/additional-texts?limit=5"
  [cp-slider-banners]="/cp/slider-banners?limit=5"
  [cp-structure-dumps]="/cp/structure-dumps?limit=5"
  [cp-communications-test]="/cp/communications-test?limit=5"
  [cp-languages]="/cp/languages?limit=5"
  [cp-plugins-manager]="/cp/plugins-manager?limit=5"
  [cp-templates-manager]="/cp/templates-manager?limit=5"
  [cp-design-tokens]="/cp/design-tokens?limit=5"
  [cp-sitemap]="/cp/sitemap?limit=5"
  [cp-failover-status]="/cp/failover-status?limit=5"
  [cp-ops-guides]="/cp/ops-guides?limit=5"
  [cp-file-manager]="/cp/file-manager?limit=5"
  [cp-server-ip]="/cp/server-ip?limit=5"
  [cp-debug-console]="/cp/debug-console?limit=5"
  [cp-einvoice-documents]="/cp/einvoice-documents?limit=5"
  [cp-config-items]="/cp/config-items?limit=5"
  [cp-admin-sessions]="/cp/admin-sessions?limit=5"
  [cp-storages]="/cp/storages?limit=5"
  [cp-orders-digest]="/cp/orders-digest?limit=5"
  [erp-dashboard-summary]="/erp/dashboard-summary"
  [erp-accounts-summary]="/erp/accounts-summary"
  [erp-suppliers]="/erp/suppliers?limit=5"
  [erp-purchases]="/erp/purchases?limit=5"
  [erp-cash-accounts]="/erp/cash-accounts?limit=5"
  [erp-cash-entries]="/erp/cash-entries?limit=5"
  [erp-coa-accounts]="/erp/coa-accounts?limit=5"
  [erp-warehouses]="/erp/warehouses?limit=5"
  [erp-sales-orders]="/erp/sales-orders?limit=5"
  [erp-purchase-orders]="/erp/purchase-orders?limit=5"
  [erp-inventory-stock]="/erp/inventory-stock"
  [erp-invoices]="/erp/invoices?limit=5"
  [erp-gl-journals]="/erp/gl-journals?limit=5"
  [bos-fleet-summary]="/bos/fleet-summary"
  [bos-tenants]="/bos/tenants?limit=5"
  [bos-fleet-health]="/bos/fleet-health"
  [bos-fleet-readiness]="/bos/fleet-readiness"
  [bos-audit-log]="/bos/audit-log?limit=5"
  [storefront-account-summary]="/storefront/account-summary"
  [storefront-orders]="/storefront/orders?limit=5"
  [storefront-garage]="/storefront/garage?limit=5"
  [storefront-profile]="/storefront/profile"
  [storefront-search]="/storefront/search?article=0986424590&limit=5"
  [storefront-cart]="/storefront/cart?limit=5"
  [storefront-checkout]="/storefront/checkout?limit=5"
)

cookie_for_stem() {
  local stem="$1"
  if [[ "$stem" == storefront-* ]]; then
    printf '%s' "$CUSTOMER_COOKIE"
  else
    printf '%s' "$ADMIN_COOKIE"
  fi
}

capture() {
  local label="$1" base="$2" path="$3" out="$4" cookie="$5"
  local code
  if [[ -z "$cookie" ]]; then
    printf 'FAIL %s %s missing cookie (storefront needs ECOMAE_CUSTOMER_COOKIE_HEADER)\n' "$label" "$path" >&2
    return 1
  fi
  code="$(curl -sS -m 30 \
    -H "Cookie: $cookie" \
    -H 'Accept: application/json' \
    -A 'Mozilla/5.0 EcomAE-digest-dual-sample' \
    -o "$out" -w '%{http_code}' \
    "${base}${path}" || echo 000)"
  if [[ "$code" != "200" ]]; then
    printf 'FAIL %s %s HTTP %s\n' "$label" "$path" "$code" >&2
    head -c 160 "$out" >&2 || true
    printf '\n' >&2
    return 1
  fi
  if grep -qi '<!DOCTYPE\|<html' "$out" 2>/dev/null; then
    printf 'FAIL %s %s returned HTML\n' "$label" "$path" >&2
    return 1
  fi
  python3 - "$out" <<'PY'
import json, sys
from pathlib import Path
p = Path(sys.argv[1])
doc = json.loads(p.read_text(encoding="utf-8"))
p.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
PY
  printf 'OK %s %s -> %s\n' "$label" "$path" "$out"
}

ok=0
fail=0
for stem in "${!ROUTES[@]}"; do
  path="${ROUTES[$stem]}"
  cookie="$(cookie_for_stem "$stem")"
  if capture aspnet "$ASPNET_BASE" "$path" "$OUT/aspnet-${stem}.json" "$cookie"; then
    ok=$((ok + 1))
  else
    fail=$((fail + 1))
  fi
  if [[ -n "$PHP_BASE" ]]; then
    if capture php "$PHP_BASE" "$path" "$OUT/php-${stem}.json" "$cookie"; then
      ok=$((ok + 1))
    else
      printf 'WARN: PHP sample missing for %s\n' "$stem" >&2
    fi
  fi
done

# Remove previously seeded migration php-* baselines so compare uses migration/ + contract-only.
removed=0
for seeded in "$OUT"/php-*.json; do
  [[ -f "$seeded" ]] || continue
  if python3 - "$seeded" <<'PY'
import json, sys
from pathlib import Path
doc = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
sys.exit(0 if isinstance(doc, dict) and doc.get("dualSampleBaseline") == "migration-contract-golden" else 1)
PY
  then
    rm -f "$seeded"
    removed=$((removed + 1))
    printf 'CLEAN seeded baseline %s (compare will use migration/)\n' "$(basename "$seeded")"
  fi
done

printf '\nCaptured ASP.NET samples under %s (ok=%s fails=%s cleaned_seeded=%s)\n' "$OUT" "$ok" "$fail" "$removed"
printf 'Compare uses migration/ goldens as left baseline (contract-only) when PHP JSON is not public.\n'
printf 'Next: python3 scripts/compare_digest_dual_samples.py --samples-dir %s\n' "$OUT"
if [[ "$fail" -gt 0 ]]; then
  exit 1
fi

if [[ "$RUN_COMPARE" == "1" ]]; then
  printf '\n-- Running compare_digest_dual_samples.py --\n'
  python3 "$ROOT/scripts/compare_digest_dual_samples.py" --samples-dir "$OUT"
fi
