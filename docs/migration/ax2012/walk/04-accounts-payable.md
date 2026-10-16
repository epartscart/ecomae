# Accounts payable

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Vendor invoice — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Matching — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Payment — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Settlement — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

Accounts payable parameters were not opened. See `19-parameters.md`.

## Forms the field register marks only as Named

`14-all-modules-form-register.md` marks these forms Named. This walk did not open them. Header FastTabs and line FastTabs: not clicked. `Ax32.exe` did not start, so no infolog was shown.

| Form in the register | This walk |
| --- | --- |
| Accounts payable parameters (`VendParameters`) | not in this license. not clicked. See `19-parameters.md` |
| Vendor posting profiles (`VendLedger`) | not in this license. not clicked |
| Vendor groups (`VendGroup`) | not in this license. not clicked |
| Matching policy | not in this license. not clicked |
| Charges (`MarkupTrans`) | not in this license. not clicked |

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
