# Accounts payable

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Vendor invoice | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Matching | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Payment | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Settlement | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

Accounts payable parameters were not opened. See `19-parameters.md`.

## ECOM AE table for a new column

No new column was observed. Do not add a second purchase or invoice table.

| Step | Table that would receive a new column |
| --- | --- |
| Vendor invoice and matching | `epc_erp_purchases` and `epc_erp_purchase_inv_lines` |
| Vendor subledger | `epc_erp_supplier_accounting` |
| Payment | `epc_erp_cash_bank_entries` |
| Settlement | `epc_erp_settlement_allocations` |
| Ledger result | `epc_erp_gl_journals` and `epc_erp_gl_lines` |
| Document number, if a sequence is later observed | `epc_erp_voucher_sequences` |
