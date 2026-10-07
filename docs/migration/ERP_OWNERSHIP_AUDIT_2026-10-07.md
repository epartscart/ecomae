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

## Open findings, highest risk first

1. **Cash entries can exist without a journal.** `TryPostCashEntryAsync` swallows GL failures (missing COA, closed period), as PHP does. The bank balance then differs from GL 1000/1010 until `ErpGlSyncUnpostedWriteService` runs. Next step: an integrity check that lists `epc_erp_cash_bank_entries` with `gl_journal_id = 0`.
2. **The integrity scan checks orphans only.** `ErpIntegrityService` has no check for unbalanced journals or for sub-ledger totals against control accounts (AR 1100, AP 2000, cash 1000/1010). Next step: add both checks, read-only.
3. **POS sales are outside the ledger.** `ErpRtlPosSaleWriteService` writes `epc_rtl_txn` and its lines but posts no journal and no stock movement, matching PHP `epc_rtl_pos_sale`. Its request uses `double` for quantity and unit price. Next step: post the sale through the existing posting engine and the existing inventory movement service. Do not add a second engine.
4. **Voucher gaps.** Cash, journal, and transfer vouchers are not gap-free after a failed write. Tax invoices need a separate check for gap-free numbering.
5. **Untested write services:** `ErpAmlComplianceWriteService`, `ErpOpeningPostBatchWriteService`, `ErpPettyCashWriteService`, `ErpPmListingSaveWriteService`, `ErpPrjaRecognitionWriteService`, `ErpQmOrderCreateWriteService`, `ErpRfqResponseWriteService`, `ErpRtlPosSaleWriteService`. Opening balances and project revenue recognition affect reported figures and come first.
6. **Floating-point money** in `ErpOrderFulfillmentWriteService`, `ErpRtlPosSaleWriteService`, `ErpPfDemoSyncWriteService`, and `ErpOplPlanningWriteService`.
7. **The 2026-10-07 deploy ran with `ECOMAE_EMERGENCY_PUBLISH=1`**, which skips the server-side foundation and unit tests. The merged code passed 5121 of 5121 locally before the deploy. Later deploys should run without that flag once the gate check passes.

## Gates

Nothing here changes the phase gates. `ReadyForPhpRemoval`, `PhpSourceDeletionAllowed`, and `CutoverAllowed` stay false.
