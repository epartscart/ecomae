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
probe_post "/erp/cash-entries/create" '{"accountId":1,"amount":10,"direction":true,"confirmWrites":false}' "$ADMIN_COOKIE" "erp cash-entries create"
probe_post "/erp/cash-entries/receipt-voucher" '{"userId":1,"accountId":1,"amount":25,"confirmWrites":false}' "$ADMIN_COOKIE" "erp receipt voucher"
probe_post "/erp/cash-entries/payment-voucher" '{"supplierId":1,"accountId":1,"amount":25,"confirmWrites":false}' "$ADMIN_COOKIE" "erp payment voucher"
probe_post "/erp/suppliers/create" '{"name":"Dry-Run Supplier","confirmWrites":false}' "$ADMIN_COOKIE" "erp supplier create"
probe_post "/erp/purchases/create" '{"supplierId":1,"amountExVat":100,"confirmWrites":false}' "$ADMIN_COOKIE" "erp purchase create"
probe_post "/erp/purchases/delete" '{"purchaseId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp purchase delete"
probe_post "/erp/invoices/delete" '{"invoiceId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp invoice delete"
probe_post "/erp/cash-accounts/create" '{"name":"Dry-Run Cash","accountType":"cash","confirmWrites":false}' "$ADMIN_COOKIE" "erp cash account create"
probe_post "/erp/coa-accounts/create" '{"code":"9999","name":"Dry-Run COA","accountType":"expense","confirmWrites":false}' "$ADMIN_COOKIE" "erp coa create"
probe_post "/cp/orders/update-items" '{"orderId":1,"items":[{"itemId":1,"price":12,"countNeed":2}],"confirmWrites":false}' "$ADMIN_COOKIE" "oms update-items"
probe_post "/erp/gl-journals/manual" '{"lines":[{"coaId":1,"debit":10,"credit":0},{"coaId":2,"debit":0,"credit":10}],"reference":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp gl-journals manual"
probe_post "/storefront/cart/add" '{"productType":2,"manufacturer":"Bosch","article":"0986","countNeed":1,"price":12,"confirmWrites":false}' "$COOKIE" "cart add type-2"
probe_post "/cp/orders/send-message" '{"orderId":1,"text":"dry-run","itemId":0,"confirmWrites":false}' "$ADMIN_COOKIE" "oms send-message"
probe_post "/cp/orders/set-courier" '{"orderId":1,"deliveryPrice":25,"country":"AE","confirmWrites":false}' "$ADMIN_COOKIE" "oms set-courier"
probe_post "/erp/gl-journals/reverse" '{"journalId":1,"note":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp gl-journals reverse"
probe_post "/erp/purchases/void" '{"purchaseId":1,"reason":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp purchases void"
probe_post "/erp/invoices/cancel" '{"invoiceId":1,"reason":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp invoices cancel"
probe_post "/storefront/garage/notepad-add" '{"garageId":1,"manufacturer":"Bosch","article":"0986","name":"Pad","exist":2,"price":12,"confirmWrites":false}' "$COOKIE" "garage notepad-add"
probe_post "/storefront/quotes/submit" '{"quoteId":1,"customerNote":"dry-run","confirmWrites":false}' "$COOKIE" "quote submit"
probe_post "/storefront/quotes/accept" '{"quoteId":1,"confirmWrites":false}' "$COOKIE" "quote accept"
probe_post "/storefront/garage/set-active" '{"carId":1,"confirmWrites":false}' "$COOKIE" "garage set-active"
probe_post "/cp/orders/add-comment" '{"orderId":1,"text":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "oms add-comment"
probe_post "/storefront/orders/send-message" '{"orderId":1,"text":"dry-run","confirmWrites":false}' "$COOKIE" "storefront order send-message"
probe_post "/cp/orders/set-viewed" '{"orderIds":[1],"viewedFlag":1,"confirmWrites":false}' "$ADMIN_COOKIE" "oms set-viewed"
probe_post "/storefront/quotes/add-item" '{"productType":2,"manufacturer":"Bosch","article":"0986","countNeed":1,"confirmWrites":false}' "$COOKIE" "quote add-item"
probe_post "/storefront/garage/delete" '{"carId":1,"confirmWrites":false}' "$COOKIE" "garage delete"
probe_post "/storefront/checkout/create" '{"howGetMode":1,"officeId":1,"confirmWrites":false}' "$COOKIE" "checkout create"
probe_post "/cp/orders/update-item" '{"orderId":1,"itemId":1,"price":12,"countNeed":2,"manufacturer":"Bosch","article":"0986","confirmWrites":false}' "$ADMIN_COOKIE" "oms update-item"
probe_post "/cp/orders/pay-refund" '{"orderId":1,"directRefund":true,"confirmWrites":false}' "$ADMIN_COOKIE" "oms pay-refund"
probe_post "/erp/on-premises/health-dry-run" '{"licenseKey":"DEMO-KEY-XXXX","status":"ok","confirmWrites":false}' "" "on-premises health"
probe_post "/erp/on-premises/license-activate-dry-run" '{"licenseKey":"LIC-2026-ABCD-EFGH","fingerprint":"fp-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","hostname":"onprem-demo","confirmWrites":false}' "" "on-premises license activate"
probe_post "/erp/sales-orders/cancel" '{"salesOrderId":1,"reason":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp sales-orders cancel"
probe_post "/cp/orders/delete" '{"orderIds":[1],"confirmWrites":false}' "$ADMIN_COOKIE" "oms delete-orders"
probe_post "/erp/purchase-orders/delete" '{"purchaseOrderId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp purchase-orders delete"

echo "PASS=${pass} FAIL=${fail}"
[[ "$fail" -eq 0 ]]
