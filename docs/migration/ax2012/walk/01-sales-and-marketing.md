# Sales and marketing

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown. FastTabs, buttons, number sequences, and printouts were not read from the client.

The sales chain is in the order requested. Invoice, customer payment, and settlement are also in `02-accounts-receivable.md`. This file does not stop at the quote.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Quote | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Sales order | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Confirmation | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Packing slip | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Invoice | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Customer payment | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Settlement | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Credit note | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

## ECOM AE table for a new column

No new column was observed. Do not add a second sales table. The tables already named in `docs/migration/ax2012/00-build-rules.md` are:

| Step | Table that would receive a new column |
| --- | --- |
| Quote, sales order, confirmation, packing slip, credit note | `epc_erp_sales_orders`. Lines stay on `epc_erp_sales_order_lines`. |
| Packing slip stock, credit-note stock | `epc_erp_inv_movements` |
| Invoice | `epc_erp_sales_orders.sales_invoice_id`. No second invoice table. |
| Customer payment | `epc_erp_cash_bank_entries` |
| Settlement | `epc_erp_settlement_allocations` |
| Ledger result of any post | `epc_erp_gl_journals` and `epc_erp_gl_lines` through `ErpGlPostingService.PostJournalAsync` |
| Document number, if a sequence is later observed | `epc_erp_voucher_sequences` |
