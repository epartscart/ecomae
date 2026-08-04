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

# Fail fast if ASP.NET is down — avoids 100+ identical HTTP 404 noise.
health_code="$(curl -sS -m 5 -o /tmp/ecomae-write-dryrun-health.txt -w '%{http_code}' "${BASE}/health" 2>/dev/null || true)"
if [[ "$health_code" != "200" ]]; then
  echo "FAIL aspnet /health: HTTP ${health_code:-000} (refusing to run write dry-run probes)" >&2
  echo "ASP.NET loopback is not ready at ${BASE}." >&2
  echo "Recover on CloudPanel:" >&2
  echo "  systemctl status ecomae-platform.service --no-pager" >&2
  echo "  journalctl -u ecomae-platform.service -n 80 --no-pager" >&2
  echo "  cd /opt/ecomae-aspnet-source && git fetch origin main && git reset --hard origin/main" >&2
  echo "  ECOMAE_BRANCH=main ECOMAE_RUN_SYSTEMD=1 bash scripts/cloudpanel_production_deploy_foundation.sh" >&2
  echo "  bash scripts/wait_for_aspnet_health.sh" >&2
  echo "  curl -i ${BASE}/health" >&2
  echo "Public /migration/* JSON needs diagnostics nginx after loopback is healthy:" >&2
  echo "  ECOMAE_INSTALL_DIAGNOSTICS_NGINX=1 bash scripts/cloudpanel_production_deploy_foundation.sh" >&2
  exit 1
