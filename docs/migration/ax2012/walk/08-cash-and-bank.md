# Cash and bank management

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

Fixed asset acquisition, depreciation, and disposal are in `09-fixed-assets.md`.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Bank deposit — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Reconciliation — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Check — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

## Forms the field register marks only as Named

`14-all-modules-form-register.md` marks these forms Named. This walk did not open them. Header FastTabs and line FastTabs: not clicked. `Ax32.exe` did not start, so no infolog was shown.

| Form in the register | This walk |
| --- | --- |
| Bank groups (`BankGroup`) | not in this license. not clicked |
| Bank transaction types | not in this license. not clicked |

## ECOM AE table for a new column

No new column was observed. Do not add a second cash table or a second ledger table.

| Step | Table that would receive a new column |
| --- | --- |
| Bank deposit and reconciliation | `epc_erp_cash_bank_entries`. The bank account stays `epc_erp_cash_bank_accounts`. |
| Check | `epc_erp_pm_cheques` |
| Ledger result | `epc_erp_gl_journals` and `epc_erp_gl_lines` |
| Document number, if a sequence is later observed | `epc_erp_voucher_sequences` |
