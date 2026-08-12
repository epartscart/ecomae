#!/usr/bin/env bash
# Prove CHPU multi-warehouse grouping + left filter range wiring on ePartsCart.
set -euo pipefail
HOST="${ECOMAE_PROVE_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
ART="${ECOMAE_WHG_ARTICLE:-C110J}"
BRAND="${ECOMAE_WHG_BRAND:-JS ASAKASHI}"
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }
pass=0; fail=0
ok() { note "WHG_OK $*"; pass=$((pass+1)); }
bad() { note "WHG_BAD $*"; fail=$((fail+1)); }

note "======== EPARTSCART CHPU WAREHOUSE GROUP PROVE ========"
BRAND_Q=$(python3 - <<PY
import urllib.parse
print(urllib.parse.quote('''${BRAND}''', safe=''))
PY
)
CHPU="${BASE}/en/parts/${BRAND_Q}/${ART}"
curl -sk -o /tmp/whg.html -w 'CHPU_HTTP=%{http_code}\n' --max-time 25 "$CHPU" || true

grep -q 'epc_filter_price_min' /tmp/whg.html && ok "price filter inputs" || bad "price filter missing"
grep -q 'epc_filter_exist_min' /tmp/whg.html && ok "qty filter inputs" || bad "qty filter missing"
grep -q 'epc_filter_term_min' /tmp/whg.html && ok "delivery filter inputs" || bad "delivery filter missing"
grep -q 'epc_filter_manufacturer_options' /tmp/whg.html && ok "brand filter box" || bad "brand filter missing"
grep -q '20260812-whgroup' /tmp/whg.html && ok "whgroup cache buster" || bad "stale assets (need republish)"

# Grouping markers (SSR may have one warehouse only; still expect group attrs/classes in markup or JS).
curl -sk -o /tmp/whg.js -w 'PARITY_HTTP=%{http_code}\n' --max-time 15 \
  "${BASE}/platform-assets/epc_warehouse_search_parity.js?v=20260812-whgroup" || true
grep -q 'fillRangeInputsFromOffers' /tmp/whg.js && ok "live parity range fill" || bad "stale parity.js"
grep -q 'normalizeWarehouseGroups\|epc-warehouse-subrow' /tmp/whg.html /tmp/whg.js \
  && ok "warehouse group wiring present" || bad "warehouse group wiring missing"

# If multiple offer rows share article, at least one subrow or group-hint should appear after paint.
python3 - <<'PY' && ok "offer rows present or polling shell" || bad "no offer table"
from pathlib import Path
import re
t=Path('/tmp/whg.html').read_text(errors='ignore')
assert 'all_table_products' in t
rows=len(re.findall(r'data-offer-key=', t))
print('offer_rows', rows)
# grouping attrs when SSR seeded multiple warehouses
sub=t.count('epc-warehouse-subrow')
hint=t.count('epc-warehouse-group-hint')
gk=t.count('data-group-key=')
print('subrows', sub, 'hints', hint, 'group_keys', gk)
if rows >= 2:
    assert gk >= 2 or sub >= 1 or hint >= 1, 'expected group markup when 2+ offers'
PY

note "WHG_SUMMARY pass=${pass} fail=${fail}"
[[ "$fail" -eq 0 ]] || die "warehouse group prove failed"
note "RESULT=PASS EPARTSCART_CHPU_WH_GROUP"
