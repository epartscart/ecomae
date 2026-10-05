# ERP lazy-schema parity sweep — 2026-10-05

Continuation of the contracts/subscriptions/hr-expense lazy-schema fixes
(#2015, #2016). PHP `*_ensure_schema` functions create 454 tables lazily
(`CREATE TABLE IF NOT EXISTS` at entry). On a tenant where the table was never
used, PHP succeeds; the ASP.NET writer returned HTTP 500 — a real parity bug.

## Sweep method

1. Extracted the 454 `CREATE TABLE IF NOT EXISTS` targets from
   `content/shop/finance/*.php`.
2. Diffed them against the throwaway tenant DB → ~250 ensured tables absent.
3. Found every `Erp*WriteService` that touches an absent, PHP-ensured table
   without its own ensure/`CREATE TABLE` → **13 services, 15 tables**.

## Fix

New `Erp/ErpLazySchema.cs`: verbatim-DDL `Ensure*Async` helpers (via
`ErpDb.TryExecuteAsync`, `ENGINE=InnoDB` as PHP), wired right after
`OpenAsync` in each affected writer — same pattern as `ErpContractSchema`,
`ErpSubscriptionSchema`, `ErpHrExpenseSchema`.

| Helper (tables) | Writers |
|---|---|
| `EnsureVatRefundsAsync` (epc_bos_vat_refunds) | bos-vat-refund-save, bos-vat-refund-status |
| `EnsureConsEntitiesAsync` (epc_cons_entities) | cons-entity save, cons delete |
| `EnsureHrLeaveAsync` (epc_hr_leave) | hr-leave-request |
| `EnsureInsuranceAsync` (ins_claims, ins_documents) | ins-claim-add, ins-claim-status, ins-doc-delete |
| `EnsureInventoryForecastAsync` (demand_history, inventory_forecast) | inventory-forecast |
| `EnsureLandedCostAsync` (sheets, lines, expenses) | landed-cost |
| `EnsureFxRatesAsync` (epc_fx_rates) | multi-currency GL |
| `EnsureProjectAccountingAsync` (prja_budget, prja_recognition, prja_txn) | prja-recognize |
| `EnsureUserShortcutsAsync` (epc_user_shortcuts) | workspace favorites/shortcuts |

## Live proof on throwaway tenant DB

- `POST /erp/ajax/bos-vat-refund-save` → `ok:true "Refund record saved"`;
  `epc_bos_vat_refunds` created, row status `recorded`.
- `POST /erp/ajax/hr-leave-request` → `ok:true "Leave request submitted"`;
  `epc_hr_leave` created, row `annual`, 3.00 days, `pending`.
- `POST /erp/ajax/shortcut-add` → `ok:true "Shortcut added"`;
  `epc_user_shortcuts` created.

All three tables did not exist before the calls. Residue removed afterwards
(rows deleted, tables dropped back to pre-rehearsal state).

## Incidental fixes in this tranche

- Renamed `IErpCsPdfImportService` → `IErpCsPdfImportWriteService` (file,
  implementation, registration, test path) so the catalog↔handler
  `Write|ReadService` naming contract in
  `ErpLiveWriteCatalogStatusTests` holds; the test had begun failing on
  `cs_import_declaration_pdf` (#1994 merged with the convention already
  required by later tests).
- `epc_erp_favourites` (erp-fav-add) is **not** a lazy-schema case: PHP never
  ensures it (install-time table) — a 500 there is fixture gap parity, not a
  bug.

## Verification

- `dotnet test tests/EcomAE.Platform.Tests` — 2254/2254 pass.
- `dotnet build` — clean, 0 warnings.
