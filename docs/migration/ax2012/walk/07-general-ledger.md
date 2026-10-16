# General ledger

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

The ledger chain is in the order requested. It does not stop at the general journal.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| General journal | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Voucher | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Period close | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Consolidation | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Financial report | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

General ledger parameters were not opened. See `19-parameters.md`.

## ECOM AE table for a new column

No new column was observed. Do not add a second ledger table. Posting stays on `ErpGlPostingService.PostJournalAsync`.

| Step | Table that would receive a new column |
| --- | --- |
| General journal, voucher, period close, consolidation | `epc_erp_gl_journals` and `epc_erp_gl_lines` |
| Voucher number | `epc_erp_gl_journals.journal_no`, with the sequence on `epc_erp_voucher_sequences` |
| Financial report | No table. It reads `epc_erp_gl_lines`. |
