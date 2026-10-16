# Inventory and warehouse management

Area page: not opened. Menu items under this area were not listed, because the page was not on screen.

Click result used in every cell below: not clicked. `Ax32.exe` did not start. No infolog was shown.

The inventory chain is in the order requested. It does not stop at the released product.

| Menu path | List page and details form | What New creates, and the number sequence | Header FastTabs and line FastTabs | Action-pane buttons, including reversal | Block rule in the infolog | Inquiry, printout, and report | Setup form and the one setting |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Released product — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| On-hand — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Reservation — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Transfer — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Movement — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Adjustment — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Counting — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Quarantine — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |
| Close — not in this license | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked | not clicked |

Inventory parameters were not opened. See `19-parameters.md`.

## Forms the field register marks only as Named

`14-all-modules-form-register.md` marks these forms Named. This walk did not open them. Header FastTabs and line FastTabs: not clicked. `Ax32.exe` did not start, so no infolog was shown. The counted inventory-parameter topic was not re-counted from the client.

| Form in the register | This walk |
| --- | --- |
| Tag counting | not in this license. not clicked |
| Bills of materials journal | not in this license. not clicked |
| Item arrival | not in this license. not clicked |
| Production input | not in this license. not clicked |
| Closing and adjustment (`InventSettlement`) | not in this license. not clicked |
| Batches (`InventBatch`) | not in this license. not clicked |
| Serial numbers (`InventSerial`) | not in this license. not clicked |
| Item tracing | not in this license. not clicked |
| Quarantine orders (`InventQuarantineOrder`) | not in this license. not clicked |
| Quality orders | not in this license. not clicked |
| Nonconformances | not in this license. not clicked |
| Inventory blocking | not in this license. not clicked |
| Locations (`WMSLocation`) | not in this license. not clicked |
| Journal names (`InventJournalName`) | not in this license. not clicked |
| Batch number groups (`InventNumGroup`) | not in this license. not clicked |
| Serial number groups (`InventNumGroup`) | not in this license. not clicked |
| Inventory and warehouse parameters (`InventParameters`) | not in this license. not clicked. See `19-parameters.md` |

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
