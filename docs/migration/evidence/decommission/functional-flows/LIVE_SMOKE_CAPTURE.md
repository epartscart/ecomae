# Functional live-smoke capture runbook

Honest commands and artifacts required to flip each of the **7** `live-smoke/*.json` stubs from `status=blocked` → `status=captured`.

**Do not flip stubs without real artifacts.** Never invent `RELEASE_OWNER_APPROVAL.md`, `MODULE_FUNCTION_PARITY_PASS`, or `php-vs-aspnet-recheck status=pass`. Keep `cutoverAllowed=false`, `readyForPhpRemoval=false`, `aspNetInteractiveComplete=0`.

## Preconditions (all flows)

```bash
# ASP.NET healthy on loopback
bash scripts/wait_for_aspnet_health.sh

# Admin session for CP/ERP digests and ajax dry-runs
export ECOMAE_ADMIN_COOKIE='session=...; u_id=...'   # from CloudPanel login
# or
export ECOMAE_ADMIN_COOKIE_JAR=/root/ecomae-admin.cookies

# Optional customer session for storefront digests/checkout
export ECOMAE_CUSTOMER_COOKIE='session=...; u_id=...'
```

Record artifacts under `docs/migration/evidence/decommission/functional-flows/live-smoke/` and reference paths in each stub's `capturedEvidence` array.

---

## 1. `warehouse-search-offers`

**Goal:** Authenticated storefront search offers + ERP inventory-stock parity vs PHP warehouse listing.

```bash
# Digest dual-sample (migration baseline or live php-*)
bash scripts/cloudpanel_capture_digest_dual_samples.sh
python3 scripts/compare_digest_dual_samples.py --contract-only

# Hybrid UI floor (search + inventory-stock)
bash scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh
python3 scripts/compare_hybrid_ui_dual_samples.py --contract-only

# Live smoke probes (example — adjust host/cookies)
curl -sS -b "$ECOMAE_CUSTOMER_COOKIE" \
  'https://epartscart.com/storefront/search?q=filter&limit=5' \
  -o /tmp/smoke-storefront-search.html
curl -sS -b "$ECOMAE_ADMIN_COOKIE" \
  'http://127.0.0.1:5100/erp/inventory-stock?limit=5' \
  -o docs/migration/evidence/decommission/functional-flows/live-smoke/artifacts/warehouse-erp-inventory-stock.json
```

**Artifacts to attach in stub:**
- `surface-parity/samples/php-storefront-search.json` or live capture with non-empty offer rows
- `surface-parity/samples/php-erp-inventory-stock.json` or authenticated digest JSON
- HTML/JSON probe showing `part_search` / `ajax_getProductsOfBunch` field parity notes

---

## 2. `erp-external-report-fetch`

**Goal:** Named report fetch totals match PHP report-center; worker dry-runs stay `writes=0`.

```bash
bash scripts/cloudpanel_probe_write_dryruns.sh | tee /tmp/write-dryrun-probe.out
# ERP report dry-runs (writes=0)
curl -sS -X POST -H "Cookie: $ECOMAE_ADMIN_COOKIE" -H 'Content-Type: application/json' \
  -d '{"confirmWrites":false}' \
  http://127.0.0.1:5100/erp/ajax/report-center/sales/dry-run \
  -o docs/migration/evidence/decommission/functional-flows/live-smoke/artifacts/erp-report-sales-dryrun.json
```

**Artifacts:**
- Dry-run JSON for sales|inventory|vat|ar-aging with `writes=0`
- Side-by-side totals vs PHP `tests/erp_advanced/run_report_center_tests.php` output (saved text/JSON)
- `decommission/staging-smoke/surface-digests-aspnet.json` refresh if staging attached

---

## 3. `einvoice`

**Goal:** `/cp/einvoice-documents` digest parity + create→validate→submit→poll dry-run chain.

```bash
bash scripts/cloudpanel_capture_digest_dual_samples.sh
python3 scripts/compare_digest_dual_samples.py --contract-only --samples-dir docs/migration/evidence/surface-parity/samples

curl -sS -b "$ECOMAE_ADMIN_COOKIE" \
  'http://127.0.0.1:5100/cp/einvoice-documents?limit=5' \
  -o docs/migration/evidence/decommission/functional-flows/live-smoke/artifacts/cp-einvoice-documents-live.json

# Module-ajax / write dry-run chain (writes=0)
curl -sS -X POST -H "Cookie: $ECOMAE_ADMIN_COOKIE" -H 'Content-Type: application/json' \
  -d '{"confirmWrites":false}' \
  http://127.0.0.1:5100/erp/ajax/einvoice-create/dry-run \
  -o docs/migration/evidence/decommission/functional-flows/live-smoke/artifacts/erp-einvoice-create-dryrun.json
```

