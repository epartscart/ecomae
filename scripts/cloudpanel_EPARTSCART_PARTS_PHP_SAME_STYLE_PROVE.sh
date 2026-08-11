#!/usr/bin/env bash
# Prove ePartsCart brand picker + CHPU warehouse use PHP professional-shell presentation.
# Usage: bash scripts/cloudpanel_EPARTSCART_PARTS_PHP_SAME_STYLE_PROVE.sh
# Must print RESULT=PASS — silent External action without paste-back = FAIL.
# CloudPanel images often lack ripgrep — use grep -Eiq fallback.
set -euo pipefail

HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
PICKER="${ECOMAE_PICKER_PROBE:-/storefront/search-app?article=1310154101}"
CHPU="${ECOMAE_CHPU_PROBE:-/en/parts/TEIKINP/1310154101}"
ASSET="${ECOMAE_WH_ASSET_PROBE:-/platform-assets/epc_warehouse_search_parity.js?v=20260811x}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }

# Prefer rg when present; otherwise GNU/BusyBox grep -Eiq (CloudPanel has no rg).
haystack_match() {
  local pat="$1" file="$2"
  if command -v rg >/dev/null 2>&1; then
    rg -qi -- "$pat" "$file"
  else
    grep -Eiq -- "$pat" "$file"
  fi
}

note "======== EPARTSCART PARTS PHP SAME-STYLE PROVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo local)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "PICKER=${BASE}${PICKER}"
note "CHPU=${BASE}${CHPU}"
if command -v rg >/dev/null 2>&1; then
  note "MATCHER=rg"
else
  note "MATCHER=grep -Eiq (rg not installed)"
fi

curl -sS -o /tmp/epc_picker.html -w '%{http_code}' --max-time 45 -L -A 'Mozilla/5.0' "${BASE}${PICKER}" > /tmp/epc_picker_code.txt || echo 000 > /tmp/epc_picker_code.txt
note "PICKER_HTTP=$(cat /tmp/epc_picker_code.txt)"
haystack_match 'epc-brand-picker-table' /tmp/epc_picker.html && ok "PICKER_TABLE=YES" || fail "PICKER_TABLE=NO"
haystack_match 'epc-brand-picker-top__title' /tmp/epc_picker.html && ok "PICKER_TOP=YES" || fail "PICKER_TOP=NO"
haystack_match 'Open prices' /tmp/epc_picker.html && ok "PICKER_CTA=YES" || fail "PICKER_CTA=NO"
haystack_match 'epc-sf-brand-grid' /tmp/epc_picker.html && fail "PICKER_INVENTED_GRID=YES" || ok "PICKER_INVENTED_GRID=NO"
haystack_match '<base href="/templates/nero/"' /tmp/epc_picker.html && fail "PICKER_PHP_NERO=YES" || ok "PICKER_PHP_NERO=NO"

curl -sS -o /tmp/epc_chpu.html -w '%{http_code}' --max-time 45 -L -A 'Mozilla/5.0' "${BASE}${CHPU}" > /tmp/epc_chpu_code.txt || echo 000 > /tmp/epc_chpu_code.txt
note "CHPU_HTTP=$(cat /tmp/epc_chpu_code.txt)"
haystack_match 'id="all_table_products"' /tmp/epc_chpu.html && ok "CHPU_TABLE=YES" || fail "CHPU_TABLE=NO"
haystack_match 'th_photo' /tmp/epc_chpu.html && ok "CHPU_PHOTO_COL=YES" || fail "CHPU_PHOTO_COL=NO"
haystack_match 'th_info' /tmp/epc_chpu.html && ok "CHPU_INFO_COL=YES" || fail "CHPU_INFO_COL=NO"
haystack_match 'epc-part-type-split' /tmp/epc_chpu.html && ok "CHPU_SPLIT=YES" || fail "CHPU_SPLIT=NO"
haystack_match 'one_property' /tmp/epc_chpu.html && ok "CHPU_FILTER=YES" || fail "CHPU_FILTER=NO"
haystack_match 'epc-fitment-check-btn' /tmp/epc_chpu.html && ok "CHPU_FITMENT=YES" || fail "CHPU_FITMENT=NO"
haystack_match 'epc-cross-search-btn' /tmp/epc_chpu.html && ok "CHPU_CROSS=YES" || fail "CHPU_CROSS=NO"
haystack_match 'epc-seo-cross-refs' /tmp/epc_chpu.html && ok "CHPU_CROSS_NAV=YES" || fail "CHPU_CROSS_NAV=NO"
haystack_match 'epc-wa-share-btn|epc-btn-cart|Log in' /tmp/epc_chpu.html && ok "CHPU_ACTIONS=YES" || fail "CHPU_ACTIONS=NO"
haystack_match 'v=20260811x' /tmp/epc_chpu.html && ok "CHPU_ASSET_BUST=YES" || fail "CHPU_ASSET_BUST=NO (publish stale?)"
haystack_match 'epc-sf-search-table' /tmp/epc_chpu.html && fail "CHPU_INVENTED_TABLE=YES" || ok "CHPU_INVENTED_TABLE=NO"

curl -sSI --max-time 20 "${BASE}${ASSET}" | tr -d '\r' > /tmp/epc_wh_asset.hdr || true
haystack_match '^HTTP/.*[[:space:]]200' /tmp/epc_wh_asset.hdr && ok "ASSET_HTTP=200" || fail "ASSET_HTTP_NOT_200"

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS PARTS_PHP_SAME_STYLE=YES PICKER=YES CHPU=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above"
exit 1
