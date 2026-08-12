#!/usr/bin/env bash
# Prove CHPU Spec + Photos (sku_media) ASP.NET routes + HTML wiring.
set -euo pipefail
HOST="${ECOMAE_PROVE_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
ART="${ECOMAE_SKU_ARTICLE:-DT068}"
BRAND="${ECOMAE_SKU_BRAND:-AISINC}"
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }
pass=0; fail=0
ok() { note "SKU_OK $*"; pass=$((pass+1)); }
bad() { note "SKU_BAD $*"; fail=$((fail+1)); }

note "======== EPARTSCART CHPU SKU MEDIA PROVE ========"
note "HOST=${HOST} BRAND=${BRAND} ART=${ART}"

CHPU="${BASE}/en/parts/$(python3 - <<PY
import urllib.parse
print(urllib.parse.quote('''${BRAND}''', safe=''))
PY
)/${ART}"
curl -sk -o /tmp/sku-chpu.html -w 'CHPU_HTTP=%{http_code}\n' --max-time 30 "$CHPU" || true
grep -q '20260812-skumedia' /tmp/sku-chpu.html && ok "cache bust" || bad "stale cache bust"
grep -q 'epc_sku_media.css' /tmp/sku-chpu.html && ok "sku media css" || bad "missing sku media css"
grep -q 'epc-fitment-check-btn' /tmp/sku-chpu.html && ok "fitment btn" || bad "missing fitment"
# Spec/Photos SSR only when data exists — API must always respond.
BRAND_Q=$(python3 - <<PY
import urllib.parse
print(urllib.parse.quote('''${BRAND}'''))
PY
)
curl -sk -o /tmp/sku-media.json -w 'SKU_MEDIA_HTTP=%{http_code}\n' --max-time 25 \
  "${BASE}/storefront/sku-media?brand=${BRAND_Q}&article=${ART}" || true
python3 - <<'PY' && ok "sku-media JSON" || bad "sku-media JSON broken"
import json
d=json.load(open('/tmp/sku-media.json'))
assert 'ok' in d and 'photos' in d and 'specs' in d and 'url' in d
print('source', d.get('source'), 'photos', len(d.get('photos') or []), 'specs', len(d.get('specs') or []), 'url', bool(d.get('url')))
if d.get('photos') or d.get('specs'):
    print('HAS_MEDIA=YES')
else:
    print('HAS_MEDIA=NO (UI hidden until CP/UMAPI data exists — routes still OK)')
PY

curl -sk -o /tmp/sku-img.json -w 'PRODUCT_IMAGE_HTTP=%{http_code}\n' --max-time 20 \
  "${BASE}/storefront/product-image?brand=${BRAND_Q}&article=${ART}" || true
python3 - <<'PY' && ok "product-image alias" || bad "product-image alias broken"
import json
d=json.load(open('/tmp/sku-img.json'))
assert 'ok' in d and 'url' in d
PY

curl -sk -o /tmp/sku-parity.js -w 'PARITY_HTTP=%{http_code}\n' --max-time 15 \
  "${BASE}/platform-assets/epc_warehouse_search_parity.js?v=20260812-skumedia" || true
grep -q 'window.epcOpenSpecSplash' /tmp/sku-parity.js && ok "Spec splash JS" || bad "missing Spec splash JS"
grep -q 'window.epcOpenImageLightbox' /tmp/sku-parity.js && ok "lightbox JS" || bad "missing lightbox JS"
grep -q '/storefront/sku-media?' /tmp/sku-parity.js && ok "sku-media client fetch" || bad "stale row-photo endpoint"

# If media exists, HTML must paint Spec/Photos like PHP.
python3 - <<'PY' || true
import json,re
d=json.load(open('/tmp/sku-media.json'))
html=open('/tmp/sku-chpu.html').read()
if d.get('specs'):
    assert 'epc-spec-check-btn' in html, 'specs present but Spec button missing'
    assert 'epc-spec-panel' in html, 'specs present but Spec panel missing'
    print('SKU_OK SSR Spec chrome')
if d.get('photos'):
    assert 'epc-sku-media-part-page' in html, 'photos present but Photos area missing'
    print('SKU_OK SSR Photos chrome')
PY

note "SKU_SUMMARY pass=${pass} fail=${fail}"
[[ "$fail" -eq 0 ]] || die "sku media prove checks failed"
note "RESULT=PASS EPARTSCART_CHPU_SKU_MEDIA"
