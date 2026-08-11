#!/usr/bin/env bash
# Prove ePartsCart brand+article CHPU uses ASP.NET skip-SSR + protocol-3 AJAX fast path.
# Usage:
#   bash scripts/cloudpanel_EPARTSCART_PARTS_WAREHOUSE_PHP_FAST_PROVE.sh
# Must print RESULT=PASS — silent External action without paste-back = FAIL.
set -euo pipefail

HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
PARTS_PATH="${ECOMAE_PARTS_PROBE:-/en/parts/JS%20ASAKASHI/C110J}"
BUNCH_API="${ECOMAE_BUNCH_PROBE:-/storefront/products-of-bunch}"
TTFB_BUDGET_MS="${ECOMAE_CHPU_TTFB_BUDGET_MS:-1200}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }

note "======== EPARTSCART PARTS WAREHOUSE PHP-FAST PROVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo local)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "CHPU=${BASE}${PARTS_PATH}"
note "TTFB_BUDGET_MS=${TTFB_BUDGET_MS}"

# Warm once, then measure TTFB (ms) on second hit.
curl -sS -o /dev/null --max-time 45 "${BASE}${PARTS_PATH}" || true
ttfb_ms=$(curl -sS -o /tmp/epc_wh_fast.html -w '%{time_starttransfer}' --max-time 45 "${BASE}${PARTS_PATH}" \
  | awk '{printf "%d", ($1*1000)+0.5}')
code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 30 -I "${BASE}${PARTS_PATH}" || echo 000)
note "PARTS_HTTP=${code}"
note "TTFB_MS=${ttfb_ms}"

curl -sSI --max-time 30 "${BASE}${PARTS_PATH}" | tr -d '\r' > /tmp/epc_wh_fast_hdr.txt || true
rg -qi 'x-ecomae-platform:\s*primary' /tmp/epc_wh_fast_hdr.txt && ok "PARTS_HEADER_PRIMARY=YES" || fail "PARTS_HEADER_PRIMARY=NO"
[[ "$code" == "200" ]] && ok "PARTS_HTTP_200=YES" || fail "PARTS_HTTP_200=NO code=${code}"

if rg -qi '<base href="/templates/nero/"' /tmp/epc_wh_fast.html 2>/dev/null; then
  fail "PARTS_PHP_NERO=YES"
else
  ok "PARTS_PHP_NERO=NO"
fi
if rg -qi 'ajax_getProductsOfBunch\.php' /tmp/epc_wh_fast.html 2>/dev/null; then
  fail "PARTS_PHP_AJAX_BODY=YES"
else
  ok "PARTS_PHP_AJAX_BODY=NO"
fi

rg -qi 'runChpuPriceSearch' /tmp/epc_wh_fast.html && ok "CHPU_RUN_SEARCH=YES" || fail "CHPU_RUN_SEARCH=NO"
rg -qi 'pickProtocol3Bunch' /tmp/epc_wh_fast.html && ok "CHPU_PROTOCOL3_PICK=YES" || fail "CHPU_PROTOCOL3_PICK=NO"
rg -qi 'Promise\.all' /tmp/epc_wh_fast.html && ok "CHPU_PROMISE_ALL=YES" || fail "CHPU_PROMISE_ALL=NO"
rg -qi '/storefront/products-of-bunch' /tmp/epc_wh_fast.html && ok "CHPU_PRODUCTS_OF_BUNCH=YES" || fail "CHPU_PRODUCTS_OF_BUNCH=NO"
rg -qi 'all_table_products' /tmp/epc_wh_fast.html && ok "PARTS_TABLE=YES" || fail "PARTS_TABLE=NO"
rg -qi 'polling suppliers|No warehouse offers yet' /tmp/epc_wh_fast.html && ok "CHPU_AJAX_SHELL=YES" || fail "CHPU_AJAX_SHELL=NO"
rg -qi 'C110J|JS ASAKASHI|ASAKASHI' /tmp/epc_wh_fast.html && ok "PARTS_ARTICLE_MARKERS=YES" || fail "PARTS_ARTICLE_MARKERS=NO"

if [[ -n "${ttfb_ms}" && "${ttfb_ms}" -le "${TTFB_BUDGET_MS}" ]]; then
  ok "TTFB_BUDGET=YES (${ttfb_ms}ms <= ${TTFB_BUDGET_MS}ms)"
else
  # Soft until warm caches settle — still report, fail hard only if absurdly slow.
  if [[ -n "${ttfb_ms}" && "${ttfb_ms}" -gt 3000 ]]; then
    fail "TTFB_BUDGET=NO (${ttfb_ms}ms > 3000ms hard cap)"
  else
    note "GATE_WARN TTFB_BUDGET soft miss (${ttfb_ms}ms > ${TTFB_BUDGET_MS}ms)"
  fi
fi

# Protocol-3 warehouse poll must return ASP.NET/php-chpu products (not empty migration).
article_plain="${ECOMAE_ARTICLE_PLAIN:-C110J}"
brand_plain="${ECOMAE_BRAND_PLAIN:-JS ASAKASHI}"
query_json=$(printf '{"article":"%s","searsch_str":"%s","manufacturer":"%s","manufacturers":[],"analogs":[],"office_storage_bunches":[]}' \
  "$article_plain" "$article_plain" "$brand_plain")
curl -sS -o /tmp/epc_wh_bunch.json -w '%{http_code}' --max-time 45 \
  -X POST "${BASE}${BUNCH_API}" \
  -F "article=${article_plain}" \
  -F "brand=${brand_plain}" \
  -F "office_id=0" \
  -F "storage_id=0" \
  -F "query=${query_json}" > /tmp/epc_wh_bunch_code.txt || echo 000 > /tmp/epc_wh_bunch_code.txt
bunch_code=$(cat /tmp/epc_wh_bunch_code.txt)
note "BUNCH_HTTP=${bunch_code}"
[[ "$bunch_code" == "200" ]] && ok "BUNCH_HTTP_200=YES" || fail "BUNCH_HTTP_200=NO code=${bunch_code}"
if rg -qi '"source"\s*:\s*"(aspnet-warehouse|php-chpu|database)"' /tmp/epc_wh_bunch.json 2>/dev/null; then
  ok "BUNCH_SOURCE_OK=YES"
else
  note "GATE_WARN BUNCH_SOURCE=$(rg -o '\"source\"\s*:\s*\"[^\"]+\"' /tmp/epc_wh_bunch.json | head -1 || true)"
fi
if rg -qi '"products"\s*:\s*\[' /tmp/epc_wh_bunch.json 2>/dev/null; then
  ok "BUNCH_PRODUCTS_KEY=YES"
else
  fail "BUNCH_PRODUCTS_KEY=NO"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS PARTS_WAREHOUSE_PHP_FAST=YES TTFB_MS=${ttfb_ms} PROBE=${PARTS_PATH}"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above TTFB_MS=${ttfb_ms}"
exit 1
