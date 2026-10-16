# Inventory and warehouse management

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

The inventory chain is in the order requested. It does not stop at the released product.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Released product | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| On-hand | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Reservation | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Transfer | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Movement | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Adjustment | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Counting | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Quarantine | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Close | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

Inventory parameters were not opened. See `19-parameters.md`.

## ECOM AE table for a new column

No new column was observed. Do not add a second inventory table.

| Step | Table that would receive a new column |
| --- | --- |
| Released product | `epc_erp_inv_items` |
| On-hand | `epc_erp_inv_stock` |
| Reservation, transfer, movement, adjustment, counting, quarantine | `epc_erp_inv_movements` |
| Close | `epc_erp_inv_closing` |
| Ledger result of a posted journal | `epc_erp_gl_journals` and `epc_erp_gl_lines` |
| Document number, if a sequence is later observed | `epc_erp_voucher_sequences` |
