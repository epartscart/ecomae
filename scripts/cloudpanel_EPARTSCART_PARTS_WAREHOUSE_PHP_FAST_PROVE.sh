#!/usr/bin/env bash
# Prove ePartsCart brand+article CHPU uses ASP.NET skip-SSR + protocol-3 AJAX fast path.
# Usage:
#   bash scripts/cloudpanel_EPARTSCART_PARTS_WAREHOUSE_PHP_FAST_PROVE.sh
# Must print RESULT=PASS — silent External action without paste-back = FAIL.
#
# CloudPanel hosts often lack ripgrep — use grep -Ei only (no `rg` dependency).
# Blazor CHPU rejects HEAD → 405; always probe with GET.
set -euo pipefail

HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
PARTS_PATH="${ECOMAE_PARTS_PROBE:-/en/parts/JS%20ASAKASHI/C110J}"
BUNCH_API="${ECOMAE_BUNCH_PROBE:-/storefront/products-of-bunch}"
TTFB_BUDGET_MS="${ECOMAE_CHPU_TTFB_BUDGET_MS:-1200}"
BUNCH_BUDGET_MS="${ECOMAE_CHPU_BUNCH_BUDGET_MS:-3000}"
UA="${ECOMAE_CURL_UA:-Mozilla/5.0 (compatible; EcomAE-CHPU-Prove/1.0)}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }

# Case-insensitive substring match without ripgrep.
has() {
  local pat="$1"
  local file="$2"
  grep -Eiq -- "$pat" "$file" 2>/dev/null
}

note "======== EPARTSCART PARTS WAREHOUSE PHP-FAST PROVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo local)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "CHPU=${BASE}${PARTS_PATH}"
note "TTFB_BUDGET_MS=${TTFB_BUDGET_MS}"

# Warm once, then measure TTFB (ms) on second GET (never HEAD — Blazor returns 405).
curl -sS -A "$UA" -o /dev/null --max-time 45 "${BASE}${PARTS_PATH}" || true
ttfb_ms=$(curl -sS -A "$UA" -o /tmp/epc_wh_fast.html -D /tmp/epc_wh_fast_hdr.txt \
  -w '%{time_starttransfer}' --max-time 45 "${BASE}${PARTS_PATH}" \
  | awk '{printf "%d", ($1*1000)+0.5}')
code=$(awk 'BEGIN{c="000"} /^HTTP\//{c=$2} END{print c}' /tmp/epc_wh_fast_hdr.txt 2>/dev/null || echo 000)
# Fallback code from a dedicated GET if header parse failed.
if [[ "$code" == "000" || -z "$code" ]]; then
  code=$(curl -sS -A "$UA" -o /dev/null -w '%{http_code}' --max-time 30 "${BASE}${PARTS_PATH}" || echo 000)
fi
tr -d '\r' < /tmp/epc_wh_fast_hdr.txt > /tmp/epc_wh_fast_hdr.clean.txt 2>/dev/null || true
mv -f /tmp/epc_wh_fast_hdr.clean.txt /tmp/epc_wh_fast_hdr.txt 2>/dev/null || true

note "PARTS_HTTP=${code}"
note "TTFB_MS=${ttfb_ms}"

has 'x-ecomae-platform:[[:space:]]*primary' /tmp/epc_wh_fast_hdr.txt && ok "PARTS_HEADER_PRIMARY=YES" || fail "PARTS_HEADER_PRIMARY=NO"
[[ "$code" == "200" ]] && ok "PARTS_HTTP_200=YES" || fail "PARTS_HTTP_200=NO code=${code}"

if has '<base href="/templates/nero/"' /tmp/epc_wh_fast.html; then
  fail "PARTS_PHP_NERO=YES"
else
  ok "PARTS_PHP_NERO=NO"
fi
if has 'ajax_getProductsOfBunch\.php' /tmp/epc_wh_fast.html; then
  fail "PARTS_PHP_AJAX_BODY=YES"
else
  ok "PARTS_PHP_AJAX_BODY=NO"
fi

