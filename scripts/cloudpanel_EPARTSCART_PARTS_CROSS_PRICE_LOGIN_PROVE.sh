#!/usr/bin/env bash
# Prove AISIN/DT068 crossbase rows surface + logged-in price unmask markers are live.
# Optional: set ECOMAE_LOGIN_PASSWORD (+ ECOMAE_LOGIN_CONTACT) to POST /storefront/login
# and assert /storefront/search prices_visible=true (not guest **).
set -euo pipefail
HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
UA="${ECOMAE_CURL_UA:-Mozilla/5.0 (compatible; EcomAE-PartsCrossPriceProve/1.0)}"
CONTACT="${ECOMAE_LOGIN_CONTACT:-taxofin2025@gmail.com}"
PASSWORD="${ECOMAE_LOGIN_PASSWORD:-}"
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

# Brandless CHPU must not 502 (302→home or brand canonical is OK).
BRANDLESS_CODE=$(curl -sS -A "$UA" -o /dev/null -w '%{http_code}' --max-time 25 "${BASE}/en/parts/DT068" || echo 000)
note "CHPU_BRANDLESS_HTTP=${BRANDLESS_CODE}"
[[ "$BRANDLESS_CODE" != "502" && "$BRANDLESS_CODE" != "500" ]] && ok "CHPU_NO_502 brandless=${BRANDLESS_CODE}" || fail "CHPU_502 brandless=${BRANDLESS_CODE}"

has 'x-ecomae-platform:[[:space:]]*primary' /tmp/epc_dt068.hdr && ok "PRIMARY=YES" || fail "PRIMARY=NO"
has 'epc-cross-ref-list' /tmp/epc_dt068.html && ok "CROSS_LIST=YES" || fail "CROSS_LIST=NO"
has 'include_crossbase=1' /tmp/epc_dt068.html && ok "CLIENT_CROSSBASE=YES" || fail "CLIENT_CROSSBASE=NO"
has 'prices_visible === true' /tmp/epc_dt068.html && ok "PRICE_UNLATCH=YES" || fail "PRICE_UNLATCH=NO"
has '__epcPriceUnmaskRepoll' /tmp/epc_dt068.html && ok "PRICE_REPOLL=YES" || fail "PRICE_REPOLL=NO"
has 'data-prices-visible="0"' /tmp/epc_dt068.html && ok "GUEST_MASK_SSR=YES" || fail "GUEST_MASK_SSR=NO"

# Crossbase merge must reserve slots — unique source=crossbase OR crossbase_count>0 with merge source.
curl -sS -A "$UA" -o /tmp/epc_dt068_cross.json -w 'CROSS_HTTP=%{http_code} TTFB=%{time_starttransfer}\n' --max-time 25 \
  "${BASE}/storefront/cross-search?article=DT068&brand=AISIN&limit=80&include_crossbase=1" || true
[[ "$(curl -sS -A "$UA" -o /dev/null -w '%{http_code}' --max-time 20 "${BASE}/storefront/cross-search?article=DT068&include_crossbase=1" || echo 000)" != "502" ]] \
  && ok "CROSS_NO_502=YES" || fail "CROSS_NO_502=NO"
has '"status"[[:space:]]*:[[:space:]]*true' /tmp/epc_dt068_cross.json && ok "CROSS_STATUS=YES" || fail "CROSS_STATUS=NO"
python3 - <<'PY' || fail "CROSS_MERGE=NO"
import json
d=json.load(open('/tmp/epc_dt068_cross.json'))
refs=d.get('references') or []
cb_rows=[r for r in refs if str(r.get('source') or '').lower()=='crossbase']
local=int(d.get('local_count') or 0)
cb=int(d.get('crossbase_count') or 0)
src=str(d.get('source') or '')
msg=str(d.get('message') or '')
print(f'local={local} crossbase_count={cb} crossbase_rows={len(cb_rows)} refs={len(refs)} source={src}')
if 'Access denied' in msg or src == 'database-error':
    raise SystemExit('database-error (GRANT ecomae→docpart missing): ' + msg)
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

# Optional real login — proves price/term unmask (not **).
if [[ -n "$PASSWORD" ]]; then
  note "---- LOGIN unmask (contact=${CONTACT}) ----"
  COOKIE_JAR=/tmp/epc_dt068_login.jar
  rm -f "$COOKIE_JAR"
  curl -sS -A "$UA" -c "$COOKIE_JAR" -b "$COOKIE_JAR" -o /tmp/epc_login_post.html -D /tmp/epc_login_post.hdr \
    -w 'LOGIN_HTTP=%{http_code} REDIR=%{redirect_url}\n' --max-time 30 \
    -X POST "${BASE}/storefront/login" \
    -H 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'surface=storefront' \
    --data-urlencode 'redirect=/en/parts/AISIN/DT068' \
    --data-urlencode 'contact_type=email' \
    --data-urlencode "contact=${CONTACT}" \
    --data-urlencode "password=${PASSWORD}" || true
  tr -d '\r' < /tmp/epc_login_post.hdr > /tmp/epc_login_post.hdr.c 2>/dev/null || true
  mv -f /tmp/epc_login_post.hdr.c /tmp/epc_login_post.hdr 2>/dev/null || true
  if grep -Eiq 'invalid_credentials|bridge_not_configured|tenant_db_unbound' /tmp/epc_login_post.hdr /tmp/epc_login_post.html; then
    fail "LOGIN=NO (see LOGIN_HTTP / redirect)"
  else
    ok "LOGIN_POST_OK"
  fi
  curl -sS -A "$UA" -b "$COOKIE_JAR" -c "$COOKIE_JAR" -o /tmp/epc_dt068_authed_search.json \
    -w 'AUTH_SEARCH_HTTP=%{http_code}\n' --max-time 30 \
    "${BASE}/storefront/search?article=DT068&brand=AISIN" || true
  python3 - <<'PY' || fail "LOGIN_PRICE_UNMASK=NO"
import json
d=json.load(open('/tmp/epc_dt068_authed_search.json'))
vis=bool(d.get('prices_visible'))
state=str(d.get('access_state') or '')
rows=d.get('rows') or []
prices=[r.get('price') for r in rows if isinstance(r, dict)]
print(f'prices_visible={vis} access_state={state} rows={len(rows)} sample_prices={prices[:5]}')
assert vis is True, f'expected prices_visible true, got {vis} state={state}'
# At least one numeric price or non-empty term/exist when rows present.
if rows:
    numeric=any(isinstance(p,(int,float)) and float(p)>0 for p in prices)
    assert numeric or any(str(r.get('timeToExe') or '').strip() for r in rows if isinstance(r,dict)), 'no unmasked price/term'
print('GATE_OK LOGIN_PRICE_UNMASK=YES')
PY
  curl -sS -A "$UA" -b "$COOKIE_JAR" -o /tmp/epc_dt068_authed.html -D /tmp/epc_dt068_authed.hdr \
    -w 'AUTH_CHPU_HTTP=%{http_code}\n' --max-time 45 \
    "${BASE}/en/parts/AISIN/DT068" || true
  if grep -Eiq 'data-prices-visible="1"' /tmp/epc_dt068_authed.html; then
    ok "AUTH_SSR_PRICES_VISIBLE=1"
  else
    # Cookie session may still paint via JS repoll; search API gate above is authoritative.
    note "AUTH_SSR_PRICES_VISIBLE soft (rely on search API)"
  fi
else
  note "LOGIN_UNMASK_SKIP set ECOMAE_LOGIN_PASSWORD to prove live price/term unmask"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS EPARTSCART_PARTS_CROSS_PRICE_LOGIN=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD"
exit 1
