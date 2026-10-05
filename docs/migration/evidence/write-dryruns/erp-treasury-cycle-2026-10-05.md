# ERP treasury cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| petty cash | POST /erp/ajax/petty-cash-save | ok — `epc_erp_petty_cash` 1 "CYC Petty" float 500 acct 1 |
| transfer | POST /erp/ajax/transfer-voucher (300 bank→cash) | ok — `Transfer voucher posted`; paired `epc_erp_cash_bank_entries` 18/19 (`transfer_out`/`transfer_in`, pair ids, `TV-2026-00001`), GL journals 140/141 |
| batch | POST /erp/ajax/payment-batch-save | ok — PAY-1 draft 150 acct 2 |
| bad target | `targetStatus:"approved"` | verbatim `Payment batch status transition is invalid.` (targets limited to submitted/processed/cancelled) |
| transition | `submitted` then `processed` | ok — `Payment batch submitted.` / `processed.` status `processed` |
| backwards | `submitted` after processed | verbatim `Payment batch cannot move from processed to submitted.` |

**Bug found and fixed in this PR:** `ErpPaymentBatchStatusWriteService.ReadStatusAsync` built its own command and added an *unnamed* parameter while `ErpDb.Positional` rewrites `?` to `@p0` — the id never bound, so every valid transition answered `Payment batch not found.` The read now goes through `ErpDb.StringAsync`. Fix proven above (draft→submitted→processed live).

Cleanup: GL lines/journals, entries, petty cash, batch deleted — residue 0.