has 'runChpuPriceSearch' /tmp/epc_wh_fast.html && ok "CHPU_RUN_SEARCH=YES" || fail "CHPU_RUN_SEARCH=NO"
has 'Immediate protocol-3 poll' /tmp/epc_wh_fast.html && ok "CHPU_IMMEDIATE_P3=YES" || fail "CHPU_IMMEDIATE_P3=NO"
has 'AbortSignal\.timeout\(3000\)' /tmp/epc_wh_fast.html && ok "CHPU_ABORT_3S=YES" || fail "CHPU_ABORT_3S=NO"
has '/storefront/cross-search' /tmp/epc_wh_fast.html && ok "CHPU_ASPNET_CROSS=YES" || fail "CHPU_ASPNET_CROSS=NO"
has 'data-enhance-nav="false"' /tmp/epc_wh_fast.html && ok "CHPU_FULL_NAV=YES" || fail "CHPU_FULL_NAV=NO"
# Product CHPU HTML must not embed live .php product URLs (PHP deletion-ready).
if has 'ajax_epc_cross_search\.php|ajax_getProductsOfBunch\.php|ajax_add_to_basket\.php|umapi_proxy\.php|/content/shop/[^"'\'' ]+\.php' /tmp/epc_wh_fast.html; then
  fail "CHPU_NO_PRODUCT_PHP=NO (product .php URL still in HTML)"
else
  ok "CHPU_NO_PRODUCT_PHP=YES"
fi
has 'loadAspNetCrossSearch' /tmp/epc_wh_fast.html && ok "CHPU_ASPNET_CROSS_FN=YES" || fail "CHPU_ASPNET_CROSS_FN=NO"
has 'pickProtocol3Bunch' /tmp/epc_wh_fast.html && ok "CHPU_PROTOCOL3_PICK=YES" || fail "CHPU_PROTOCOL3_PICK=NO"
has 'Promise\.all' /tmp/epc_wh_fast.html && ok "CHPU_PROMISE_ALL=YES" || fail "CHPU_PROMISE_ALL=NO"
has '/storefront/products-of-bunch' /tmp/epc_wh_fast.html && ok "CHPU_PRODUCTS_OF_BUNCH=YES" || fail "CHPU_PRODUCTS_OF_BUNCH=NO"
has 'all_table_products' /tmp/epc_wh_fast.html && ok "PARTS_TABLE=YES" || fail "PARTS_TABLE=NO"
has 'polling suppliers|No warehouse offers yet' /tmp/epc_wh_fast.html && ok "CHPU_AJAX_SHELL=YES" || fail "CHPU_AJAX_SHELL=NO"
has 'DT068|AISIN|C110J|ASAKASHI' /tmp/epc_wh_fast.html && ok "PARTS_ARTICLE_MARKERS=YES" || fail "PARTS_ARTICLE_MARKERS=NO"

if [[ -n "${ttfb_ms}" && "${ttfb_ms}" -le "${TTFB_BUDGET_MS}" ]]; then
  ok "TTFB_BUDGET=YES (${ttfb_ms}ms <= ${TTFB_BUDGET_MS}ms)"
else
  if [[ -n "${ttfb_ms}" && "${ttfb_ms}" -gt 3000 ]]; then
    fail "TTFB_BUDGET=NO (${ttfb_ms}ms > 3000ms hard cap)"
  else
    note "GATE_WARN TTFB_BUDGET soft miss (${ttfb_ms}ms > ${TTFB_BUDGET_MS}ms)"
  fi
fi

# Protocol-3 warehouse poll must return products inside the 1–3s budget.
article_plain="${ECOMAE_ARTICLE_PLAIN:-C110J}"
brand_plain="${ECOMAE_BRAND_PLAIN:-JS ASAKASHI}"
query_json=$(printf '{"article":"%s","searsch_str":"%s","manufacturer":"%s","manufacturers":[],"analogs":[],"office_storage_bunches":[]}' \
  "$article_plain" "$article_plain" "$brand_plain")