fi
echo "OK   ${BASE}/health HTTP 200 — probing write dry-runs"

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
probe_post "/storefront/quotes/add-manual" '{"manufacturer":"Bosch","article":"0986","countNeed":1,"confirmWrites":false}' "$COOKIE" "quote add-manual"
probe_post "/storefront/garage/delete" '{"carId":1,"confirmWrites":false}' "$COOKIE" "garage delete"
probe_post "/storefront/garage/check-car" '{"carId":1,"orderId":1,"confirmWrites":false}' "$COOKIE" "garage check-car"
probe_post "/storefront/checkout/create" '{"howGetMode":1,"officeId":1,"confirmWrites":false}' "$COOKIE" "checkout create"
probe_post "/cp/orders/update-item" '{"orderId":1,"itemId":1,"price":12,"countNeed":2,"manufacturer":"Bosch","article":"0986","confirmWrites":false}' "$ADMIN_COOKIE" "oms update-item"
probe_post "/cp/orders/pay-refund" '{"orderId":1,"directRefund":true,"confirmWrites":false}' "$ADMIN_COOKIE" "oms pay-refund"
probe_post "/cp/orders/fulfillment-set-stage" '{"orderId":1,"supplierKey":"wh:1","stage":"packing","confirmWrites":false}' "$ADMIN_COOKIE" "oms fulfillment-set-stage"
probe_post "/cp/orders/fulfillment-advance" '{"orderId":1,"supplierKey":"wh:1","confirmWrites":false}' "$ADMIN_COOKIE" "oms fulfillment-advance"
probe_post "/erp/purchases/amend" '{"purchaseId":1,"invoiceNumber":"dry-run","note":"wave-b","confirmWrites":false}' "$ADMIN_COOKIE" "erp purchase amend"
probe_post "/erp/sales-orders/delete" '{"salesOrderId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp sales-orders delete"
probe_post "/erp/customers/master-save" '{"customerId":1,"customerName":"Dry-Run","creditLimit":1000,"termsDays":30,"confirmWrites":false}' "$ADMIN_COOKIE" "erp customer master-save"
probe_post "/cp/orders/refresh-item-cost" '{"orderId":1,"itemId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "oms refresh-item-cost"
probe_post "/erp/purchases/from-order" '{"orderId":1,"supplierId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp purchase from-order"
probe_post "/erp/currency/set-rate" '{"from":"USD","to":"AED","rate":3.67,"confirmWrites":false}' "$ADMIN_COOKIE" "erp currency set-rate"
probe_post "/erp/periods/soft-close" '{"yearMonth":"2026-08","note":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp period soft-close"
probe_post "/erp/periods/lock" '{"yearMonth":"2026-08","note":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp period lock"
probe_post "/erp/customers/settlement" '{"userId":1,"amount":10,"direction":"credit","entryKind":"adjustment","confirmWrites":false}' "$ADMIN_COOKIE" "erp customer settlement"
probe_post "/erp/suppliers/settlement" '{"supplierId":1,"amount":10,"direction":"decrease","confirmWrites":false}' "$ADMIN_COOKIE" "erp supplier settlement"
probe_post "/erp/periods/reopen" '{"yearMonth":"2026-08","note":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp period reopen"
probe_post "/erp/purchases/adjust" '{"purchaseId":1,"deltaExVat":-5,"note":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp purchase adjust"
probe_post "/erp/orders/settlement" '{"orderId":1,"amount":10,"direction":"credit","confirmWrites":false}' "$ADMIN_COOKIE" "erp order settlement"
probe_post "/erp/suppliers/sync" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp suppliers sync"
probe_post "/erp/gl-journals/post-sales" '{"dateFromUnix":1,"dateToUnix":2,"confirmWrites":false}' "$ADMIN_COOKIE" "erp gl post-sales"
probe_post "/erp/gl-journals/sync-unposted" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp gl sync-unposted"
probe_post "/erp/workflow/status" '{"taskId":1,"status":"done","confirmWrites":false}' "$ADMIN_COOKIE" "erp workflow status"
probe_post "/erp/marketing/create" '{"name":"Dry-Run Campaign","confirmWrites":false}' "$ADMIN_COOKIE" "erp marketing create"
probe_post "/erp/subscriptions/save" '{"code":"SUB-1","customer":"Acme","confirmWrites":false}' "$ADMIN_COOKIE" "erp subscription save"
probe_post "/erp/contracts/save" '{"code":"CTR-1","title":"MSA","confirmWrites":false}' "$ADMIN_COOKIE" "erp contract save"
probe_post "/erp/wms/receive" '{"item":"SKU-1","qty":1,"receiveLocationId":1,"putawayLocationId":2,"confirmWrites":false}' "$ADMIN_COOKIE" "erp wms receive"
probe_post "/erp/wms/locations/save" '{"code":"A-01-01","confirmWrites":false}' "$ADMIN_COOKIE" "erp wms location save"
probe_post "/erp/collections/cases/save" '{"customerId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp collections case save"
probe_post "/erp/procurement/requisitions/save" '{"requester":"buyer@ecom.ae","confirmWrites":false}' "$ADMIN_COOKIE" "erp procurement req save"
probe_post "/erp/wms/waves/create" '{"item":"SKU-1","qty":1,"reference":"W1","confirmWrites":false}' "$ADMIN_COOKIE" "erp wms wave create"
probe_post "/erp/wms/waves/release" '{"id":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp wms wave release"
probe_post "/erp/wms/work/complete" '{"id":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp wms work complete"
probe_post "/erp/subscriptions/status" '{"id":1,"status":"active","confirmWrites":false}' "$ADMIN_COOKIE" "erp subscription status"
probe_post "/erp/collections/cases/status" '{"id":1,"status":"open","confirmWrites":false}' "$ADMIN_COOKIE" "erp collections case status"
probe_post "/erp/procurement/requisitions/submit" '{"id":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp procurement req submit"
probe_post "/erp/procurement/requisitions/decision" '{"id":1,"approve":true,"confirmWrites":false}' "$ADMIN_COOKIE" "erp procurement req decision"
probe_post "/erp/wms/locations/delete" '{"id":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp wms location delete"
probe_post "/erp/fin/periods/status" '{"fy":2026,"periodNo":8,"status":"open","confirmWrites":false}' "$ADMIN_COOKIE" "erp fin period status"
probe_post "/erp/workflow/create" '{"title":"Dry-run task","departmentCode":"admin","priority":"normal","confirmWrites":false}' "$ADMIN_COOKIE" "erp workflow create"
probe_post "/erp/fiscal/set-lock" '{"lockDateUnix":0,"note":"dry-run clear","confirmWrites":false}' "$ADMIN_COOKIE" "erp fiscal set-lock"
probe_post "/erp/aftersales/rma-create" '{"customerId":1,"sourceId":1,"reason":"dry-run","lines":[{"itemId":1,"qty":1,"unitPrice":10}],"confirmWrites":false}' "$ADMIN_COOKIE" "erp aftersales rma-create"
probe_post "/erp/on-premises/health-dry-run" '{"licenseKey":"DEMO-KEY-XXXX","status":"ok","confirmWrites":false}' "" "on-premises health"
probe_post "/erp/on-premises/license-activate-dry-run" '{"licenseKey":"LIC-2026-ABCD-EFGH","fingerprint":"fp-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","hostname":"onprem-demo","confirmWrites":false}' "" "on-premises license activate"
probe_post "/erp/sales-orders/cancel" '{"salesOrderId":1,"reason":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "erp sales-orders cancel"
probe_post "/cp/orders/delete" '{"orderIds":[1],"confirmWrites":false}' "$ADMIN_COOKIE" "oms delete-orders"
probe_post "/erp/purchase-orders/delete" '{"purchaseOrderId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp purchase-orders delete"


