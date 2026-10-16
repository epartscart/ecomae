# AX 2012 field map — how to build from it

Audience: [Devin.ai migration review](https://cursor.com/agents/bc-d97081dc-aff3-46b1-a904-2a60549a5dbd). Parent study: `docs/migration/AX2012_AX32_CLIENT_STUDY.md`.

This folder is a build contract for the main documents, not a census of every field on every form. Item tracking is the worked example of the missing depth: `11-item-tracking-fields.md`. Do not tell a builder that the module files already list each control.

Each module file lists the main menu sections, the list page, the details form, the FastTabs, the business fields on those sections, and the key that links the field to the next table. The client was not launched. These are standard AX 2012 form fields (R2/R3 shape). Custom fields in a CUS or VAR model were not observed. Do not invent them.

## Rules for the builder

1. Extend the ECOM AE table named in the "ECOM AE" column. Do not create a second sales, purchase, inventory, invoice, or ledger table to mirror an AX name.
2. Post only through `ErpGlPostingService.PostJournalAsync`. A document link to `epc_erp_gl_journals.id` is the accounting result. A form must not write `epc_erp_gl_lines` by itself.
3. Every business document carries the legal entity. AX stores that as `DataAreaId` on every row. ECOM AE stores it as the company context already required by `docs/migration/D365_FO_STRUCTURE_PARITY_AUDIT.md`. A line inherits the header company. A line does not pick a different company.
4. System columns exist on every AX table and are not repeated on each form: `RecId` (surrogate key), `DataAreaId` (company), `Partition` (R2+), `RecVersion`, `CreatedDateTime`, `CreatedBy`, `ModifiedDateTime`, `ModifiedBy`. Map them to `id`, the company id, and `time_created` / `time_updated` / `admin_id`. Do not show them as business fields.
5. A "GAP" cell means the AX field has no column yet. Add the column on the named table when that process is built. Do not add every GAP in one pass. Build one document chain at a time: header, lines, inventory link, tax link, subledger, journal.
6. Status on the ECOM AE header is a short enum. AX status is an integer enum. Store the AX name (`Backorder`, `Delivered`, `Invoiced`) only if the existing enum cannot say it. Do not replace `draft|confirmed|invoiced|cancelled` until the posting chain sets the new value.
7. Quantities and money use `decimal`. Do not add `double` columns. Header totals are the sum of rounded line amounts, not a second price list.
8. Delete is not the AX pattern. Posted documents are reversed. Draft documents may be cancelled. The existing sales-order and purchase-order services already follow that split. Keep it.

## How a details form is built

AX opens a list page, then a details form with two views.

- **Header view.** FastTabs are the sections. One row in the header table.
- **Line view.** The grid is the lines. The line FastTabs edit the selected line. The line row stores the foreign keys, not a copy of the header.
- **Related information.** FactBoxes read other tables by the keys below. They are not extra copies of the document.

The action pane posts. Posting writes the next document in the chain and then the journal. The infolog is the message list that comes back. It is not a table.

## Shared link keys

Use these keys everywhere. Module files refer to them by name.

| Key | AX | Points at | ECOM AE |
| --- | --- | --- | --- |
| Company | `DataAreaId` | `CompanyInfo.DataArea` | Legal entity on the request. Masters: `epc_erp_pm_legal_entities.id` |
| Customer | `CustAccount` / `AccountNum` | `CustTable.AccountNum` | `epc_erp_sales_orders.customer_user_id` |
| Invoice account | `InvoiceAccount` | `CustTable.AccountNum` (may differ from the order customer) | GAP on `epc_erp_sales_orders` |
| Vendor | `OrderAccount` / `InvoiceAccount` | `VendTable.AccountNum` | `epc_erp_purchase_orders.supplier_id`, `epc_erp_purchases.supplier_id` |
| Item | `ItemId` | `InventTable.ItemId` | `epc_erp_inv_items.sku` and line `item_code` |
| Inventory dimension | `InventDimId` | `InventDim.inventDimId` | Split columns today: `warehouse_id`, `batch_no`, `expiry_date`, `variant_label` on `epc_erp_inv_stock`. Site, location, serial, size, color, config, style are GAP |
| Inventory transaction | `InventTransId` | `InventTrans.InventTransId` | GAP. Movements are `epc_erp_inv_movements.id`, linked by `order_id` or `purchase_id`, not by a line transaction id |
| Ledger dimension | `LedgerDimension` | `DimensionAttributeValueCombination.RecId` | `epc_erp_gl_lines.coa_id` plus GAP for dimension combination |
| Default dimension | `DefaultDimension` | `DimensionAttributeValueSet.RecId` | `epc_erp_dim_links` (entity type + entity id + dimension value) |
| Voucher | `Voucher` / `LedgerVoucher` | `GeneralJournalEntry.SubledgerVoucher` | `epc_erp_gl_journals.journal_no`, and `voucher_no` / `gl_journal_id` on the source document |
| Number sequence | `NumberSequence` scope + reference | Issued number on the document | `epc_erp_voucher_sequences` (`voucher_type`, `year`, `last_seq`) |

## Inventory dimension fields (one row, many documents)

`InventDim` is not a form the user posts. It is the section "Dimensions" on sales lines, purchase lines, inventory journals, and production lines. One `InventDimId` expands to:

| AX field | Meaning | ECOM AE |
| --- | --- | --- |
| `InventSiteId` | Site | GAP |
| `InventLocationId` | Warehouse | `epc_erp_inv_warehouses.id` (lines often store `storage_id` instead) |
| `wMSLocationId` | Location inside the warehouse | GAP |
| `inventBatchId` | Batch | `epc_erp_inv_stock.batch_no`, `epc_erp_inv_movements.batch_no` |
| `inventSerialId` | Serial | GAP (`item_type = serialized` exists, no serial row) |
| `InventStatusId` | Inventory status (R3) | GAP |
| `LicensePlateId` | License plate (R3) | GAP |
| `configId` | Configuration | GAP |
| `InventSizeId` | Size | GAP (variant text only: `variant_label`) |
| `InventColorId` | Color | GAP |
| `InventStyleId` | Style | GAP |

On-hand is `InventSum` by `ItemId` + `InventDimId`. ECOM AE on-hand is `epc_erp_inv_stock` by `warehouse_id` + `item_id` + `batch_no` + `variant_label`. Do not create a second on-hand table.

## Financial dimension fields (one section, many documents)

The FastTab "Financial dimensions" is the same control on customers, vendors, items, orders, and journals. Values come from `DimensionAttribute` / `DimensionAttributeValue`. ECOM AE masters are `epc_erp_pm_dimensions` and `epc_erp_pm_dimension_values`. Assignments are `epc_erp_dim_links`. A posted journal line still needs the combination stored with `epc_erp_gl_lines`. That combination column is GAP.

## Document chain the builder must not break

```
quotation → sales order → confirmation → packing slip → sales invoice → customer transaction → settlement → GL journal
requisition → RFQ → purchase order → product receipt → vendor invoice → vendor transaction → settlement → GL journal
item + inventory dimension → inventory transaction → on-hand
any posted document → voucher → epc_erp_gl_journals.id → epc_erp_gl_lines
```

Each arrow is a foreign key in the module file. If a step does not exist yet, leave the previous document's link column at 0 and do not skip ahead to the journal without the source document id (`source_type`, `source_id`).

## Module index

| File | AX menu | ECOM AE process |
| --- | --- | --- |
| `02-sales-and-marketing.md` | Sales and marketing | Order-to-Cash, up to invoice |
| `03-accounts-receivable.md` | Accounts receivable | Invoice, collection, settlement |
| `04-procurement-and-sourcing.md` | Procurement and sourcing | Procure-to-Pay, up to receipt |
| `05-accounts-payable.md` | Accounts payable | Vendor invoice, match, payment |
| `06-inventory-and-product.md` | Inventory, warehouse, product information | Inventory / WMS |
| `11-item-tracking-fields.md` | Tracking dimension groups, one form counted | Not a full inventory census |
| `07-general-ledger.md` | General ledger | Record-to-Report |
| `08-cash-bank-assets.md` | Cash and bank, fixed assets | Treasury, fixed assets |
| `09-production-project-service-retail-hr.md` | Production, projects, service, retail, HR | Those open processes |
| `10-organization-and-system.md` | Organization administration, system administration | Company, number sequences, security, batch |
