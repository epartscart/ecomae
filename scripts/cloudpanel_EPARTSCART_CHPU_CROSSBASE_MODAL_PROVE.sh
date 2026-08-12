#!/usr/bin/env bash
# Prove TOYOTA/1310154101 green CROSSBASE button opens PHP-twin modal + overlap provenance.
set -euo pipefail
HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
UA="${ECOMAE_CURL_UA:-Mozilla/5.0 (compatible; EcomAE-ChpuCrossModalProve/1.0)}"
pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }
has() { grep -Eiq -- "$1" "$2" 2>/dev/null; }

note "======== EPARTSCART CHPU CROSSBASE MODAL PROVE ========"
curl -sS -A "$UA" -o /tmp/epc_chpu_cb_modal.html -D /tmp/epc_chpu_cb_modal.hdr -w 'CHPU_HTTP=%{http_code}\n' --max-time 45 \
  "${BASE}/en/parts/TOYOTA/1310154101" || true
tr -d '\r' < /tmp/epc_chpu_cb_modal.hdr > /tmp/epc_chpu_cb_modal.hdr.c 2>/dev/null || true
mv -f /tmp/epc_chpu_cb_modal.hdr.c /tmp/epc_chpu_cb_modal.hdr 2>/dev/null || true

has 'x-ecomae-platform:[[:space:]]*primary' /tmp/epc_chpu_cb_modal.hdr && ok "PRIMARY=YES" || fail "PRIMARY=NO"
has 'epc-cross-search-btn' /tmp/epc_chpu_cb_modal.html && ok "CROSS_BTN=YES" || fail "CROSS_BTN=NO"
has 'epc_warehouse_search_parity\.js\?v=20260812-cross-stock' /tmp/epc_chpu_cb_modal.html && ok "PARITY_JS_BUST=YES" || fail "PARITY_JS_BUST=NO"
has '__epcLastCrossPayload' /tmp/epc_chpu_cb_modal.html && ok "PAYLOAD_CACHE=YES" || fail "PAYLOAD_CACHE=NO"
has 'mergeCrossStockIntoOffers' /tmp/epc_chpu_cb_modal.html && ok "CROSS_STOCK_MERGE=YES" || fail "CROSS_STOCK_MERGE=NO"

# The HTML-referenced URL is what browsers load — must contain the modal (not a stale CDN body).
REF_JS_URL="$(grep -oE '/platform-assets/epc_warehouse_search_parity\.js\?v=[^\"'\'' ]+' /tmp/epc_chpu_cb_modal.html | head -1 || true)"
[[ -n "$REF_JS_URL" ]] || REF_JS_URL="/platform-assets/epc_warehouse_search_parity.js?v=20260812-cross-stock"
curl -sS -A "$UA" -o /tmp/epc_parity_cb_modal.js -w 'PARITY_JS_HTTP=%{http_code}\n' --max-time 20 \
  "${BASE}${REF_JS_URL}" || true
note "PARITY_JS_REF=${REF_JS_URL}"
has 'function openCrossModal\(' /tmp/epc_parity_cb_modal.js && ok "OPEN_MODAL_FN=YES" || fail "OPEN_MODAL_FN=NO"
has 'openCrossModalFromButton' /tmp/epc_parity_cb_modal.js && ok "MODAL_BTN_WIRE=YES" || fail "MODAL_BTN_WIRE=NO"
# Live must NOT only scroll — modal path replaces focusCross-only click.
if has 'openCrossModalFromButton' /tmp/epc_parity_cb_modal.js; then
  ok "FOCUS_ONLY_CLICK=YES"
elif has 'crossBtn\.addEventListener\("click", function \(\) \{\s*focusCross' /tmp/epc_parity_cb_modal.js; then
  fail "FOCUS_ONLY_CLICK=NO"
else
  fail "FOCUS_ONLY_CLICK=NO"
fi

curl -sS -A "$UA" -o /tmp/epc_toyota_cross.json -w 'CROSS_HTTP=%{http_code} TTFB=%{time_starttransfer}\n' --max-time 25 \
  "${BASE}/storefront/cross-search?article=1310154101&brand=TOYOTA&limit=600&include_crossbase=1" || true
has '"status"[[:space:]]*:[[:space:]]*true' /tmp/epc_toyota_cross.json && ok "CROSS_STATUS=YES" || fail "CROSS_STATUS=NO"

python3 - <<'PY' || fail "CROSS_PROVENANCE=NO"
import json
from collections import Counter
d = json.load(open("/tmp/epc_toyota_cross.json"))
cb = int(d.get("crossbase_count") or 0)
refs = d.get("references") or []
sources = Counter(str(r.get("source") or "") for r in refs)
cross_tagged = sum(1 for r in refs if "crossbase" in str(r.get("source") or "").lower())
print(f"crossbase_count={cb} refs={len(refs)} sources={dict(sources)} cross_tagged={cross_tagged}")
assert cb > 0, "expected crossbase_count > 0 for TOYOTA 1310154101"
# Overlap retag (cp+crossbase) and/or unique crossbase rows must be visible to the modal Source column.
assert cross_tagged > 0, "expected at least one reference source containing crossbase"
print("GATE_OK CROSS_PROVENANCE=YES")
stock = d.get("stock") or []
stock_count = int(d.get("stock_count") or len(stock) or 0)
print(f"stock_count={stock_count} stock_len={len(stock)}")
# PHP ajax_epc_cross_search returns UAE warehouse hits for cross numbers when typed OE is empty.
assert stock_count > 0 and len(stock) > 0, "expected cross stock rows (PHP empty-warehouse fill)"
print("GATE_OK CROSS_STOCK=YES")
PY

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS EPARTSCART_CHPU_CROSSBASE_MODAL=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD"
exit 1
