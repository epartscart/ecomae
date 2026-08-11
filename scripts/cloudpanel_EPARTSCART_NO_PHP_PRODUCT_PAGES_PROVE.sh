#!/usr/bin/env bash
# Prove ePartsCart product URLs are ASP.NET (no PHP nero page bodies).
# PHP HTML allowed only under /php-reference/* (may be paused 503).
set -euo pipefail

HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }

note "======== EPARTSCART NO PHP PRODUCT PAGES PROVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo local)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"

probe_product() {
  local path="$1"
  local label="$2"
  local hdr body code
  hdr=$(curl -sSI --max-time 25 "${BASE}${path}" | tr -d '\r' || true)
  code=$(curl -sS -o "/tmp/epc_nophp_${label}.html" -w '%{http_code}' --max-time 40 "${BASE}${path}" || echo 000)
  note "PROBE ${label} http=${code} path=${path}"
  if echo "$hdr" | rg -qi 'x-ecomae-platform:\s*primary'; then
    ok "${label}_PRIMARY=YES"
  else
    fail "${label}_PRIMARY=NO"
  fi
  if rg -qi '<base href="/templates/nero/"' "/tmp/epc_nophp_${label}.html" 2>/dev/null; then
    fail "${label}_PHP_NERO=YES"
  else
    ok "${label}_PHP_NERO=NO"
  fi
  if rg -qi 'ajax_getProductsOfBunch\.php' "/tmp/epc_nophp_${label}.html" 2>/dev/null \
     && ! rg -qi 'ecomae-chrome-surface' "/tmp/epc_nophp_${label}.html" 2>/dev/null; then
    fail "${label}_PHP_AJAX_BODY=YES"
  fi
}

probe_product "/" "home"
probe_product "/en/parts/TOYOTA/1310154101" "parts_chpu"
probe_product "/en/shop/part_search?article=1310154101&brend=TOYOTA" "part_search"
probe_product "/en/shop/cart" "cart"
probe_product "/en/users/login" "login"
probe_product "/storefront/search-app?article=1310154101&brand=TOYOTA" "search_app"

# Archive may be paused (503) — that is PASS for "PHP product pages stopped".
ref_code=$(curl -sS -o /tmp/epc_nophp_ref.txt -w '%{http_code}' --max-time 20 "${BASE}/php-reference/home" || echo 000)
note "PHP_REFERENCE_HOME=${ref_code}"
if [[ "$ref_code" == "503" ]]; then
  ok "PHP_REFERENCE_PAUSED=YES (activate only when asked)"
elif [[ "$ref_code" == "200" || "$ref_code" == "302" || "$ref_code" == "301" ]]; then
  note "GATE_WARN PHP_REFERENCE_LIVE=${ref_code} (archive open — OK if you restored on purpose)"
else
  note "GATE_WARN PHP_REFERENCE=${ref_code}"
fi

# Digests must not unbound on ePartsCart
api=$(curl -sS --max-time 25 "${BASE}/storefront/search?article=1310154101&brand=TOYOTA&limit=5" || true)
if echo "$api" | rg -qi 'not bound|www alias'; then
  fail "SEARCH_UNBOUND=YES"
else
  ok "SEARCH_UNBOUND=NO"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS PHP_PRODUCT_PAGES=STOPPED ASPNET_PRIMARY=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above"
exit 1
