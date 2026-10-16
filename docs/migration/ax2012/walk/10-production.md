# Production control

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

The production chain is in the order requested. It does not stop at the production order.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Production order | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Bill of materials | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Route | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Costing | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Report as finished | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

Production parameters were not opened. See `19-parameters.md`.

## ECOM AE table for a new column

No new column was observed. Do not add a second inventory table or a second ledger table. `00-build-rules.md` does not name a production-order table.

| Step | Table that would receive a new column |
| --- | --- |
| Component issue and report as finished | `epc_erp_inv_movements` |
| On-hand after that movement | `epc_erp_inv_stock` |
| Ledger result | `epc_erp_gl_journals` and `epc_erp_gl_lines` |
