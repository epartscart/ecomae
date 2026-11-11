# Inventory, warehouse, and product information

Two menus: **Product information management** and **Inventory and warehouse management**. R3 adds the **Warehouse management** area (`WHS*`). Classic location journals stay on `WMS*`. Build classic inventory on the tables that exist. Add R3 work, wave, and license plate only when a warehouse process needs them. Do not create both models for the same movement.

## Product information — menu sections

| Section | Details form |
| --- | --- |
| Products | `EcoResProduct` (shared product) |
| Released products | `InventTable` |
| Product categories | `EcoResCategory` |
| Units | `UnitOfMeasure` |
| Dimension groups | `EcoResStorageDimensionGroup`, `EcoResTrackingDimensionGroup`, `EcoResProductDimensionGroup` |

### Released product — `InventTable`

FastTabs: **General, Purchase, Sell, Manage inventory, Engineer, Financial dimensions, Product dimensions**.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| General | `ItemId` | Key | `epc_erp_inv_items.sku` |
| General | `NameAlias` / product name | `EcoResProductTranslation.Name` | `name` |
| General | `ItemType` | Item, Service, BOM | `item_type`: standard, perishable, serialized. Service and BOM are GAP |
| General | `Product` | `EcoResProduct.RecId` | `product_id` (storefront product) |
| General | `ItemGroupId` | `InventItemGroup.ItemGroupId` | `epc_erp_pm_inv_groups`. GAP on the item row |
| Purchase | `TaxItemGroupId` (purch) | | GAP |
| Sell | `TaxItemGroupId` (sales) | | GAP |
| Sell | `SalesUnitId` | `UnitOfMeasure` | `unit` |
| Inventory | `BatchNumGroupId`, `SerialNumGroupId` | Number sequences | `track_expiry`. Serial group is GAP. The tracking setup form itself is counted in `11-item-tracking-fields.md` |
| Inventory | `modelGroupId` | `InventModelGroup` (stocked, FIFO/weighted, quarantine, registration) | Group `valuation` is `weighted_avg` on `epc_erp_pm_inv_groups`. FIFO, standard, and moving average are GAP |
| Inventory | `DimGroupId` (storage, tracking, product) | Which `InventDim` fields are required | GAP. Field counts for the three group forms and the inventory parameters are in `11-item-tracking-fields.md` and `12-inventory-setup-field-counts.md` |
| Dimensions | `DefaultDimension` | | `epc_erp_dim_links` |
| Extra | Tenant fields | | `epc_erp_inv_item_fields` (`item_id`, `field_key`, `value`) defined by `epc_erp_inv_field_defs` |

`InventItemGroup` also holds the ledger accounts: inventory receipt, inventory issue, COGS, purchase expenditure, sales. Those accounts are GAP on `epc_erp_pm_inv_groups` (`code`, `name`, `valuation`, `note`, `active` only). Posting looks up the account from the item group. Do not hard-code the account on the movement.

### Product dimensions and variants

`EcoResProductMaster` plus `InventDimCombination` (`ItemId` + `InventDimId` for the variant). Distinct product is a separate `ItemId`. ECOM AE stores `variant_label` on `epc_erp_inv_stock`. That is one text field, not size/color/config. Add real dimension columns only when a process posts them. Do not create a parallel item master for each variant.

## Inventory management — menu sections

| Section | List / details | Table |
| --- | --- | --- |
| On-hand | On-hand list | `InventSum` |
| Transactions | Inventory transactions | `InventTrans` |
| Journals | Movement, inventory adjustment, transfer, BOM, counting, item arrival, production input | `InventJournalTable` / `InventJournalTrans` |
| Transfer orders | Transfer orders | `InventTransferTable` / `InventTransferLine` |
| Warehouses and sites | Sites, warehouses, locations | `InventSite`, `InventLocation`, `WMSLocation` |
| Closing | Inventory close and adjustment | `InventSettlement` |

### On-hand — `InventSum`

| AX field | Links to | ECOM AE |
| --- | --- | --- |
| `ItemId` | `InventTable` | `epc_erp_inv_stock.item_id` |
| `InventDimId` | `InventDim` | `warehouse_id`, `batch_no`, `variant_label`, `expiry_date` |
| `PhysicalInvent` | Posted quantity | `qty_on_hand` |
| `ReservPhysical` | Reserved | GAP. Do not reduce `qty_on_hand` for a reservation |
| `AvailPhysical` | Physical − reserved | Derive. Do not store a second quantity that can drift |
| `PostedValue` | Value | `qty_on_hand × avg_unit_cost` |
| `PostedQty` | Financial quantity | GAP if it must differ from physical |

