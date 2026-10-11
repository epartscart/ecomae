# ERP ownership audit — 2026-10-07

Cursor owns ERP from this date. Devin's ERP work is merged on `main` through #2026 and was deployed as release `20261007084903` (`debe2654e`). This audit reads that code and compares it with the PHP reference in `content/shop/finance/`. PHP parity is the minimum. Dynamics 365, SAP, and Oracle set the bar for accounting controls.

## Scope

- 361 files in `aspnet/src/EcomAE.Platform/Erp`, 275 of them write services.
- 267 of the 275 write services are referenced by at least one test. The full suite on this branch is 5139 of 5139.
- One posting engine: every GL journal goes through `ErpGlPostingService.PostJournalAsync`. No other code inserts into `epc_erp_gl_journals` or `epc_erp_gl_lines`. Balance validation and the period lock run there before the transaction opens.

## Earlier known issues, re-checked

| Issue | Now |
| --- | --- |
| Sales order digest Cancel could flip an invoiced order through `/erp/ajax/so-status` | Fixed. `SetStatusAsync` runs `StatusTransitionError` and a compare-and-set update. |
| `epc_erp_supplier_accounting` created with different columns by two modules | Fixed. Both creators add the missing `active`, `entry_kind`, and `gl_journal_id` columns. |
| Cash transfer posted Dr 6100 expense and Cr 4000 revenue | Fixed on this branch (see below). PHP still does this. |
| Failed writes consume voucher numbers | Still open. `NextAsync` runs DDL, which MySQL commits implicitly, so it runs before the document transaction, the same as PHP. |

## Fixed on this branch

1. **Journal balance is checked on stored values.** `Validate` summed lines rounded to 4 decimals and allowed 0.0001 difference, while `epc_erp_gl_lines` stores `decimal(14,2)`. Debit 10.005 against credits 5.0025 + 5.0025 passed and was stored as 10.01 against 10.00. Lines are now rounded to 2 decimals before summing and must match exactly. PHP `epc_erp_gl_post_journal` has the same 4-decimal check.
2. **Cash transfers no longer touch profit and loss.** `PostCashEntryAsync` treated a `counterparty_type = 'internal'` leg as other income or a general expense. Each transfer leg now posts against system account `1090 Cash in transit`: out leg Dr 1090 / Cr source cash, in leg Dr destination cash / Cr 1090. The account is seeded for new tenants and added on first transfer for existing tenants. It must be an active asset account, otherwise posting is refused and the cash entry stays unposted, as for any other GL failure.

## Open findings, highest risk first — resolution status (2026-10-11)

1. **Cash entries can exist without a journal — FIXED (#2074).** The cash entry and its GL journal now post in one transaction inside `TryPostCashEntryAsync`; a GL failure rolls the entry back, so `gl_journal_id = 0` can no longer persist. The integrity scan also lists entries with `gl_journal_id = 0` for any legacy rows.
2. **The integrity scan checks orphans only — FIXED (#2074).** `ErpIntegrityService` gained read-only checks for unbalanced journals (stored 2-decimal Dr vs Cr per journal) and sub-ledger vs control-account drift (AR 1100 vs open receivables, AP 2000 vs open payables, cash 1000/1010 vs `epc_erp_cash_bank_entries`).
3. **POS sales are outside the ledger — FIXED (#2075).** `ErpRtlPosSaleWriteService` now writes `epc_rtl_txn` + lines, per-line `sale_out` rows in `epc_erp_inv_movements` and stock decrements, and a balanced GL journal (Dr 1000 / Cr 4000 / Cr 2100) via `ErpGlPostingService.PostJournalAsync` — all inside the sale transaction. GL posts when COA 1000+4000 exist (skipped silently when unprovisioned); stock posts when `WarehouseId > 0` and the inventory tables exist; any posting failure rolls the whole sale back. This exceeds PHP, which posts neither.
4. **Voucher gaps — FIXED (#2074).** Voucher numbers are only consumed after the document transaction succeeds (`ErpVoucherNumberService` defers the counter increment to post-commit); a failed write leaves no gap.
5. **Untested write services — FIXED (#2074).** `ErpOpeningAndPrjaWriteServiceTests` (3 tests: opening batch post + status flip + not_found, unsupported-line refusal, PRJA POC + completed math persisted) and `ErpUntestedWriteServicesTests` (petty cash, PM listing, QM order, RFQ response, RTL POS with journal + stock assertions) cover the listed services; `ErpAmlComplianceWriteService` was already covered.
6. **Floating-point money — FIXED (#2075, #2076).** All four services plus `ErpPmListingSaveWriteService` now use `decimal` for qty/price/cost/value. In `ErpOplPlanningWriteService` the statistical internals (service level, demand series, ADI/CV², sigma, z-factor) deliberately stay `double` — they are probabilities, not money — and cross into `decimal` only at the ROQ/value boundary.
7. **The 2026-10-07 deploy ran with `ECOMAE_EMERGENCY_PUBLISH=1`** — still an operational note, not code: later deploys should run without that flag once the gate check passes.

## Gates

Nothing here changes the phase gates. `ReadyForPhpRemoval`, `PhpSourceDeletionAllowed`, and `CutoverAllowed` stay false.
