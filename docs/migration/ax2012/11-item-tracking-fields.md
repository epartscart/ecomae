# Item tracking — field count

This is the depth check for Inventory management. The earlier inventory file named batch and serial as a gap. It did not open the tracking form. The count below is from Microsoft’s form topic for AX 2012, AX 2012 Feature Pack, R2, and R3: [Tracking dimension groups (form)](https://learn.microsoft.com/en-us/previous-versions/dynamicsax-2012/tracking-dimension-groups-form).

Path: **Product information management > Setup > Dimension groups > Tracking dimension groups**. R3 also opens it from Inventory management. The form is `EcoResTrackingDimensionGroup`.

## What “how many fields” means

Microsoft names **12 controls** on the base form.

| # | Control | Once, or once per dimension row |
| --- | --- | --- |
| 1 | Name | Once, on the group |
| 2 | Description | Once, on the group |
| 3 | Serial number control | Serial row only. Forces quantity 1 and drives Physical inventory |
| 4 | Active | Each dimension row |
| 5 | Primary stocking | Each dimension row |
| 6 | Blank receipt allowed | Each dimension row. Allowed only when Primary stocking is clear |
| 7 | Blank issue allowed | Each dimension row. Allowed only when Primary stocking is clear |
| 8 | Physical inventory | Each dimension row |
| 9 | Financial inventory | Each dimension row |
| 10 | Coverage plan by dimension | Each dimension row |
| 11 | For purchase prices | Each dimension row |
| 12 | For sales prices | Each dimension row |

The topic names **two dimension rows**: batch number and serial number.

Settable values on that base form: 2 header fields + 9 checkboxes on the batch row + 9 checkboxes on the serial row + Serial number control = **21**.

## R3 controls the same topic does not list

Microsoft says the Warehouse management tabs added in AX 2012 R3 are not described on that page. Two of them are documented separately ([serial number tracking in R3](https://www.microsoft.com/en-us/dynamics-365/blog/no-audience/2014/07/02/serial-number-tracking-for-service-and-warranty-in-microsoft-dynamics-ax-r3/)):

| Control | Where | Rule |
| --- | --- | --- |
| Active in sales process | Serial row | On-hand can exist without a serial. The serial is registered at packing slip or invoice |
| Capture serial | Serial row | Advanced warehousing only. Defaults to None and locks when Active in sales process is selected |

Those two raise the R3 serial row by 2. They are not a claim that the R3 form has only 23 values. The warehouse tabs are not fully listed here because the form topic omits them, and this client was not opened.

## Where the values are stored

| UI | Table | Key |
| --- | --- | --- |
| Group name and description | `EcoResTrackingDimensionGroup` | `Name` |
| One row per dimension flag set | `EcoResTrackingDimensionGroupFldSetup` | Group + dimension field (batch or serial) |
| Product uses one group | `InventTable.trackingDimensionGroup` | Released product → group name |
| A posted line stores the values | `InventDim` | `inventBatchId`, `inventSerialId`, then `InventDimId` on the order line and on `InventTrans` |

ECOM AE today: `epc_erp_inv_items.track_expiry` and `item_type = serialized`. There is no tracking-dimension-group table and no serial row. `epc_erp_inv_stock.batch_no` is one batch string on on-hand, not the 9 flags.

## Forms that sit next to this one and are not counted yet

| Form | Path | What it holds |
| --- | --- | --- |
| Batches | Inventory management > Inquiries > Dimensions > Batches | `InventBatch`: batch id, item, manufacturing date, expiry, vendor batch, disposition |
| Serial numbers | Inventory management > Inquiries > Dimensions > Serial numbers | `InventSerial`: serial id, item, manufacturing date |
| Item tracing | Inventory management > Inquiries > Tracing > Item tracing | Item number plus batch or serial, trace direction, then the transaction tree |
| Register serial numbers | Packing slip or invoice > Lines > Update line | One serial per unit at sale time when Active in sales process is on |
| Item model groups | Inventory management > Setup | Physical negative inventory, which the Physical inventory flag depends on |
| Batch number groups and serial number groups | Inventory setup | How the number is generated. These are `BatchNumGroupId` and `SerialNumGroupId` on the item, not the 12 flags above |

A builder who only reads “batch and serial are GAP” will add two columns and miss the 9 flags, the serial quantity rule, and the sales-process path that must not create an on-hand row per serial.