### Inventory transaction — `InventTrans`

One row per issue or receipt. This is the line the sales packing slip, product receipt, and journals all point at with `InventTransId`.

| AX field | Meaning | ECOM AE |
| --- | --- | --- |
| `InventTransId` | Lot id shared by the order line and its issues | GAP on document lines |
| `ItemId` | | `item_id` |
| `InventDimId` | | Warehouse, batch, expiry on `epc_erp_inv_movements` |
| `StatusIssue` / `StatusReceipt` | None, OnOrder, ReservPhysical, Picked, Deducted, Sold / Purchased, Received | `movement_type`: opening, purchase_in, sale_out, transfer_in, transfer_out, adjustment, return_in, return_out |
| `Qty` | Signed | `qty` |
| `CostAmountPosted` | | `unit_cost`, `total_cost` |
| `Voucher` | | GAP on the movement. Journal link is GAP |
| `DatePhysical`, `DateFinancial` | | `movement_date` |
| `TransRefId` | SalesId, PurchId, journal id | `order_id`, `purchase_id`, `reference` |
| `InvoiceId` | | GAP |

`epc_erp_inv_movements` also has `transfer_warehouse_id`, `opening_batch_id`, `admin_id`, `active`. A reversal sets `active = 0` or writes the opposite movement. Do not delete a posted movement.

### Inventory journal — `InventJournalTable` / `InventJournalTrans`

Header: `JournalId`, `JournalType` (Movement, LossProfit, Transfer, Count, BOM), `Description`, `Posted`, `InventSiteId`. Line: `ItemId`, `InventDimId`, `Qty`, `CostPrice`, `CostAmount`, offset account `LedgerDimension`. Counting uses `Counted` and `InventOnHand` and posts the difference. ECOM AE adjustment is `movement_type = adjustment`. There is no journal header. Add a header only when several lines must post as one voucher. The voucher is still `epc_erp_gl_journals`.

### Transfer order — `InventTransferTable` / `InventTransferLine`

| AX field | ECOM AE |
| --- | --- |
| `TransferId` | GAP as a document number. Two movements exist: `transfer_out` and `transfer_in` |
| `InventLocationIdFrom`, `InventLocationIdTo` | `warehouse_id` and `transfer_warehouse_id` |
| Line `ItemId`, `QtyTransfer`, `QtyShipped`, `QtyReceived`, `InventDimId` | GAP per line. A transfer with no line id cannot be partial |
| Ship / receive | Two `InventTrans` lots | Two movement rows. They must share one reference |

### Site, warehouse, location

| AX | Fields | ECOM AE |
| --- | --- | --- |
| `InventSite` | `SiteId`, `Name`, `DefaultDimension` | GAP |
| `InventLocation` | `InventLocationId`, `Name`, `InventSiteId` | `epc_erp_inv_warehouses`: `code`, `name`, `storage_id`, `active` |
| `WMSLocation` | `wMSLocationId`, `InventLocationId`, zone | GAP |
| Closing snapshot | Period, item, dim, qty, value | `epc_erp_inv_closing`: `period_end`, `warehouse_id`, `item_id`, `qty_closing`, `avg_unit_cost`, `value_closing` |

## Warehouse management (R3) — add only with the WMS process

| Section | Form | Key fields | Link |
| --- | --- | --- | --- |
| Waves | `WHSWaveTable` | `WaveId`, status, warehouse | Lines are shipments or loads |
| Work | `WHSWorkTable` / `WHSWorkLine` | `WorkId`, work type (pick, put), `ItemId`, `InventDimId`, target license plate | Created from a shipment or receipt. Completing work posts `InventTrans` |
| Loads | `WHSLoadTable` | `LoadId`, `InventLocationId` | Links to sales lines |
| License plates | `WHSLicensePlate` | `LicensePlateId` | An `InventDim` field |
| Locations | `WMSLocation` with location profile | | GAP |
| Cycle count | Cycle count plan and work | | Counting journal is the classic path. Use that until work-based counting is required |

Reservation in AX is `InventTrans` status `ReservPhysical`, not a cut of on-hand. ECOM AE must keep `qty_on_hand` as the physical quantity and store reserved quantity apart from it.
