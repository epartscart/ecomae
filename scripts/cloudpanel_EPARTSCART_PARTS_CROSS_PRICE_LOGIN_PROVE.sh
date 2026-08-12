#!/usr/bin/env bash
# Prove AISIN/DT068 crossbase rows surface + logged-in price unmask markers are live.
set -euo pipefail
HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
UA="${ECOMAE_CURL_UA:-Mozilla/5.0 (compatible; EcomAE-PartsCrossPriceProve/1.0)}"
pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }
has() { grep -Eiq -- "$1" "$2" 2>/dev/null; }

note "======== EPARTSCART PARTS CROSS+PRICE LOGIN PROVE ========"
curl -sS -A "$UA" -o /tmp/epc_dt068.html -D /tmp/epc_dt068.hdr -w 'CHPU_HTTP=%{http_code} TTFB=%{time_starttransfer}\n' --max-time 45 \
  "${BASE}/en/parts/AISIN/DT068" || true
tr -d '\r' < /tmp/epc_dt068.hdr > /tmp/epc_dt068.hdr.c 2>/dev/null || true
mv -f /tmp/epc_dt068.hdr.c /tmp/epc_dt068.hdr 2>/dev/null || true

has 'x-ecomae-platform:[[:space:]]*primary' /tmp/epc_dt068.hdr && ok "PRIMARY=YES" || fail "PRIMARY=NO"
has 'epc-cross-ref-list' /tmp/epc_dt068.html && ok "CROSS_LIST=YES" || fail "CROSS_LIST=NO"
has 'include_crossbase=1' /tmp/epc_dt068.html && ok "CLIENT_CROSSBASE=YES" || fail "CLIENT_CROSSBASE=NO"
has 'prices_visible === true' /tmp/epc_dt068.html && ok "PRICE_UNLATCH=YES" || fail "PRICE_UNLATCH=NO"
has '__epcPriceUnmaskRepoll' /tmp/epc_dt068.html && ok "PRICE_REPOLL=YES" || fail "PRICE_REPOLL=NO"
has 'data-prices-visible="0"' /tmp/epc_dt068.html && ok "GUEST_MASK_SSR=YES" || fail "GUEST_MASK_SSR=NO"

# Crossbase merge must reserve slots — unique source=crossbase OR crossbase_count>0 with merge source.
curl -sS -A "$UA" -o /tmp/epc_dt068_cross.json -w 'CROSS_HTTP=%{http_code} TTFB=%{time_starttransfer}\n' --max-time 25 \
  "${BASE}/storefront/cross-search?article=DT068&brand=AISIN&limit=80&include_crossbase=1" || true
has '"status"[[:space:]]*:[[:space:]]*true' /tmp/epc_dt068_cross.json && ok "CROSS_STATUS=YES" || fail "CROSS_STATUS=NO"
python3 - <<'PY' || fail "CROSS_MERGE=NO"
import json
d=json.load(open('/tmp/epc_dt068_cross.json'))
refs=d.get('references') or []
cb_rows=[r for r in refs if str(r.get('source') or '').lower()=='crossbase']
local=int(d.get('local_count') or 0)
cb=int(d.get('crossbase_count') or 0)
src=str(d.get('source') or '')
print(f'local={local} crossbase_count={cb} crossbase_rows={len(cb_rows)} refs={len(refs)} source={src}')
assert len(refs) > 0, 'no references'
# Either unique crossbase rows painted, or merge source + non-zero crossbase_count (overlap-only).
assert len(cb_rows) > 0 or (cb > 0 and 'crossbase' in src), f'crossbase missing: rows={len(cb_rows)} count={cb} source={src}'
# With limit=80, local alone must not consume 100% when unique crossbase exists.
if len(cb_rows) > 0:
    assert local < 80 or len(refs) >= 80, 'local crowded out reserved slots'
print('GATE_OK CROSS_MERGE=YES')
PY

# Parity JS asset ships include_crossbase
curl -sS -A "$UA" -o /tmp/epc_parity.js --max-time 20 \
  "${BASE}/platform-assets/epc_warehouse_search_parity.js?v=20260812-cross-price" || true
has 'include_crossbase=1' /tmp/epc_parity.js && ok "PARITY_JS_CROSSBASE=YES" || fail "PARITY_JS_CROSSBASE=NO"
has 'data-source=' /tmp/epc_parity.js && ok "PARITY_JS_SOURCE=YES" || fail "PARITY_JS_SOURCE=NO"

if has 'ajax_epc_cross_search\.php|product\.php|dp_product\.php' /tmp/epc_dt068.html; then
  fail "NO_PRODUCT_PHP=NO"
else
  ok "NO_PRODUCT_PHP=YES"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS EPARTSCART_PARTS_CROSS_PRICE_LOGIN=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD"
exit 1