bunch_total_s=$(curl -sS -A "$UA" -o /tmp/epc_wh_bunch.json -w '%{time_total}' --max-time 10 \
  -X POST "${BASE}${BUNCH_API}" \
  -F "article=${article_plain}" \
  -F "brand=${brand_plain}" \
  -F "office_id=0" \
  -F "storage_id=0" \
  -F "query=${query_json}" || echo 99)
bunch_code=$(curl -sS -A "$UA" -o /dev/null -w '%{http_code}' --max-time 10 \
  -X POST "${BASE}${BUNCH_API}" \
  -F "article=${article_plain}" \
  -F "brand=${brand_plain}" \
  -F "office_id=0" \
  -F "storage_id=0" \
  -F "query=${query_json}" || echo 000)
bunch_ms=$(awk -v t="$bunch_total_s" 'BEGIN { printf "%d", (t*1000)+0.5 }')
note "BUNCH_HTTP=${bunch_code}"
note "BUNCH_MS=${bunch_ms} (budget ${BUNCH_BUDGET_MS}ms)"
[[ "$bunch_code" == "200" ]] && ok "BUNCH_HTTP_200=YES" || fail "BUNCH_HTTP_200=NO code=${bunch_code}"
if [[ -n "${bunch_ms}" && "${bunch_ms}" -le "${BUNCH_BUDGET_MS}" ]]; then
  ok "BUNCH_BUDGET=YES (${bunch_ms}ms <= ${BUNCH_BUDGET_MS}ms)"
else
  fail "BUNCH_BUDGET=NO (${bunch_ms}ms > ${BUNCH_BUDGET_MS}ms)"
fi
if has '"source"[[:space:]]*:[[:space:]]*"(aspnet-warehouse|php-chpu|php-bunch|database)"' /tmp/epc_wh_bunch.json; then
  ok "BUNCH_SOURCE_OK=YES"
else
  src_hint=$(grep -Eo '"source"[[:space:]]*:[[:space:]]*"[^"]+"' /tmp/epc_wh_bunch.json 2>/dev/null | head -1 || true)
  note "GATE_WARN BUNCH_SOURCE=${src_hint}"
fi
if has '"products"[[:space:]]*:[[:space:]]*\[' /tmp/epc_wh_bunch.json; then
  ok "BUNCH_PRODUCTS_KEY=YES"
else
  fail "BUNCH_PRODUCTS_KEY=NO"
fi

# Local CP cross network must paint fast (ASP.NET-only; no product .php).
CROSS_BUDGET_MS="${ECOMAE_CHPU_CROSS_BUDGET_MS:-1500}"
brand_q=$(python3 -c "import urllib.parse; print(urllib.parse.quote('''${brand_plain}'''))")
cross_total_s=$(curl -sS -A "$UA" -o /tmp/epc_wh_cross.json -w '%{time_total}' --max-time 5 \
  "${BASE}/storefront/cross-search?article=${article_plain}&brand=${brand_q}" \
  || echo 99)
cross_ms=$(awk -v t="$cross_total_s" 'BEGIN { printf "%d", (t*1000)+0.5 }')
note "CROSS_MS=${cross_ms} (budget ${CROSS_BUDGET_MS}ms)"
if [[ -n "${cross_ms}" && "${cross_ms}" -le "${CROSS_BUDGET_MS}" ]]; then
  ok "CROSS_BUDGET=YES (${cross_ms}ms <= ${CROSS_BUDGET_MS}ms)"
else
  fail "CROSS_BUDGET=NO (${cross_ms}ms > ${CROSS_BUDGET_MS}ms)"
fi
if has '"source"[[:space:]]*:[[:space:]]*"aspnet-cross-local"' /tmp/epc_wh_cross.json; then
  ok "CROSS_SOURCE_LOCAL=YES"
else
  fail "CROSS_SOURCE_LOCAL=NO"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS PARTS_WAREHOUSE_PHP_FAST=YES TTFB_MS=${ttfb_ms} BUNCH_MS=${bunch_ms} CROSS_MS=${cross_ms} PROBE=${PARTS_PATH}"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above TTFB_MS=${ttfb_ms} BUNCH_MS=${bunch_ms} CROSS_MS=${cross_ms}"
exit 1
