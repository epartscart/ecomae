# ERP WMS transfer cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

Same authenticated session, live routes only.

| Step | Route | Result |
|---|---|---|
| warehouses | POST /erp/ajax/inv-create-warehouse ×2 | ok — CYC-WHB 910, CYC-WHA 911 |
| item | POST /erp/ajax/inv-create-item | ok — CYC-TR-1 937 (cost 20) |
| receipt | POST /erp/ajax/inv-record-movement `purchase_in` 30 @ 20 into 910 | ok — movement 35 |
| transfer | POST /erp/ajax/inv-transfer 910→911 qty 12 | ok — `transfer_out` 36 (910) + `transfer_in` 37 (911) pair, "at avg cost 20.0000" |
| stock readback | SQL `epc_erp_inv_stock` | 910: `qty_on_hand=18`, 911: `qty_on_hand=12`, both avg 20.0000 |
| over-transfer guard | inv-transfer 99 > 18 | verbatim refusal `Insufficient stock at source warehouse`, no rows |
| same-warehouse guard | from=to=910 | verbatim `Source and destination warehouses required (must differ)` |

Cleanup: movements, stock, item, both warehouses deleted — residue 0.
