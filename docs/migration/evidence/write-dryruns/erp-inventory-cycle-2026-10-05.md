# ERP inventory cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

Same authenticated session as the O2C+P2P cycle, live ajax routes only.

| Step | Route | Result |
|---|---|---|
| warehouse | POST /erp/ajax/inv-create-warehouse | ok — `epc_erp_inv_warehouses` 909 (CYC-WH) |
| item | POST /erp/ajax/inv-create-item | ok — `epc_erp_inv_items` 936 (CYC-IT-1, cost 10) |
| receipt | POST /erp/ajax/inv-record-movement `purchase_in` 50 @ 10 | ok — movement 33 |
| issue | POST /erp/ajax/inv-record-movement `sale_out` 8 | ok — movement 34 |
| stock readback | SQL `epc_erp_inv_stock` | `qty_on_hand=42.000`, `avg_unit_cost=10.0000` — receipt-issue arithmetic + running avg cost correct |
| over-issue guard | POST inv-record-movement `sale_out` 100 > 42 | verbatim refusal `Insufficient quantity on hand`, no row |
| invalid type guard | `receipt`/`issue` (not PHP movement codes) | verbatim `Invalid movement type` |

Cleanup: movements, stock, item, warehouse deleted — residue 0.