# Wave B ajax_erp registry + high-value dedicated (catalog long-tail; PHP authoritative)
probe_post "/erp/ajax-writes/dry-run/edit_lock_acquire" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp ajax registry edit_lock_acquire"
probe_post "/erp/ajax-writes/dry-run/bos_wf_decide" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp ajax registry bos_wf_decide"
probe_post "/erp/ajax-writes/dry-run/agenda_save" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp ajax registry agenda_save"
probe_post "/erp/on-premises/setup-wizard-dry-run" '{"tenantCode":"demo","confirmWrites":false}' "" "on-premises setup-wizard"
probe_post "/erp/on-premises/backup-dry-run" '{"label":"dry-run","confirmWrites":false}' "" "on-premises backup"
probe_post "/erp/ajax/edit-lock-acquire" '{"resourceKey":"so:1","confirmWrites":false}' "$ADMIN_COOKIE" "erp edit_lock_acquire"
probe_post "/erp/ajax/bos-wf-decide" '{"id":1,"approve":true,"confirmWrites":false}' "$ADMIN_COOKIE" "erp bos_wf_decide"
probe_post "/erp/ajax/bos-compliance-file" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp bos_compliance_file"
probe_post "/erp/ajax/opl-params-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp opl_params_save"
probe_post "/erp/ajax/pf-case-start" '{"id":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp pf_case_start"
probe_post "/erp/ajax/sub-generate" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp sub_generate"
probe_post "/erp/ajax/coll-dunning-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp coll_dunning_run"
probe_post "/erp/ajax/proc-req-convert" '{"id":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp proc_req_convert"
probe_post "/erp/ajax/bank-reconcile" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp bank_reconcile"
probe_post "/erp/ajax/aml-kyc-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp aml_kyc_save"
probe_post "/erp/ajax/bplan-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp bplan_save"
probe_post "/erp/ajax/supplier-payment" '{"id":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp supplier_payment"
probe_post "/erp/ajax/fx-post-revaluation" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp fx_post_revaluation"
probe_post "/erp/ajax/presence-heartbeat" '{"resourceKey":"so:1","confirmWrites":false}' "$ADMIN_COOKIE" "erp presence_heartbeat"
probe_post "/erp/ajax/bos-intel-toggle-control" '{"controlKey":"vat","enabled":true,"confirmWrites":false}' "$ADMIN_COOKIE" "erp bos_intel_toggle_control"


