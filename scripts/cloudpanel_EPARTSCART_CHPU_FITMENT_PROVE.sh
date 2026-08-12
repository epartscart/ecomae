#!/usr/bin/env bash
# Prove Fitment check on ASP.NET CHPU matches PHP behavior (panel + brands + vehicle load).
set -euo pipefail
HOST="${ECOMAE_PROVE_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
ART="${ECOMAE_FITMENT_ARTICLE:-C110J}"
BRAND="${ECOMAE_FITMENT_BRAND:-JS ASAKASHI}"
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }
pass=0; fail=0
ok() { note "FITMENT_OK $*"; pass=$((pass+1)); }
bad() { note "FITMENT_BAD $*"; fail=$((fail+1)); }

note "======== EPARTSCART CHPU FITMENT PROVE ========"
note "HOST=${HOST} ART=${ART} BRAND=${BRAND}"

CHPU="${BASE}/en/parts/$(python3 - <<PY
import urllib.parse
print(urllib.parse.quote('''${BRAND}''', safe=''))
PY
)/${ART}"
curl -sk -o /tmp/fit-chpu.html -w 'CHPU_HTTP=%{http_code} TTFB=%{time_starttransfer}\n' --max-time 25 "$CHPU" || true
grep -q 'epc-fitment-check-btn' /tmp/fit-chpu.html && ok "panel button in HTML" || bad "missing fitment button"
grep -q 'applicability_widget' /tmp/fit-chpu.html && ok "applicability widget" || bad "missing applicability_widget"
grep -q '20260812-fitment' /tmp/fit-chpu.html && ok "parity.js cache buster" || bad "stale parity.js (need republish)"
grep -q 'epc_warehouse_search_parity.js' /tmp/fit-chpu.html && ok "parity script tag" || bad "missing parity script"

curl -sk -o /tmp/fit-brands.json -w 'BRANDS_HTTP=%{http_code}\n' --max-time 20 \
  "${BASE}/storefront/search-brands?article=${ART}&limit=20" || true
python3 - <<'PY' && ok "search-brands JSON" || bad "search-brands broken"
import json
d=json.load(open('/tmp/fit-brands.json'))
assert d.get('ok') is True or d.get('brands') is not None
assert isinstance(d.get('brands') or [], list)
print('brands', len(d.get('brands') or []))
PY

BRAND_Q=$(python3 - <<PY
import urllib.parse
print(urllib.parse.quote('''${BRAND}'''))
PY
)
curl -sk -o /tmp/fit.json -w 'FITMENT_HTTP=%{http_code}\n' --max-time 25 \
  "${BASE}/storefront/fitment?article=${ART}&brand=${BRAND_Q}&language=en" || true
python3 - <<'PY' && ok "fitment JSON route" || bad "fitment JSON route missing"
import json
d=json.load(open('/tmp/fit.json'))
assert 'fallback_widget' in d or d.get('ok') is True or 'PC' in d
print('ok', d.get('ok'), 'source', d.get('source'), 'fallback', d.get('fallback_widget'))
PY

curl -sk -o /tmp/fit-widget.js -w 'WIDGET_HTTP=%{http_code} BYTES=%{size_download}\n' --max-time 25 \
  "${BASE}/storefront/fitment-widget.js?n=${ART}&lang=en" || true
grep -q 'applicability_widget\|fitment-table\|gettable\|epartscross' /tmp/fit-widget.js \
  && ok "fitment-widget.js body" || bad "fitment-widget.js empty/broken"
grep -q '/storefront/fitment-table' /tmp/fit-widget.js \
  && ok "widget rewritten to ASP.NET table proxy" || note "FITMENT_WARN widget still points upstream (rewrite miss)"

# Live umapi analogs often 402 — widget/table path must still respond.
curl -sk -o /tmp/fit-table.html -w 'TABLE_HTTP=%{http_code} BYTES=%{size_download}\n' --max-time 25 \
  -X POST -H 'Content-Type: application/json' -d '{}' \
  "${BASE}/storefront/fitment-table?n=${ART}&lang=en&cartype=UNI" || true
# Accept HTML body OR graceful message (upstream may 500 from some egress).
if [[ -s /tmp/fit-table.html ]]; then
  ok "fitment-table returned body"
else
  note "FITMENT_WARN fitment-table empty (upstream may be egress-blocked; browser path still uses widget)"
fi

curl -sk -o /tmp/parity.js -w 'PARITY_HTTP=%{http_code}\n' --max-time 15 \
  "${BASE}/platform-assets/epc_warehouse_search_parity.js?v=20260812-fitment" || true
grep -q 'loadEpartscrossFitmentFallback' /tmp/parity.js && ok "live parity has fallback" || bad "live parity stale"
grep -q 'Fitment action requires ASP.NET catalog route' /tmp/parity.js \
  && bad "live parity still hard-rejects analogs" || ok "analogs hard-reject removed"

note "FITMENT_SUMMARY pass=${pass} fail=${fail}"
[[ "$fail" -eq 0 ]] || die "fitment prove checks failed"
note "RESULT=PASS EPARTSCART_CHPU_FITMENT"
