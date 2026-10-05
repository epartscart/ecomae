# ERP treasury bank cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

Same authenticated session, live routes only.

| Step | Route | Result |
|---|---|---|
| bank import | POST /erp/ajax/bank-import (CSV 2 lines, account 2) | ok — `epc_erp_bank_statement_lines` 2 (in 1050 REF-CYC-1 / out 25 REF-CYC-2, batch IMP-20261005-075731) |
| customer + receipt | /erp/ajax/customer-create + /erp/cash-entries/receipt-voucher (account 2, 1050) | ok — user 3 + contact 26; cash entry 17 (receipt 1050, account 2) |
| reconcile | POST /erp/ajax/bank-reconcile `lineId=2,entryId=17` | ok — `matched_entry_id=17`, "Bank line matched to cash entry" |
| blank match guard | bank-reconcile with no ids | verbatim refusal `Invalid match` |

Note: matching is line↔entry id only (no direction check) — same contract as PHP `epc_erp_bank_reconcile`; recorded as observed, not added as a guard.

Cleanup: statement lines, cash entry, contact and user deleted — residue 0.
