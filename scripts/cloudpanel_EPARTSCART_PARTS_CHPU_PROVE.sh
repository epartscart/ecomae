#!/usr/bin/env bash
# Prove ePartsCart part search: CHPU + search-app are the same ASP.NET result (PHP reference parity).
# Usage:
#   bash scripts/cloudpanel_EPARTSCART_PARTS_CHPU_PROVE.sh
# Must print RESULT=PASS — silent External action without paste-back = FAIL.
set -euo pipefail

HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
PARTS_PATH="${ECOMAE_PARTS_PROBE:-/en/parts/TOYOTA/1310154101}"
SEARCH_APP="${ECOMAE_SEARCH_APP_PROBE:-/storefront/search-app?article=1310154101&brand=TOYOTA}"
SEARCH_API="${ECOMAE_SEARCH_API_PROBE:-/storefront/search?article=1310154101&brand=TOYOTA&limit=20}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }

note "======== EPARTSCART PARTS CHPU / SEARCH-APP SAME ASP.NET PROVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo local)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "CHPU=${BASE}${PARTS_PATH}"
note "SEARCH_APP=${BASE}${SEARCH_APP}"

curl -sSI --max-time 30 "${BASE}${PARTS_PATH}" | tr -d '\r' > /tmp/epc_parts_hdr.txt || true
curl -sS -o /tmp/epc_parts_chpu.html -w '%{http_code}' --max-time 45 "${BASE}${PARTS_PATH}" > /tmp/epc_parts_code.txt || echo 000 > /tmp/epc_parts_code.txt
code=$(cat /tmp/epc_parts_code.txt)
note "PARTS_HTTP=${code}"

rg -qi 'x-ecomae-platform:\s*primary' /tmp/epc_parts_hdr.txt && ok "PARTS_HEADER_PRIMARY=YES" || fail "PARTS_HEADER_PRIMARY=NO (still PHP edge?)"
if rg -qi '<base href="/templates/nero/"' /tmp/epc_parts_chpu.html 2>/dev/null; then
  fail "PARTS_PHP_NERO=YES"
else
  ok "PARTS_PHP_NERO=NO"
fi
if rg -qi 'ajax_getProductsOfBunch\.php' /tmp/epc_parts_chpu.html 2>/dev/null; then
  fail "PARTS_PHP_AJAX_BODY=YES"
else
  ok "PARTS_PHP_AJAX_BODY=NO"
fi
rg -qi 'ecomae-chrome-surface' /tmp/epc_parts_chpu.html && ok "PARTS_CHROME=YES" || fail "PARTS_CHROME=NO"
rg -qi 'Pricing and availability for TOYOTA' /tmp/epc_parts_chpu.html && ok "PARTS_TITLE_PHP_PARITY=YES" || fail "PARTS_TITLE_PHP_PARITY=NO"
rg -qi 'all_table_products' /tmp/epc_parts_chpu.html && ok "PARTS_TABLE=YES" || fail "PARTS_TABLE=NO"

# search-app must land on same ASP.NET digest (may 302 → CHPU)
curl -sSIL --max-time 30 "${BASE}${SEARCH_APP}" | tr -d '\r' > /tmp/epc_search_hdr.txt || true
final_url=$(curl -sS -o /tmp/epc_search_app.html -w '%{url_effective}' --max-time 45 -L "${BASE}${SEARCH_APP}" || true)
note "SEARCH_APP_FINAL=${final_url}"
rg -qi 'x-ecomae-platform:\s*primary' /tmp/epc_search_hdr.txt && ok "SEARCH_APP_PRIMARY=YES" || fail "SEARCH_APP_PRIMARY=NO"
rg -qi 'ecomae-chrome-surface' /tmp/epc_search_app.html && ok "SEARCH_APP_CHROME=YES" || fail "SEARCH_APP_CHROME=NO"
rg -qi '<base href="/templates/nero/"' /tmp/epc_search_app.html && fail "SEARCH_APP_PHP_NERO=YES" || ok "SEARCH_APP_PHP_NERO=NO"

# Digests must not show unbound migration on ePartsCart (docpart Model C).
curl -sS -o /tmp/epc_search_api.json -w '%{http_code}' --max-time 30 "${BASE}${SEARCH_API}" > /tmp/epc_search_api_code.txt || echo 000 > /tmp/epc_search_api_code.txt
api_code=$(cat /tmp/epc_search_api_code.txt)
note "SEARCH_API_HTTP=${api_code}"
if rg -qi 'not bound|www alias' /tmp/epc_search_api.json 2>/dev/null; then
  fail "SEARCH_UNBOUND=YES (docpart bind / publish missing)"
else
  ok "SEARCH_UNBOUND=NO"
fi
if rg -qi '"source"\s*:\s*"(database|php-chpu)"' /tmp/epc_search_api.json 2>/dev/null; then
  ok "SEARCH_SOURCE_OK=YES"
else
  note "GATE_WARN SEARCH_SOURCE=$(rg -o '\"source\"\s*:\s*\"[^\"]+\"' /tmp/epc_search_api.json | head -1 || true)"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS PARTS_CHPU_ASPNET=YES SEARCH_APP_SAME=YES UNBOUND=NO"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above"
exit 1
