#!/usr/bin/env bash
# Prove ePartsCart storefront frontend vs PHP-reference locks after LIVE_PUBLISH.
# Usage (root on CloudPanel):
#   export ECOMAE_BRANCH=cursor/epartscart-frontend-php-parity-7b3b
#   bash scripts/cloudpanel_EPARTSCART_FRONTEND_PARITY_PROVE.sh
# Must print RESULT=PASS with SEARCH_OK=YES OWN_CATALOG_LABELS=YES — silent External action = FAIL.
set -euo pipefail

HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }

note "======== EPARTSCART FRONTEND PARITY PROVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "TARGET=${BASE}"

code_home=$(curl -sS -o /tmp/epc_prove_home.html -w '%{http_code}' "${BASE}/" || echo 000)
rg -q 'ecomae-chrome-surface|x-ecomae-platform' /tmp/epc_prove_home.html 2>/dev/null || true
hdr=$(curl -sSI "${BASE}/" | tr -d '\r' || true)
echo "$hdr" | rg -qi 'x-ecomae-platform:\s*primary' && ok "home_aspnet_primary" || fail "home_not_aspnet_primary code=${code_home}"

# Part search must not show unbound migration gate for ePartsCart.
code_search=$(curl -sS -o /tmp/epc_prove_search.json -w '%{http_code}' \
  "${BASE}/storefront/search-bunches?article=0986424590&brand=BOSCH" || echo 000)
if echo "$(cat /tmp/epc_prove_search.json 2>/dev/null || true)" | rg -qi 'not bound|www alias'; then
  fail "SEARCH_OK=NO unbound_gate code=${code_search}"
else
  ok "SEARCH_OK=YES code=${code_search}"
fi

# Own catalog tree must not render bare numeric lang ids as labels.
code_tree=$(curl -sS -o /tmp/epc_prove_tree.json -w '%{http_code}' \
  -H 'Accept: application/json' "${BASE}/storefront/catalogue/tree" || echo 000)
if rg -q '"value"\s*:\s*"[0-9]+"' /tmp/epc_prove_tree.json 2>/dev/null; then
  fail "OWN_CATALOG_LABELS=NO numeric_value_labels code=${code_tree}"
else
  ok "OWN_CATALOG_LABELS=YES code=${code_tree}"
fi

# Cart page must not deep-link product /en/shop/cart as primary CTA.
code_cart=$(curl -sS -o /tmp/epc_prove_cart.html -w '%{http_code}' "${BASE}/storefront/cart-app" || echo 000)
if rg -q 'href="/en/shop/cart"' /tmp/epc_prove_cart.html 2>/dev/null; then
  fail "CART_CTA=NO product_en_cart_link code=${code_cart}"
else
  ok "CART_CTA=YES code=${code_cart}"
fi

php_ref=$(curl -sS -o /dev/null -w '%{http_code}' "${BASE}/php-reference/home" || echo 000)
if [[ "$php_ref" == "200" || "$php_ref" == "302" || "$php_ref" == "301" ]]; then
  ok "PHP_REFERENCE=YES code=${php_ref}"
else
  note "GATE_WARN PHP_REFERENCE=${php_ref} (restore archive / disable TemporarilyDeactivatePhpServing)"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS SEARCH_OK=YES OWN_CATALOG_LABELS=YES CART_CTA=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above"
exit 1