**Artifacts:**
- Authenticated einvoice digest JSON with `documents[]` field parity notes
- Dry-run sequence JSON files (each `writes=0`, `cutoverAllowed=false`)

---

## 4. `ct-catalog-umapi`

**Goal:** Auth catalog `connected=true`, miss→PHP UMAPI fill→ASP.NET cache hit dual-sample.

```bash
bash scripts/cloudpanel_capture_catalog_miss_dual_samples.sh
python3 scripts/compare_catalog_miss_dual_samples.py

bash scripts/cloudpanel_probe_catalog_miss_path.sh
bash scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh

# Staging smoke catalog status
curl -sS -H "X-Api-Key: $ECOMAE_CATALOG_API_KEY" \
  http://127.0.0.1:5100/api/v1/catalog/status \
  -o docs/migration/evidence/decommission/functional-flows/live-smoke/artifacts/catalog-status-live.json
```

**Artifacts:**
- `catalog-miss-umapi/compare-result.json` (green, cutover false)
- `decommission/staging-smoke/catalog-status-aspnet.json` with `connected=true`
- Miss-fill probe output showing PHP UMAPI fill then ASP.NET cache hit

---

## 5. `process-flow`

**Goal:** Process-flow task digest vs PHP `epc_erp_processflow_tasks`; industry step-chain parity.

```bash
bash scripts/cloudpanel_capture_digest_dual_samples.sh
curl -sS -b "$ECOMAE_ADMIN_COOKIE" \
  'http://127.0.0.1:5100/cp/workflows?limit=5' \
  -o docs/migration/evidence/decommission/functional-flows/live-smoke/artifacts/cp-workflows-live.json
```

**Artifacts:**
- Workflow/process-flow digest JSON with open/done/overdue + rows
- PHP CLI output from `tests/erp_advanced/run_flow_tests.php` saved under `live-smoke/artifacts/`

---

## 6. `oms-checkout`

**Goal:** Orders digest + checkout field parity; OMS ajax dry-runs paired with PHP.

```bash
bash scripts/cloudpanel_run_module_ajax_dual_sample_operator.sh
bash scripts/cloudpanel_capture_digest_dual_samples.sh

curl -sS -b "$ECOMAE_CUSTOMER_COOKIE" \
  'https://www.ecomae.com/storefront/checkout' \
  -o docs/migration/evidence/decommission/functional-flows/live-smoke/artifacts/storefront-checkout-live.html
curl -sS -b "$ECOMAE_ADMIN_COOKIE" \
  'http://127.0.0.1:5100/cp/orders-digest?limit=5' \
  -o docs/migration/evidence/decommission/functional-flows/live-smoke/artifacts/cp-orders-digest-live.json
```

**Artifacts:**
- Live `cp-orders-digest` + `storefront/checkout` captures
- Module-ajax pairs for `oms-set_item_status`, `oms-set_courier`, `oms-send_message` (live php-* or documented parity notes)
- Checkout path notes for how_get/confirm/payment (`writes=0` until approval)

---

## 7. `super-cp-tenant-control`

**Goal:** Super CP tenants + BOS fleet-health `sampleTenants`; tenant hosts still PHP chrome.

```bash
ECOMAE_TENANT_LIVE=1 bash scripts/cloudpanel_run_tenant_safety_operator.sh
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh

curl -sS -b "$ECOMAE_ADMIN_COOKIE" \
  'http://127.0.0.1:5100/bos/fleet-health?limit=5' \
  -o docs/migration/evidence/decommission/functional-flows/live-smoke/artifacts/bos-fleet-health-live.json
```

**Artifacts:**
- `tenant-safety/live-tenant-php-chrome.json` + `tenant-safety/same-to-same-verify.json` (live refresh)
- BOS fleet-health with non-empty `sampleTenants[]` for a live tenant
- Onboard/demo tenant smoke notes (isolated DB + tenant CP login)

---

## Flipping a stub (only after artifacts exist)

Edit `live-smoke/<flowId>.json`:

```json
{
  "status": "captured",
  "capturedEvidence": [
    "decommission/functional-flows/live-smoke/artifacts/<file>.json",
    "..."
  ],
  "capturedAtUnix": 1785867000,
  "cutoverAllowed": false,
  "readyForPhpRemoval": false,
  "aspNetInteractiveComplete": 0
}
```

Re-run suite (must still block PHP removal until human approval):

```bash
bash scripts/run_pre_decommission_functional_suite.sh
# or skip PHP CLI in CI:
ECOMAE_FUNC_SKIP_PHP=1 bash scripts/run_pre_decommission_functional_suite.sh
```

**PHP delete remains refused** until `ReadyToRemovePhp=true` on readiness checklist + `RELEASE_OWNER_APPROVAL.md`.
