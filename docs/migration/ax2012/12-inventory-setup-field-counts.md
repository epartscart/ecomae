# Inventory setup — counted forms

Same rule as `11-item-tracking-fields.md`. A number below is the count of controls on the Microsoft form topic. `Ax32.exe` was not opened. R3 warehouse tabs that a topic says it omits are not in the count.

## Item model groups — 19 fields

Path: Inventory management > Setup > Inventory > Item model groups. Topic: [Item model groups (form)](https://learn.microsoft.com/en-us/previous-versions/dynamicsax-2012/item-model-groups-form). Table: `InventModelGroup`. The released product stores the group id.

Tabs on the topic: Setup, Inventory model.

| # | Field | What it controls |
| --- | --- | --- |
| 1 | Item model group | Group id |
| 2 | Name | Description |
| 3 | Stocked product | If clear, no on-hand and no inventory transaction |
| 4 | Physical negative inventory | Picking may go below physical on-hand |
| 5 | Financial negative inventory | Invoicing may go below financially updated quantity |
| 6 | Quarantine management | Registration creates a quarantine order |
| 7 | Consolidated picking method | Multi-order picking |
| 8 | Post physical inventory | Packing slip and product receipt hit the ledger |
| 9 | Post financial inventory | Invoice hits inventory receipt and issue accounts |
| 10 | Post to Deferred Revenue Account on Sales Delivery | Accrue revenue at packing slip |
| 11 | Accrue liability on product receipt | Accrue cost at product receipt |
| 12 | Registration requirements | Status must be Registered before product receipt |
| 13 | Receiving requirements | Product receipt before vendor invoice |
| 14 | Picking requirements | Status must be Picked before packing slip. Ignored for direct delivery |
| 15 | Deduction requirements | Physical update before a negative financial update |
| 16 | Backward from ship date | Reserve the receipt closest to the ship date |
| 17 | Inventory model | FIFO, LIFO, LIFO date, weighted average, weighted average date, standard cost, moving average |
| 18 | Include physical value | Used by FIFO, LIFO, and LIFO date. Not used by the weighted-average models |
| 19 | Fixed receipt price | Receipts and issues adjust to the price on the released product |

ECOM AE: `epc_erp_pm_inv_groups.valuation` is only `weighted_avg`. The other 18 controls have no column. Add them on that group table. Do not create a second item master.

Some configurations add table columns that this topic does not list (`PdsSameLot`, `MandatoryPick`, `ReserveByDate`, and other process-industry or country fields). They are not part of the 19.

## Storage dimension groups — 38 values

Path: Product information management > Setup > Dimension groups > Storage dimension groups. Topic: [Storage dimension groups (form)](https://learn.microsoft.com/en-us/previous-versions/dynamicsax-2012/storage-dimension-groups-form).

Header, once: Name, Description.

Flags on each row: Mandatory, Primary stocking, Active, Blank receipt allowed, Blank issue allowed, Physical inventory, Financial inventory, Coverage plan by dimension, For purchase prices, For sales prices. That is 9 flags.

Rows named by the topic: Site (always mandatory), Warehouse, Location (cannot be mandatory), Pallet ID (cannot be mandatory). 2 + 9 × 4 = **38**. Blank receipt and blank issue apply only when Primary stocking is clear.

R3 license plate, inventory status, and the warehouse tabs are omitted by the topic. Not counted.

Line values land in `InventDim`: `InventSiteId`, `InventLocationId`, `wMSLocationId`, `wMSPalletId`. ECOM AE has `epc_erp_inv_warehouses` only.

## Product dimension groups — 11 values

Path: Product information management > Setup > Dimension groups > Product dimension groups. Topic: [Product dimension groups (form)](https://learn.microsoft.com/en-us/previous-versions/dynamicsax-2012/product-dimension-groups-form).

Controls: Name, Description, and on each row Active, For purchase prices, For sales prices.

Rows named by the topic: configuration, size, color. 2 + 3 × 3 = **11**. Rename is a button, not a stored field. Configuration cannot be renamed.

`InventDim` also has style. This topic does not list a style row, so style is not inside the 11. ECOM AE `variant_label` is one text field, not these flags.

## Inventory and warehouse management parameters — 72 named fields

Path: Inventory management > Setup > Inventory and warehouse management parameters. Topic: [Inventory and warehouse management parameters (form)](https://learn.microsoft.com/en-us/previous-versions/dynamicsax-2012/inventory-and-warehouse-management-parameters-form). One row per company.

Sections on the topic: General, Journals, Bills of materials, Locations, Transport, AIF, RFID, Quality management, Inventory dimensions, Print management, Number sequences.

| Section | Count | Fields |
| --- | --- | --- |
| General | 19 | Outbound rule ID, End output inventory order, Bar code setup, Automatic addition, Maximum unit cost price, Reserve ordered items, Extra batch helpers, Helper batch group, Number of items per bundle, Post physical sales tax, Lock items during count, Unit, Pallet type, Manual sort codes, Check text, Calculate packing material fees, Last price history, Fallback warehouse, Return to |
| Journals | 8 | Default journal name for Movement, Inventory adjustment, Transfer, Counting, BOM, Tag counting, Item arrival, Production input |
| Bills of materials | 8 | Level of circularity, Max. levels, Mandatory quantity and dates, Block removal of approval, Block editing, Calculation group, Cost breakdown, Variances to standard |
| Locations | 3 | Standard width, Standard depth, Standard height |
| Transport | 7 | Input transport priority, Output transport priority, Refill transport priority, Reserve items automatically, Accept overdelivery, Accept underdelivery, Picking route status |
| AIF | 3 | Default journal for incoming Inventory adjustment, Transfer, Counting |
| RFID | 5 | Logging level, File name, EPC partition, EPC header, EPC manager |
| Quality | 2 | Use quality management, Hourly rate |
| Inventory dimensions | 17 | Display checkbox per area for the selected dimension: Movement, Inventory adjustment, Transfers, Counting, Bills of materials, Inventory transactions, Item arrival, Production input, Shipment lines, Transfer orders, Quarantine orders, Quality orders, Non conformances, Certificate of analysis, Inventory blocking, Transport lines, Picking lines |

19 + 8 + 8 + 3 + 7 + 3 + 5 + 2 + 17 = **72**. The number-sequence grid (Reference, Number sequence code) has one row per document and the topic does not list those references, so that row count is unknown. R3 warehouse parameters are a separate form and are not in the 72.

ECOM AE has no inventory-parameters row. Do not hard-code these defaults inside a posting method.

## Still uncounted in Inventory management

Do not invent fields for these forms.

| Area | Forms |
| --- | --- |
| Common | Released products, On-hand, Transactions, Transfer orders |
| Journals | Movement, Inventory adjustment, Transfer, Counting, Tag counting, Item arrival, Production input, BOM journal |
| Orders | Quarantine orders, Quality orders, Nonconformances, Inventory blocking |
| Periodic | Closing and adjustment, ABC classification |
| Inquiries | Batches, Serial numbers, Item tracing result columns |
| Setup | Item groups, Journal names, Warehouses, Sites, Locations, Batch number groups, Serial number groups, Counting groups |
