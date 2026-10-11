# ERP cost-centre + costing-model cycle — live rehearsal evidence (2026-10-11)

Scope: `/erp/fin/cost-centers/write` (save_center, post_allocation) → `/erp/ajax/costm-item-set` → `costm-txn-add` → `costm-close-run` on the throwaway tenant MariaDB (`ecomae`), with `epc_cc_*` and `epc_costm_*` absent at start.

## Lazy-schema parity fix (same PR)

`epc_erp_costing.php` ensures `epc_cc_centers` + `epc_cc_allocations`; `epc_erp_cost_models.php` ensures `epc_costm_item` + `epc_costm_txn` + `epc_costm_close` at entry. The ASP.NET writers carried `not provisioned` fail-closed guards (close-run had its own inline ensure). Added `ErpLazySchema.EnsureCostCentersAsync` (2 tables) and `EnsureCostModelsAsync` (3 tables), wired after `OpenAsync` in the cost-centre writer (both open points), `costm-item-set`, `costm-txn-add`, and `costm-close-run`.

## Real bug fixed by the rehearsal

`ErpCostmItemSetWriteService` and `ErpCostmTxnAddWriteService` wrote `company_id` from the request body verbatim (0 when absent), while `costm-close-run` resolves the session legal entity via `ErpFinAdvancedCompany.ResolveAsync` — so a close run saw zero transactions. PHP passes the same session `$companyId` to every function. Both writers now resolve company the same way; close run computes correctly.

## Steps (verified via HTTP + SQL)

1. `save_center` CC100 "Warehouse CC" → id:1; CC200 "Office CC" → id:2 (tables lazily created).
2. `post_allocation` `{runLabel:"RUN-CYC-1", sourceCost:1000, weights:"1|60\n2|40"}` → `Cost allocation posted (2 line(s)).` — rows 1:600.00, 2:400.00.
3. `costm-item-set` item 42 `moving_avg` std 10 → id:1.
4. `costm-txn-add` receipt 10@10, receipt 4@20, issue 6 → three rows under company_id=1.
5. `costm-close-run` `{itemId:42, label:"2026-10"}` → `Closing: COGS 77.14 · closing value 102.86` — avg 12.857/unit, closing_qty 8.0000, variance 0.
6. Refusal verified verbatim: `weights:"1:100"` → `At least one positive cost-centre weight is required.` (format is `id|weight` per line); unknown action → `Unknown cost-centre action. save_center / post_allocation are supported.`

## SQL corroboration

```
epc_cc_centers     : 1 CC100 / 2 CC200
epc_cc_allocations : RUN-CYC-1 → 1:600.00, 2:400.00
epc_costm_txn      : company 1 — receipt 10@10, receipt 4@20, issue 6
epc_costm_close    : company 1 moving_avg cogs=77.14 closing_qty=8.0000 closing_value=102.86
```

## Zero residue

All five tables dropped; `SHOW TABLES LIKE 'epc_cc%' / 'epc_costm%'` empty.
