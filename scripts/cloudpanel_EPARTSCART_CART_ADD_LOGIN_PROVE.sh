#!/usr/bin/env bash
# Prove cart-app login gate + /storefront/cart/add live write path (auth message PHP parity).
set -euo pipefail
HOST="${ECOMAE_PROVE_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }
pass=0; fail=0
ok() { note "CART_OK $*"; pass=$((pass+1)); }
bad() { note "CART_BAD $*"; fail=$((fail+1)); }

note "======== EPARTSCART CART ADD LOGIN PROVE ========"

curl -sk -o /tmp/cart-app.html -w 'CART_APP_HTTP=%{http_code}\n' --max-time 25 "${BASE}/storefront/cart-app" || true
grep -q 'Please log in or register to continue\|Your cart is empty\|Lines in cart\|epc-sf-cart' /tmp/cart-app.html \
  && ok "cart-app HTML shell" || bad "cart-app shell missing"

curl -sk -o /tmp/cart.json -w 'CART_JSON_HTTP=%{http_code}\n' --max-time 20 "${BASE}/storefront/cart" || true
# Anonymous must 401 with PHP-like message (not opaque dry-run wording).
python3 - <<'PY' && ok "anonymous cart digest auth message" || bad "anonymous cart digest auth message"
import json
d=json.load(open('/tmp/cart.json'))
assert d.get('ok') is False
msg=((d.get('error') or {}).get('message') or '')
assert 'log in' in msg.lower() or 'register' in msg.lower() or 'login' in msg.lower(), msg
print(msg)
PY

curl -sk -o /tmp/cart-add.json -w 'CART_ADD_HTTP=%{http_code}\n' --max-time 20 \
  -X POST -H 'Content-Type: application/json' \
  -d '{"productType":2,"manufacturer":"JS ASAKASHI","article":"C110J","countNeed":1,"price":1,"confirmWrites":true}' \
  "${BASE}/storefront/cart/add" || true
python3 - <<'PY' && ok "anonymous cart/add auth message" || bad "anonymous cart/add auth message"
import json
d=json.load(open('/tmp/cart-add.json'))
assert d.get('ok') is False
msg=((d.get('error') or {}).get('message') or d.get('message') or '')
assert 'log in' in msg.lower() or 'register' in msg.lower() or 'login' in msg.lower(), msg
print('add', msg)
PY

curl -sk -o /tmp/parity.js -w 'PARITY_HTTP=%{http_code}\n' --max-time 15 \
  "${BASE}/platform-assets/epc_warehouse_search_parity.js?v=20260812-cartadd" || true
grep -q 'cartErrorMessage' /tmp/parity.js && ok "live parity cartErrorMessage" || bad "stale parity.js"
grep -q 'confirmWrites: true' /tmp/parity.js && ok "live parity confirmWrites" || bad "parity missing confirmWrites"

note "CART_SUMMARY pass=${pass} fail=${fail}"
[[ "$fail" -eq 0 ]] || die "cart add login prove failed"
note "RESULT=PASS EPARTSCART_CART_ADD_LOGIN"
