# Project management and accounting

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

The project chain is in the order requested. It does not stop at the project.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Project — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Item requirement — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Invoice proposal — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Revenue recognition — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

Project parameters were not opened. See `19-parameters.md`.

## Forms the field register marks only as Named

`14-all-modules-form-register.md` marks these forms Named. This walk did not open them. Header FastTabs and line FastTabs: not clicked. `Ax32.exe` did not start, so no infolog was shown. The register does not name a project parameters form. That form was not opened. See `19-parameters.md`.

| Form in the register | This walk |
| --- | --- |
| Project groups (`ProjGroup`) | not in this license. not clicked |
| Project categories | not in this license. not clicked |

## ECOM AE table for a new column

No new column was observed. Do not add a second inventory, invoice, or ledger table. `00-build-rules.md` does not name a project header table.

| Step | Table that would receive a new column |
| --- | --- |
| Item requirement | `epc_erp_inv_movements` |
| Invoice proposal | The customer invoice link already on `epc_erp_sales_orders.sales_invoice_id`. No second invoice table. |
| Revenue recognition | `epc_erp_gl_journals` and `epc_erp_gl_lines` |
