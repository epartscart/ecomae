# Procurement and sourcing

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

The purchase chain is in the order requested. Vendor invoice, matching, payment, and settlement are also in `04-accounts-payable.md`. This file does not stop at the requisition.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Requisition | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Request for quotation | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Purchase order | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Product receipt | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Vendor invoice | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Matching | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Payment | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Settlement | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

## Forms the field register marks only as Named

`14-all-modules-form-register.md` marks these forms Named. This walk did not open them. Header FastTabs and line FastTabs: not clicked. `Ax32.exe` did not start, so no infolog was shown.

| Form in the register | This walk |
| --- | --- |
| Procurement categories (`EcoResCategory`) | not clicked. Ax32.exe did not start |
| Purchasing policies | not clicked. Ax32.exe did not start |

## ECOM AE table for a new column

No new column was observed. Do not add a second purchase table.

| Step | Table that would receive a new column |
| --- | --- |
| Requisition, request for quotation, purchase order | `epc_erp_purchase_orders`. Lines stay on `epc_erp_po_lines`. |
| Product receipt | `epc_erp_po_receipts`. Stock stays on `epc_erp_inv_movements`. |
| Vendor invoice and matching | `epc_erp_purchases` and `epc_erp_purchase_inv_lines` |
| Vendor subledger | `epc_erp_supplier_accounting` |
| Payment | `epc_erp_cash_bank_entries` |
| Settlement | `epc_erp_settlement_allocations` |
| Ledger result | `epc_erp_gl_journals` and `epc_erp_gl_lines` |
| Document number, if a sequence is later observed | `epc_erp_voucher_sequences` |
