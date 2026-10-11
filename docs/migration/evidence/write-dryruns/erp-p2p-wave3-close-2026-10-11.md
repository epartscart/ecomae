# P2P wave-3 close-out — 2026-10-11

Scope: P2P mirror of the O2C wave-2 items, all landed on `main` (#2096–#2101).

| Item | Evidence |
|---|---|
| 1. PO + bill document readbacks | #2096 (`ErpDocumentReadbacks` shared component — lines, attachments, audit trail on both PO and purchase detail, rendered only when non-empty; `LoadDocumentLinesAsync`/`LoadEntityAuditTrailAsync` generalized for `purchase`/`purchase_order` entity types) |
| 2. Bill paid/due + AP aging | #2097 (`ErpPurchaseDigest` gains `PaidAmount`/`AmountDue` from the same `epc_erp_supplier_accounting` non-credit formula as `SelectErpAgingApDocuments`; Paid/Due columns + AP aging strip 0–30/31–60/61–90/90+ on `/erp/purchases-app`) |
| 3. Open-PO backlog KPIs | #2098 (open PO count/value + aged-30d+ count/value on `/erp/purchase-orders-app`; closed/cancelled/received excluded) |
| 4. Bill open-balance readback on payment form | #2099 (`GET /erp/purchase-open-balance` admin-gated returning `{invoiceNumber, status, totalInclVat, paidAmount, amountDue, supplierId, orderId}`; `erp-cash-entries.js` refactored to shared `watchBalance`; `#pv_purchase_id` hint + "use open balance" fills `#pv_amount` + `#pv_supplier_id`) |
| 5. Printable PO + supplier bill | #2100 (`RenderPurchaseOrderAsync`/`RenderPurchaseAsync` on `ErpO2cDocumentPrintService`; `/erp/purchase-order-print`, `/erp/purchase-print` admin-gated; Print buttons on both detail banners) |
| 6. P2P reports | #2101 (purchases-by-supplier + purchases-by-item top-10 summaries on `/erp/purchases-app`); AP aging detail at `/erp/aging-app`, supplier statement + purchase/payment history on `/erp/payables-app`, `proc_pos` register in report center — all pre-existing, verified in place |
| 7. Extended throwaway-DB cycle | Live local app (throwaway tenant `ecomae`), all via guarded ajax writes with session CSRF: `po-save` PO-2026-00011 → `po-status` draft→approved → `po-receive-lines` partial (qty 1/2, status `partial`) → full (2/2, `received`) → `po-to-invoice` bill PI-2026-00004 (200.00, `confirmed`, po_id linked; second call refused "already linked") → `supplier-payment` 250 refused "exceeds open balance 200.00" → 120 posted (PV-2026-00003, GL journal 165, bill `partial`) → 80 posted (PV-2026-00004, GL 166, bill `paid`) → further payment refused "already fully paid". Fixture rows removed across purchases/supplier_accounting/cash entries/GL/PO tables; residue count 0. |
| 8. Per-page UX screenshots | Captured locally: `/erp/purchases-app` (Paid/Due columns, AP aging, by-supplier/by-item strip), `/erp/purchase-orders-app` (backlog KPI strip, new-PO form, action pane). |

## Fixture note
`epc_erp_purchase_orders` has no `active` column (uses status enum only); `epc_erp_purchase_inv_lines` requires `warehouse_id` — SQL inserts adjusted accordingly. No code change needed.

## Board after close
- P2P bands: Functional ~88% · UI-UX ~78% · Reports ~70% · Writes ~88% · Testing ~72% (from 85/70/45/85/70).
- Weighted headline unchanged ~20.4% (0/15 processes accepted — acceptance still gated on owner items).
- Next wave per plan: Inventory/WMS workspaces.
