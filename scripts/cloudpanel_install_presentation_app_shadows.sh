#!/usr/bin/env bash
# Install Blazor presentation-parity preview + login-bridge exact-routes.
# Routes: /cp|erp|bos|storefront/{app,login} and /auth/login/admin.
# Never broad /cp|/erp|/bos|/storefront|/. Never removes PHP product chrome.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
EXAMPLE="$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf"

# shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

if [[ "${ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES\n' >&2
  exit 2
fi
[[ -f "$CONF" ]] || { printf 'ERROR: missing %s\n' "$CONF" >&2; exit 1; }
[[ -f "$EXAMPLE" ]] || { printf 'ERROR: missing %s\n' "$EXAMPLE" >&2; exit 1; }
# Presentation/login shadows: platform www only. Never tenant/industry by default.
ecomae_assert_nginx_shadow_target_allowed "$CONF" presentation

bak="/root/$(basename "$CONF").bak.presentation-apps.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"
printf 'Backup: %s\n' "$bak"

python3 - "$CONF" "$EXAMPLE" <<'PY'
from pathlib import Path
import re, sys
conf_path, example_path = Path(sys.argv[1]), Path(sys.argv[2])
text = conf_path.read_text(encoding="utf-8")
example = example_path.read_text(encoding="utf-8")
# Example conf is the allowlist. Accept every exact location except broad product chrome.
broad = {"/cp", "/erp", "/bos", "/storefront", "/"}
blocks=[]
for m in re.finditer(r"(?m)^(location = (/[^\s{]+)\s*\{.*?\n\})", example, flags=re.S):
    block_raw, route = m.group(1), m.group(2)
    if route in broad:
        raise SystemExit(f"ERROR: refusing broad path {route}")
    indented="\n".join(("  "+line if line.strip() else line) for line in block_raw.splitlines())
    blocks.append((route, indented.rstrip()+"\n"))
expected = 157  # 5 apps (+marketing) + 4 logins + auth/login/admin + orders×2 + dashboard-summary-app + users-app + groups-app + modules-app + pages-app + menus-app + tenants-app + currencies-app + storages-app + admin-sessions-app + api-clients-app + power-bi-app + mobile-apps-app + metabase-app + nl-reporting-app + marketing-broadcast-app + demo-tenants-app + parts-agent-chats-app + pos-overview-app + tax-toolkits-app + sms-whatsapp-app + crm-board-app + document-control-app + delivery-methods-app + crosses-app + hr-overview-app + production-overview-app + projects-overview-app + industry-packs-app + jewellery-retail-app + price-lists-app + auto-price-app + uae-tax-compliance-app + budgets-app + carriers-app + payment-gateways-app + workflows-app + purchase-requests-app + promotions-app + crm-opportunities-app + integrations-app + bank-reconciliation-app + stock-transfers-app + sales-quotations-app + workspace-favorites-app + fixed-assets-app + page-builder-app + product-catalogue-app + platform-governance-app + einvoice-documents-app + jewellery-repairs-app + crm-tickets-app + marketing-growth-app + config-items-app + audit-log-app + bos-tenants-app + bos-fleet-health-app + bos-fleet-readiness-app + bos-fleet-summary-app + sales-orders-app + purchase-orders-app + invoices-app + cash-accounts-app + cash-entries-app + coa-accounts-app + gl-journals-app + warehouses-app + suppliers-app + purchases-app + inventory-stock-app + accounts-summary-app + erp-dashboard-summary-app + search-app + cart-app + storefront-orders-app + garage-app + profile-app + account-summary-app
if len(blocks) != expected:
    raise SystemExit(f"ERROR: expected {expected} presentation/login routes, found {len(blocks)}")
inserted=[]; already=[]
for route, block in blocks:
    if re.search(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{", text):
        already.append(route); continue
    m=re.search(r"\n  location / \{", text)
    if not m: raise SystemExit("ERROR: insertion point missing")
    marker=m.start()+1
    text=text[:marker]+block+"\n"+text[marker:]
    inserted.append(route)
conf_path.write_text(text, encoding="utf-8")
print(f"ALREADY PRESENT: {len(already)}")
for r in already: print("  =", r)
print(f"INSERTED: {len(inserted)}")
for r in inserted: print("  +", r)
PY

nginx -t
systemctl reload nginx
printf 'Reloaded nginx. Preview + login URLs:\n'
printf '  https://www.ecomae.com/cp/app  https://www.ecomae.com/cp/login\n'
printf '  https://www.ecomae.com/erp/app https://www.ecomae.com/erp/login\n'
printf '  https://www.ecomae.com/bos/app https://www.ecomae.com/bos/login\n'
printf '  https://www.ecomae.com/storefront/app https://www.ecomae.com/storefront/login https://www.ecomae.com/marketing/app https://www.ecomae.com/storefront/checkout-app\n'
printf '  POST https://www.ecomae.com/auth/login/admin\n'
printf 'Product chrome /CP/ /ERP/ /BOS/ / remain PHP. Do NOT remove PHP.\n'
printf 'Set EcomAE__SecretSuccession (PHP secret_succession) in platform.env for login bridge writes.\n'
printf 'Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
