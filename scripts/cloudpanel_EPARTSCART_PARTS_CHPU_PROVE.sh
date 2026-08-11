#!/usr/bin/env bash
# Prove ePartsCart /en/parts/{BRAND}/{ARTICLE} is ASP.NET primary (not PHP nero).
# Usage:
#   bash scripts/cloudpanel_EPARTSCART_PARTS_CHPU_PROVE.sh
# Must print RESULT=PASS — silent External action without paste-back = FAIL.
set -euo pipefail

HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
PARTS_PATH="${ECOMAE_PARTS_PROBE:-/en/parts/TOYOTA/1310154101}"
PART_SEARCH="${ECOMAE_PART_SEARCH_PROBE:-/en/shop/part_search?article=1310154101&brend=TOYOTA}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }

note "======== EPARTSCART PARTS CHPU ASP.NET PROVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo local)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "TARGET=${BASE}${PARTS_PATH}"

hdr=$(curl -sSI --max-time 30 "${BASE}${PARTS_PATH}" | tr -d '\r' || true)
code=$(curl -sS -o /tmp/epc_parts_chpu.html -w '%{http_code}' --max-time 45 "${BASE}${PARTS_PATH}" || echo 000)
note "PARTS_HTTP=${code}"
echo "$hdr" | head -n 20 || true

echo "$hdr" | rg -qi 'x-ecomae-platform:\s*primary' && ok "PARTS_HEADER_PRIMARY=YES" || fail "PARTS_HEADER_PRIMARY=NO (still PHP edge?)"
if rg -qi '<base href="/templates/nero/"' /tmp/epc_parts_chpu.html 2>/dev/null; then
  fail "PARTS_PHP_NERO=YES base_templates_nero"
else
  ok "PARTS_PHP_NERO=NO"
fi
if rg -qi 'ajax_getProductsOfBunch\.php' /tmp/epc_parts_chpu.html 2>/dev/null; then
  fail "PARTS_PHP_AJAX=YES ajax_getProductsOfBunch"
else
  ok "PARTS_PHP_AJAX=NO"
fi
if rg -qi 'ecomae-chrome-surface' /tmp/epc_parts_chpu.html 2>/dev/null; then
  ok "PARTS_CHROME=YES"
else
  fail "PARTS_CHROME=NO missing ecomae-chrome-surface"
fi
# Must stay on same URL (no forced bounce to /storefront/search-app only).
final_url=$(curl -sS -o /dev/null -w '%{url_effective}' --max-time 45 -L "${BASE}${PARTS_PATH}" || true)
if [[ "$final_url" == *"/en/parts/"* ]] || [[ "$final_url" == *"/parts/"* ]]; then
  ok "PARTS_SAME_URL=YES final=${final_url}"
else
  note "GATE_WARN PARTS_SAME_URL final=${final_url} (acceptable if search-app; prefer CHPU)"
fi

hdr2=$(curl -sSI --max-time 30 "${BASE}${PART_SEARCH}" | tr -d '\r' || true)
code2=$(curl -sS -o /tmp/epc_part_search.html -w '%{http_code}' --max-time 45 "${BASE}${PART_SEARCH}" || echo 000)
note "PART_SEARCH_HTTP=${code2}"
echo "$hdr2" | rg -qi 'x-ecomae-platform:\s*primary' && ok "PART_SEARCH_PRIMARY=YES" || fail "PART_SEARCH_PRIMARY=NO"

# PHP reference archive may be paused — warn only.
php_ref=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 "${BASE}/php-reference/home" || echo 000)
note "PHP_REFERENCE_HOME=${php_ref}"

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS PARTS_CHPU_ASPNET=YES PART_SEARCH_ASPNET=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above"
exit 1
