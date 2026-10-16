# Production control

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

The production chain is in the order requested. It does not stop at the production order.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Production order — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Bill of materials — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Route — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Costing — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Report as finished — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

Production parameters were not opened. See `19-parameters.md`.

## Forms the field register marks only as Named

`14-all-modules-form-register.md` marks these forms Named. This walk did not open them. Header FastTabs and line FastTabs: not clicked. `Ax32.exe` did not start, so no infolog was shown.

| Form in the register | This walk |
| --- | --- |
| Resources (`WrkCtrTable`) | not in this license. not clicked |
| Production parameters (`ProdParameters`) | not in this license. not clicked. See `19-parameters.md` |
| Cost groups and costing versions (`InventItemPrice`) | not in this license. not clicked |
| Picking list and report as finished journals | not in this license. not clicked |

## ECOM AE table for a new column

No new column was observed. Do not add a second inventory table or a second ledger table. `00-build-rules.md` does not name a production-order table.

| Step | Table that would receive a new column |
| --- | --- |
| Component issue and report as finished | `epc_erp_inv_movements` |
| On-hand after that movement | `epc_erp_inv_stock` |
| Ledger result | `epc_erp_gl_journals` and `epc_erp_gl_lines` |
