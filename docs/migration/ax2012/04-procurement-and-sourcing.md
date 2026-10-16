# Procurement and sourcing

Menu path: **Procurement and sourcing**.

## Menu sections

| Section | List page | Details form | Next document |
| --- | --- | --- | --- |
| Purchase requisitions | All purchase requisitions | `PurchReqTable` | Approval creates a purchase order or an RFQ |
| Requests for quotation | All requests for quotations | `PurchRFQCaseTable` | Accept creates `PurchTable` |
| Purchase orders | All purchase orders | `PurchTable` | Product receipt, vendor invoice |
| Purchase agreements | Purchase agreements | `AgreementHeader` (purch) | Release order |
| Vendors | All vendors | `VendTable` | Master. Field list is in `05-accounts-payable.md` |
| Catalogs | Procurement catalog | `CatProcurementCatalog` | Category lines on the requisition |

## Purchase requisition — `PurchReqTable` / `PurchReqLine`

Header FastTabs: General, Financial dimensions. Line FastTabs: Item, Price, Financial dimensions.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| Header | `PurchReqId` | Number sequence | GAP. Do not insert a purchase order at draft |
| Header | `PurchReqName` | | GAP |
| Header | `Originator` | `HcmWorker.RecId` | GAP |
| Header | `RequisitionStatus` | Draft, In review, Approved, Rejected, Closed, Cancelled | GAP. Workflow rows go through `epc_erp` workflow, not a second status table |
| Header | `TransDate` | | GAP |
| Line | `PurchReqId` | Header | GAP |
| Line | `LineNum` | | GAP |
| Line | `ItemId` | `InventTable.ItemId` | `epc_erp_inv_items.sku` |
| Line | `ProcurementCategory` | `EcoResCategory.RecId` when there is no item | GAP |
| Line | `PurchQty`, `PurchUnit`, `PurchPrice`, `LineAmount` | | GAP |
| Line | `InventDimId` | `InventDim` | Shared dimension list |
| Line | `VendAccount` | Suggested `VendTable.AccountNum` | `supplier_id` once the PO exists |
| Line | `BuyingLegalEntity` | Company | Same company as the header |
| Line | `Requisitioner` | Worker | GAP |

Approval writes `PurchTable.PurchReqId` or an RFQ case. The requisition remains.

## Request for quotation — `PurchRFQCaseTable` / `PurchRFQCaseLine` / `PurchRFQTable`

| AX field | Links to | ECOM AE |
| --- | --- | --- |
| `RFQCaseId` | Number sequence | GAP |
| `RFQType` | | GAP |
| Line `ItemId`, `PurchQty`, `InventDimId` | Item and dimensions | GAP |
| Reply header `VendAccount` | `VendTable.AccountNum` | GAP |
| Reply `PurchQty`, `PurchPrice`, `LineAmount`, `DeliveryDate` | Vendor bid | GAP |
| Accept | Creates `PurchTable` and stores the RFQ id | `epc_erp_purchase_orders.id` |

## Purchase order details — `PurchTable` / `PurchLine`

