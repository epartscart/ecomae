#!/usr/bin/env bash
# Prove CHPU no longer sticks on Polling… and crossbase merge is available.
set -euo pipefail
HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
UA="${ECOMAE_CURL_UA:-Mozilla/5.0 (compatible; EcomAE-ChpuHangProve/1.0)}"
pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }
has() { grep -Eiq -- "$1" "$2" 2>/dev/null; }

note "======== EPARTSCART CHPU HANG+CROSSBASE PROVE ========"
curl -sS -A "$UA" -o /tmp/epc_chpu_hang.html -D /tmp/epc_chpu_hang.hdr -w 'CHPU_HTTP=%{http_code} TTFB=%{time_starttransfer}\n' --max-time 45 \
  "${BASE}/en/parts/JS%20ASAKASHI/C110J" || true
tr -d '\r' < /tmp/epc_chpu_hang.hdr > /tmp/epc_chpu_hang.hdr.c 2>/dev/null || true
mv -f /tmp/epc_chpu_hang.hdr.c /tmp/epc_chpu_hang.hdr 2>/dev/null || true

has 'x-ecomae-platform:[[:space:]]*primary' /tmp/epc_chpu_hang.hdr && ok "PRIMARY=YES" || fail "PRIMARY=NO"
has 'data-ssr-offers="1"' /tmp/epc_chpu_hang.html && ok "SSR_OFFERS=YES" || fail "SSR_OFFERS=NO"
has 'epc-part-type-row' /tmp/epc_chpu_hang.html && ok "ROWS=YES" || fail "ROWS=NO"
has 'include_crossbase=1' /tmp/epc_chpu_hang.html && ok "CLIENT_CROSSBASE=YES" || fail "CLIENT_CROSSBASE=NO"
has 'Never leave .Polling suppliers' /tmp/epc_chpu_hang.html && ok "FINISHPOLL_FIX=YES" || fail "FINISHPOLL_FIX=NO"
has 'notranslate' /tmp/epc_chpu_hang.html && ok "NOTRANSLATE=YES" || fail "NOTRANSLATE=NO"
has '__epcChpuBootRunning' /tmp/epc_chpu_hang.html && ok "BOOT_GUARD=YES" || fail "BOOT_GUARD=NO"

# Local cross fast
curl -sS -A "$UA" -o /tmp/epc_cross_local.json -w 'CROSS_LOCAL_TTFB=%{time_starttransfer}\n' --max-time 20 \
  "${BASE}/storefront/cross-search?article=C110J&brand=JS%20ASAKASHI&limit=200" || true
has '"status"[[:space:]]*:[[:space:]]*true' /tmp/epc_cross_local.json && ok "CROSS_LOCAL=YES" || fail "CROSS_LOCAL=NO"

# Crossbase merge (may be 0 if provider down — still must return 200 + local)
curl -sS -A "$UA" -o /tmp/epc_cross_cb.json -w 'CROSS_CB_TTFB=%{time_starttransfer} CROSS_CB_HTTP=%{http_code}\n' --max-time 20 \
  "${BASE}/storefront/cross-search?article=C110J&brand=JS%20ASAKASHI&limit=600&include_crossbase=1" || true
has '"status"[[:space:]]*:[[:space:]]*true' /tmp/epc_cross_cb.json && ok "CROSS_CB_STATUS=YES" || fail "CROSS_CB_STATUS=NO"
has 'crossbase_count' /tmp/epc_cross_cb.json && ok "CROSS_CB_FIELD=YES" || fail "CROSS_CB_FIELD=NO"
python3 - <<'PY' || fail "CROSS_CB_PARSE=NO"
import json
d=json.load(open('/tmp/epc_cross_cb.json'))
local=int(d.get('local_count') or 0)
cb=int(d.get('crossbase_count') or 0)
refs=len(d.get('references') or [])
print(f'local={local} crossbase={cb} refs={refs} source={d.get("source")}')
assert local > 0 or refs > 0
print('GATE_OK CROSS_HAS_REFS=YES')
PY

if has 'ajax_epc_cross_search\.php|product\.php|dp_product\.php' /tmp/epc_chpu_hang.html; then
  fail "NO_PRODUCT_PHP=NO"
else
  ok "NO_PRODUCT_PHP=YES"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS EPARTSCART_CHPU_HANG_CROSSBASE=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD"
exit 1
