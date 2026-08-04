#!/usr/bin/env bash
# Probe Wave B write dry-run endpoints: always assert writes=0 / cutoverAllowed=false.
# Does not invent RELEASE_OWNER_APPROVAL.md. Live PHP ajax remains authoritative.
set -euo pipefail

BASE="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"
COOKIE="${ECOMAE_CUSTOMER_COOKIE:-}"
ADMIN_COOKIE="${ECOMAE_ADMIN_COOKIE:-}"

pass=0
fail=0

probe_post() {
  local path="$1"
  local body="$2"
  local cookie="$3"
  local label="$4"
  local hdr=()
  if [[ -n "$cookie" ]]; then
    hdr=(-H "Cookie: $cookie")
  fi
  local tmp
  tmp="$(mktemp)"
  local code
  code="$(curl -sS -o "$tmp" -w '%{http_code}' -X POST \
    -H 'Content-Type: application/json' \
    "${hdr[@]}" \
    -d "$body" \
    "${BASE}${path}" || true)"
  if [[ "$code" != "200" && "$code" != "401" ]]; then
    echo "FAIL $label: HTTP $code"
    fail=$((fail + 1))
    rm -f "$tmp"
    return
  fi
  if [[ "$code" == "401" ]]; then
    echo "PASS $label: unauthorized without/invalid session (expected gate)"
    pass=$((pass + 1))
    rm -f "$tmp"
    return
  fi
  python3 - "$tmp" "$label" <<'PY'
import json, sys
path, label = sys.argv[1], sys.argv[2]
doc = json.loads(open(path, encoding="utf-8").read())
writes = doc.get("writes")
blocked = doc.get("writesBlocked")
cutover = doc.get("cutoverAllowed")
status = doc.get("status")
if writes != 0 or blocked is not True or cutover is True:
    raise SystemExit(f"FAIL {label}: writes={writes} writesBlocked={blocked} cutoverAllowed={cutover} status={status}")
print(f"PASS {label}: status={status} writes=0 writesBlocked=true cutoverAllowed=false")
PY
  if [[ $? -eq 0 ]]; then
    pass=$((pass + 1))
  else
    fail=$((fail + 1))
  fi
  rm -f "$tmp"
}

echo "Wave B write dry-run probe against ${BASE}"

probe_post "/storefront/cart/change-count-need" '{"id":1,"countNeed":2,"confirmWrites":false}' "$COOKIE" "cart change-count-need"
probe_post "/storefront/cart/change-count-need" '{"id":1,"countNeed":2,"confirmWrites":true}' "$COOKIE" "cart change-count-need confirm refuse"
probe_post "/storefront/cart/check-for-order" '{"records":[1],"confirmWrites":false}' "$COOKIE" "cart check-for-order"
probe_post "/storefront/cart/delete" '{"recordsToDel":[1],"confirmWrites":false}' "$COOKIE" "cart delete"
probe_post "/cp/orders/set-item-status" '{"orderId":1,"itemId":1,"status":2,"confirmWrites":false}' "$ADMIN_COOKIE" "oms set-item-status"
probe_post "/cp/orders/set-items-status" '{"orderId":1,"status":2,"itemIds":[1,2],"confirmWrites":false}' "$ADMIN_COOKIE" "oms set-items-status"
probe_post "/erp/cash-entries/amend" '{"entryId":1,"reference":"dry-run","note":"wave-b","confirmWrites":false}' "$ADMIN_COOKIE" "erp cash-entries amend"
probe_post "/erp/cash-entries/void" '{"entryId":1,"reason":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp cash-entries void"
probe_post "/erp/gl-journals/manual" '{"lines":[{"coaId":1,"debit":10,"credit":0},{"coaId":2,"debit":0,"credit":10}],"reference":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp gl-journals manual"
probe_post "/storefront/cart/add" '{"productType":2,"manufacturer":"Bosch","article":"0986","countNeed":1,"price":12,"confirmWrites":false}' "$COOKIE" "cart add type-2"
probe_post "/cp/orders/send-message" '{"orderId":1,"text":"dry-run","itemId":0,"confirmWrites":false}' "$ADMIN_COOKIE" "oms send-message"
probe_post "/erp/gl-journals/reverse" '{"journalId":1,"note":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp gl-journals reverse"
probe_post "/erp/purchases/void" '{"purchaseId":1,"reason":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp purchases void"

echo "PASS=${pass} FAIL=${fail}"
[[ "$fail" -eq 0 ]]
