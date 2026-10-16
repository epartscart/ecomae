# Accounts receivable

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Invoice | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Customer payment | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Settlement | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Credit note | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

Accounts receivable parameters were not opened. See `19-parameters.md`.

## ECOM AE table for a new column

No new column was observed. Do not add a second invoice table or a second ledger table.

| Step | Table that would receive a new column |
| --- | --- |
| Invoice and credit note | `epc_erp_sales_orders` via `sales_invoice_id` |
| Customer payment | `epc_erp_cash_bank_entries` |
| Settlement | `epc_erp_settlement_allocations` |
| Ledger result | `epc_erp_gl_journals` and `epc_erp_gl_lines` |
| Document number, if a sequence is later observed | `epc_erp_voucher_sequences` |