# Wave B promote inv/hr/einvoice/mfg + storefront/CP leftovers
probe_post "/erp/ajax/inv-sync-warehouses" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp inv_sync_warehouses"
probe_post "/erp/ajax/hr-emp-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp hr_emp_save"
probe_post "/erp/ajax/einvoice-create" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp einvoice_create"
probe_post "/erp/ajax/order-fulfillment-bootstrap" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp order_fulfillment_bootstrap"
probe_post "/erp/ajax/mfgr-mrp-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp mfgr_mrp_run"
probe_post "/erp/ajax/qm-plan-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp qm_plan_save"
probe_post "/erp/ajax/rbac-priv-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp rbac_priv_save"
probe_post "/erp/ajax/pm-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp pm_save"
probe_post "/erp/ajax/hr-leave-status" '{"id":1,"targetStatus":"open","confirmWrites":false}' "$ADMIN_COOKIE" "erp hr_leave_status"
probe_post "/erp/ajax/inv-create-item" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp inv_create_item"
probe_post "/storefront/newsletter/subscribe" '{"email":"dry-run","confirmWrites":false}' "" "StorefrontNewsletterSubscribe"
probe_post "/storefront/evaluations/add" '{"productId":1,"5":5,"confirmWrites":false}' "$ADMIN_COOKIE" "StorefrontAddEvaluation"
probe_post "/storefront/finance/create-operation" '{"amount":1,"kind":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "StorefrontCreateOperation"
probe_post "/storefront/orders/check-not-authorized" '{"orderId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "StorefrontCheckOrderNotAuthorized"
probe_post "/storefront/users/set-option" '{"optionKey":"dry-run","optionValue":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "StorefrontSetUserOption"
probe_post "/storefront/geo/set-my-city" '{"cityId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "StorefrontSetMyCity"
probe_post "/storefront/login/send-code" '{"phone":"dry-run","confirmWrites":false}' "" "StorefrontLoginSendCode"
probe_post "/storefront/login/check-code" '{"code":"dry-run","confirmWrites":false}' "" "StorefrontLoginCheckCode"
probe_post "/cp/returns/action" '{"returnId":1,"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpReturnAction"
probe_post "/cp/requests/set-vin-viewed" '{"requestId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "CpSetUsersVinViewed"
probe_post "/cp/users/set-comment" '{"userId":1,"comment":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpSetUserComment"
probe_post "/cp/prices/import-csv" '{"sessionId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "CpPricesImportCsv"
probe_post "/cp/prices/complete-session" '{"sessionId":1,"confirmWrites":false}' "$ADMIN_COOKIE" "CpPricesCompleteSession"


# Wave B promote ins/fin/automation + CP leftover ajax
probe_post "/erp/ajax/ins-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp ins_save"
probe_post "/erp/ajax/docx-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp docx_save"
probe_post "/erp/ajax/fin-alloc-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp fin_alloc_run"
probe_post "/erp/ajax/pf-case-cancel" '{"id":1,"confirmWrites":false}' "$ADMIN_COOKIE" "erp pf_case_cancel"
probe_post "/erp/ajax/opl-autoplan" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp opl_autoplan"
probe_post "/cp/content/create-sitemap" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpCreateSitemap"
probe_post "/cp/lang/save-translation" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpLangSaveTranslation"
probe_post "/cp/lang/save-description" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpLangSaveDescription"
probe_post "/cp/channels/write" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpChannelsWrite"
probe_post "/cp/logistics/write" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpLogisticsWrite"
probe_post "/cp/payments/write" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpPaymentsWrite"
probe_post "/cp/lang/create-string" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpLangCreateString"
probe_post "/cp/lang/delete-not-used" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpLangDeleteNotUsed"
probe_post "/cp/packs/delete" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpPacksDelete"


