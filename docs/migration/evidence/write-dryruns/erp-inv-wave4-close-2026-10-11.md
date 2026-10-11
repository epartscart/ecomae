# Inventory/WMS wave-4 close-out — 2026-10-11

Scope: Inventory/WMS mirror of the O2C/P2P workspace polish, all landed on `main` (#2103–#2107).

| Item | Evidence |
|---|---|
| 1. Live on-hand readback on write forms | #2103 (`GET /erp/inv-on-hand` admin-gated: aggregated on-hand across batches/variants with weighted avg unit cost, stock value and `atReorder` flag; `erp-inventory-stock.js` `watchOnHand` warns when out-movements exceed on-hand and flags reorder-level stock; wired on `/erp/ajax/inv-record-movement` and `/erp/ajax/inv-transfer` forms; verified live: two batches 5@20 + 5@30 → `10 @ avg 25` + reorder flag) |
| 2. Movement summaries | #2104 (movements-by-warehouse [Lines, Qty in, Qty out, Cost] and movements-by-item [Lines, NetQty, Cost] top-10 strips on `/erp/stock-movements-app`, in-memory `GroupBy` over the loaded digest — zero extra queries) |
| 3. Warehouse names on transfers | #2105 (all three `epc_warehouse_transfers` queries LEFT JOIN `epc_erp_inv_warehouses` twice for `from_warehouse_name`/`to_warehouse_name`; names shown on list, opened document workspace Party/Reference and fields, and same-status siblings on `/erp/stock-transfers-app` + the transfer-history table on `/erp/warehouses-app`; numeric id fallback) |
| 4. Category labels on inventory report | #2106 (`CategoryLabel(long)` helper resolves `category_id`/`parent_id` to `CODE — name` from the loaded categories on `/erp/inventory-report-app` — snapshots list, snapshot Reference/Category fields, and the categories Parent column; numeric id fallback) |
| 5. Forecast reorder guidance + projection fix | #2107 (bug: `SelectErpInventoryForecast` never projected `site_key` yet the reader consumed it — list failed `database-error` for any tenant with forecast rows; fixed projection. New: "To reorder" KPI `CurrentStock <= ReorderPoint && Eoq > 0`, "Suggested qty" KPI = sum of EOQ on due rows, "Order qty" column) |
| 6. Extended throwaway-DB cycles | Already merged and standing: `erp-inventory-cycle-2026-10-05.md` (warehouse→item→purchase_in 50→sale_out 8→on_hand 42 @ avg 10; over-issue refused verbatim) and `erp-wms-transfer-cycle-2026-10-05.md` (paired transfer_out/in at avg cost, over-transfer + same-warehouse refusals; residue 0). Wave-4 pages re-verified live this wave on seeded fixtures (`epc_warehouse_transfers`, `epc_erp_inv_warehouses`, `epc_inventory_categories`, `epc_inventory_snapshots`, `epc_inventory_forecast` — all per PHP lazy DDL). |
| 7. Per-page UX verification | `/erp/stock-movements-app` (summary strips), `/erp/stock-transfers-app` + `/erp/warehouses-app?tab=virtual_warehouse` (warehouse names, workspaces), `/erp/inventory-report-app` (category labels), `/erp/inventory-forecast-app` (KPIs, order qty) — each fetched live as an admin session and rendered data confirmed. |

## Remaining Inventory/WMS gaps after close
- Ship/receive actions still stay on the Classic twin (by design — writes are guarded ajax `inv-transfer`/`inv-record-movement`, which are live).
- MRP firming/wave picking live earlier (#1706/#1709/#1710); planning page is Wave-4-adjacent, already workspace-grade.
- No accepted warehouse-manager workspace yet — acceptance still gated on owner items (second tenant DB, human visual acceptance, etc.).

## Board after close
- Inventory/WMS bands: Functional ~85% · UI/UX ~75% · Reports ~65% · Writes ~80% · Testing ~65% (from 75/65/40/70/55).
- Weighted headline unchanged ~20.4% (0/15 processes accepted — acceptance still gated on owner items).
- Next wave per plan: Finance.