Header FastTabs: **General, Setup, Delivery, Price and discount, Foreign trade, Financial dimensions**.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| General | `PurchId` | Number sequence | `epc_erp_purchase_orders.po_no` |
| General | `PurchStatus` | None, Backorder, Received, Invoiced, Canceled | `status`: draft, approved, partial, received, cancelled. Invoiced is GAP on this enum |
| General | `PurchType` | Journal, Purchase, ReturnItem | GAP |
| General | `OrderAccount` | `VendTable.AccountNum` | `supplier_id` |
| General | `InvoiceAccount` | `VendTable.AccountNum` | GAP. Same as order vendor until a separate invoice account exists |
| General | `PurchName` | | `title` |
| General | `VendorRef` | Vendor's reference | GAP |
| General | `WorkerPurchPlacer` | `HcmWorker` | GAP |
| General | `PurchaseType` / intercompany | Paired `SalesTable.SalesId` | `order_id` links a shop order, not an intercompany sales order. Intercompany is GAP |
| General | `PurchReqId` | `PurchReqTable.PurchReqId` | GAP |
| General | `AgreementId` | Purchase agreement | GAP |
| Setup | `PostingProfile` | `VendLedger` | GAP |
| Delivery | `DeliveryDate` | | GAP |
| Delivery | `DlvMode`, `DlvTerm` | Mode and terms | GAP |
| Delivery | `InventSiteId` | `InventSite` | GAP |
| Delivery | `InventLocationId` | `InventLocation` | Line `storage_id` is the closest column, and it is on the line |
| Price | `CurrencyCode` | | GAP |
| Price | `Payment` | `PaymTerm.PaymTermId` | `epc_erp_pm_pay_terms` |
| Price | `PaymMode` | Vendor pay mode | `epc_erp_pm_pay_methods` |
| Price | `TaxGroup` | `TaxGroupHeading` | Header `vat_amount` only |
| Price | `DiscPercent`, `CashDisc` | | GAP |
| Dimensions | `DefaultDimension` | | `epc_erp_dim_links` |
| Totals | | | `amount_ex_vat`, `vat_amount`, `total_amount` |
| Links | | | `purchase_id` → `epc_erp_purchases.id` after the vendor invoice. `voucher_no` after the journal. `approved_at`, `received_at` |
| Notes | | | `notes` |

### Lines — `PurchLine`

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| General | `PurchId` | Header | `epc_erp_po_lines.po_id` |
| General | `LineNumber` | | `line_no` |
| General | `ItemId` | `InventTable.ItemId` | `item_code` |
| General | `Name` | | `description` |
| General | `PurchQty` | | `qty` |
| General | `PurchUnit` | | GAP |
| General | `PurchPrice` | | `unit_cost_ex_vat` |
| General | `LineAmount` | | `line_ex_vat` |
| General | `PurchStatus` | Open, Received, Invoiced, Canceled | GAP on the line |
| Delivery | `RemainPurchPhysical` | Not yet received | Open qty is `qty − qty_received − qty_cancelled` |
| Delivery | `RemainPurchFinancial` | Not yet invoiced | GAP |
| Delivery | `QtyOrdered` | | `qty` |
| Receipt | Received quantity | `VendPackingSlipTrans` | `qty_received` |
| Cancel | Cancelled quantity | | `qty_cancelled` |
| Price | `LineDisc`, `LinePercent` | | GAP |
| Tax | `TaxGroup`, `TaxItemGroup` | | GAP |
| Product | `InventDimId` | `InventDim` | `storage_id` is warehouse only |
| Product | `InventTransId` | `InventTrans` | GAP |
| Source | Shop order line | | `shop_order_item_id` |
| Vendor | | | `supplier_id` on the line as well as the header |

## Product receipt

Action **Product receipt** writes `VendPackingSlipJour` / `VendPackingSlipTrans`.

| AX field | Links to | ECOM AE |
| --- | --- | --- |
| `PackingSlipId` | Number sequence | `epc_erp_po_receipts.receipt_no` |
| `DeliveryDate` | | `time_created` only. Delivery date is GAP |
| `PurchId` | `PurchTable.PurchId` | `po_id` |
| Line `ItemId`, `Qty`, `InventDimId`, `InventTransId` | Receipt inventory transaction | Receipt header has no lines. `qty_received` is a header total. Line quantity stays on `epc_erp_po_lines.qty_received` |
| `purchase_id` | Vendor invoice if invoiced on receipt | `epc_erp_po_receipts.purchase_id` and `epc_erp_purchases.id` |

Stock movement for the receipt is `epc_erp_inv_movements` with `movement_type = purchase_in` and `purchase_id`. The line still needs `InventTransId` before a partial receipt can be tied to one PO line and one batch. That column is GAP.

## Purchase agreement

Same shape as the sales agreement with `VendAccount` instead of `CustAccount`. Release sets `PurchTable` agreement id. GAP.

## Return purchase order

`PurchType = ReturnItem`, negative quantity. Product receipt ships stock out. Invoice is a vendor credit. Use the same `epc_erp_purchase_orders` row with a type column. Stock uses `movement_type = return_out`.