# Wave B final registry→dedicated closeout samples
probe_post "/erp/ajax/concurrency-status" '{"id":1,"targetStatus":"open","confirmWrites":false}' "$ADMIN_COOKIE" "erp concurrency_status"
probe_post "/erp/ajax/settlement-open-docs" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp settlement_open_docs"
probe_post "/erp/ajax/dashboard" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp dashboard"
probe_post "/erp/ajax/command-center" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp command_center"
probe_post "/erp/ajax/cc-kpi-tiles" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp cc_kpi_tiles"
probe_post "/erp/ajax/cc-approval-queue" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp cc_approval_queue"
probe_post "/erp/ajax/period-list" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp period_list"
probe_post "/erp/ajax/period-checklist" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp period_checklist"
probe_post "/erp/ajax/automation-install-template" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp automation_install_template"
probe_post "/erp/ajax/automation-enable-category" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp automation_enable_category"
probe_post "/erp/ajax/automation-tick" '{"confirmWrites":false}' "$ADMIN_COOKIE" "erp automation_tick"
probe_post "/erp/ajax/tenant-config-save" '{"id":0,"code":"DRY","confirmWrites":false}' "$ADMIN_COOKIE" "erp tenant_config_save"
probe_post "/cp/lang/set-is-custom" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpLangSetIsCustom"
probe_post "/cp/lang/set-is-error" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpLangSetIsError"
probe_post "/cp/lang/set-same" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpLangSetSame"
probe_post "/cp/lang/set-used-found" '{"action":"dry-run","confirmWrites":false}' "$ADMIN_COOKIE" "CpLangSetUsedFound"


# Wave B BOS ajax + POS/portal/on-prem pack
probe_post "/bos/ajax-writes/dry-run/save" '{"confirmWrites":false}' "$ADMIN_COOKIE" "bos ajax registry save"
probe_post "/cp/pos/complete-sale" '{"action":"complete_sale","confirmWrites":false}' "$ADMIN_COOKIE" "cp pos complete-sale"
probe_post "/cp/portal/save-settings" '{"action":"save_settings","confirmWrites":false}' "$ADMIN_COOKIE" "cp portal save-settings"
probe_post "/cp/crm/action" '{"action":"crm_save","confirmWrites":false}' "$ADMIN_COOKIE" "cp crm action"
probe_post "/erp/on-premises/activate-license-cli-dry-run" '{"action":"activate","confirmWrites":false}' "" "on-premises activate-license cli"
probe_post "/erp/on-premises/health-check-pack-dry-run" '{"action":"health","confirmWrites":false}' "" "on-premises health-check pack"

# Wave C CP module ajax catalogs
probe_post "/cp/module-ajax/dry-run/procurement/create_supplier" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax registry create_supplier"
probe_post "/cp/module-ajax/procurement/create_supplier/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated create_supplier"
probe_post "/cp/module-ajax/dry-run/crm/crm_save_lead" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax registry crm_save_lead"
probe_post "/cp/module-ajax/crm/crm_save_lead/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated crm_save_lead"
probe_post "/cp/module-ajax/document_control/save_company/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated save_company"
probe_post "/cp/module-ajax/auto_price/bulk_approve/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated bulk_approve"
probe_post "/cp/module-ajax/bulk_upload/process_upload/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated process_upload"
probe_post "/cp/module-ajax/marketing/save_kpi/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated save_kpi"
probe_post "/cp/module-ajax/prices_upload/ajax_1_prepare_tmp_dir/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated prices_upload ajax_1"
probe_post "/cp/module-ajax/portal_integrations/save_mobile/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated save_mobile"
probe_post "/cp/module-ajax/sku_media/upload_photo/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated upload_photo"
probe_post "/cp/module-ajax/classic_form/shop_catalogue_product/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp classic form product.php"
probe_post "/cp/module-ajax/classic_form/users_user/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp classic form user.php"
probe_post "/cp/module-ajax/parts_agent/save_config/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated parts_agent save_config"
probe_post "/cp/module-ajax/free_tools/register/dry-run" '{"confirmWrites":false}' "" "cp module ajax dedicated free_tools register"
probe_post "/cp/module-ajax/garage_manager/create_job/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated garage_manager create_job"
probe_post "/cp/module-ajax/currency_live_rates/apply/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated currency_live_rates apply"
probe_post "/cp/module-ajax/multivendor_ingest/vendor_code_save/dry-run" '{"confirmWrites":false}' "$ADMIN_COOKIE" "cp module ajax dedicated multivendor vendor_code_save"

echo "PASS=${pass} FAIL=${fail}"
[[ "$fail" -eq 0 ]]
