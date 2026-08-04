# Pre-decommission functional flows

Named functional testing gate before PHP can be removed.

## Flows

1. **warehouse-search-offers** — epartscart warehouse stock / search offers  
2. **erp-external-report-fetch** — ERP external / tax / electronic report fetch  
3. **einvoice** — e-invoice documents + submit readiness  
4. **ct-catalog-umapi** — catalog/CT fetch + UMAPI miss-fill  
5. **process-flow** — process / workflow engine  
6. **oms-checkout** — OMS order system + storefront checkout  
7. **super-cp-tenant-control** — Super CP control of tenant CP / fleet / isolation  

## Run

```bash
bash scripts/run_pre_decommission_functional_suite.sh
# or skip PHP CLI when DB fixtures unavailable:
ECOMAE_FUNC_SKIP_PHP=1 bash scripts/run_pre_decommission_functional_suite.sh
```

## Built vs pending

See `built-vs-pending.json`.

- **Built:** static floors, migration samples, OMS dual-samples, catalog-miss UMAPI evidence for all 7 flows.
- **Pending (blocks PHP removal):** live CloudPanel auth smokes per flow, write dual-sample promotion (`aspNetInteractiveComplete` stays 0), human `RELEASE_OWNER_APPROVAL.md`.

## Locks

- `cutoverAllowed=false`
- `readyForPhpRemoval=false`
- `aspNetInteractiveComplete=0`
- Never invent `RELEASE_OWNER_APPROVAL.md`

Suite **fails** if required floors/evidence are missing. Live auth smokes appear as **blocked** until CloudPanel cookies/artifacts exist — PHP must remain until every flow is fully green + dual-sample + human approval.
