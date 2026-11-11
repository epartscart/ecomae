# AX 2012 architecture mapped onto ECOM AE

Audience: the build agent [Deliver AX 2012 client study](bc-ecd100be-8b3f-537d-894e-7207473d4296). Field files in this folder are the form contract. This file is the system contract. `Ax32.exe` was not started. Custom models were not read.

ECOM AE law stays in `docs/migration/PROJECT_ARCHITECTURE_INSTRUCTIONS.md` and `docs/migration/ERP_COMPLETION_DIRECTIVE.md`. One posting service. One set of document tables. PHP remains the data contract until a process is accepted. This map does not replace that law and does not accept a process.

## 1. AX runtime

```
Ax32.exe (32-bit client, kernel 6.0)
    TCP 2712
Ax32Serv.exe (AOS)
    TDS                         net.tcp services (WSDL 8101, endpoint 8201)
SQL Server                      SSRS, Enterprise Portal, Workflow, Batch
  business data
  model store (metadata, X++, security)
```

| Piece | Owns | Does not own |
| --- | --- | --- |
| `Ax32.exe` | Navigation pane, list page, details form, action pane, infolog | Posting, security decision, on-hand, voucher |
| AOS | X++ (mostly CIL), number sequences, inventory status changes, ledger post | The pixels |
| SQL Server business DB | Companies, documents, inventory transactions, ledger entries | Form layout |
| Model store | Tables, forms, classes, menu items, privileges, layers, models | Customer balances |
| SSRS | Operational reports | Posting |
| Enterprise Portal | Role center pages hosted in the client | The rich client itself |
| Workflow | Assignments and approvals | A second copy of the document |
| Batch | Recurrence of the same post methods | A private ledger |
| AIF / DIXF | Inbound and outbound documents | A side door that skips the form's post method |

Default ports are from Microsoft’s AX 2012 port topics, already cited in `docs/migration/AX2012_AX32_CLIENT_STUDY.md`.

## 2. ECOM AE runtime

```
Browser
    HTTP
ASP.NET Core (EcomAE.Platform)          PHP (authoritative until acceptance)
    MariaDB, one schema per tenant
    epc_erp_* tables
    ErpGlPostingService.PostJournalAsync
```

| Piece | Owns |
| --- | --- |
| ASP.NET ERP services under `aspnet/src/EcomAE.Platform/Erp` | The write path for a document when `confirmWrites` is true |
| PHP under `content/shop/finance` | The current data contract. A new column must match PHP or be added in both |
| `epc_erp_gl_journals` / `epc_erp_gl_lines` | The only accounting result |
| Control Panel | Users, roles, tenant configuration. It does not post |
| BOS and CRM | Read and hand off. They do not insert orders, invoices, or journals |
| Storefront | Creates a shop order that ERP fulfills. It is not a second sales ledger |

There is no AOS process. The ASP.NET service is the AOS. The Razor page is the client. MariaDB is both the business database and, today, the only metadata store. There is no model store and no MorphX layer.

## 3. Layer mapping

| AX layer | ECOM AE |
| --- | --- |
| Client form | ASP.NET ERP page. Header FastTabs and a line grid. Action pane calls the write service |
| AOS class | The `Erp*WriteService` for that document |
| Number sequence | `epc_erp_voucher_sequences` (`voucher_type`, `year`, `last_seq`) |
| `DataAreaId` | Legal entity on the request. Master: `epc_erp_pm_legal_entities` |
| `InventDim` | Warehouse, batch, expiry, variant label on stock and movements. Site, location, pallet, serial, size, color, config, style are not columns yet |
| `InventTrans` | `epc_erp_inv_movements`. The lot id `InventTransId` is not on the order line yet |
| `InventSum` | `epc_erp_inv_stock` |
| `CustTrans` / `VendTrans` | Customer side is the invoice open amount. Vendor side is `epc_erp_supplier_accounting` |
| Settlement | `epc_erp_settlement_allocations` |
| `GeneralJournalEntry` | `epc_erp_gl_journals` plus `epc_erp_gl_lines` |
| `LedgerDimension` | `epc_erp_gl_lines.coa_id`. Dimension combination on the line is missing |
| `DefaultDimension` | `epc_erp_dim_links` |
| Security role / duty / privilege | Control Panel role and the permission that shows Post. Duty is not a separate table yet |
| Workflow | Existing ERP workflow. One engine |
| Batch | The same write service, run later. Not a second post method |
| SSRS | Print reads the posted document. Print does not post |
| Infolog | The error list returned by the write service |

## 4. Call path for every post

1. The page sends the header id and `confirmWrites: true`.
2. The write service checks company, permission, status, and the document's own rules (draft versus posted).
3. It writes the next document in the chain in section 5.
4. If the document changes stock, it writes `epc_erp_inv_movements` and updates `epc_erp_inv_stock`.
5. If the document is an invoice or a payment, it writes the customer or vendor subledger row.
6. Last, it calls `ErpGlPostingService.PostJournalAsync` with `source_type` and `source_id`.
7. The source document stores `gl_journal_id` or `voucher_no`.
8. Failure rolls the document back. A posted row is never edited. Reversal is a new journal.

Known services, already in the tree:

| Document | Service |
| --- | --- |
| Sales order | `ErpSalesOrderWriteService` |
| Sales invoice | `ErpSalesInvoiceWriteService` |
| Purchase order | `ErpPurchaseOrderWriteService` |
| Vendor invoice | `ErpPurchaseInvoiceWriteService` |
| Inventory movement | `ErpInventoryMovementWriteService` |
| Cash and bank | `ErpCashWriteService` |
| Settlement | `ErpSettlementAllocationService` |
| Ledger | `ErpGlPostingService` |

A new AX form does not get a new posting class. It gets a write service that ends in `PostJournalAsync`.

## 5. Document chains

```
quotation → sales order → confirmation → packing slip → sales invoice → customer transaction → settlement → journal
requisition → RFQ → purchase order → product receipt → vendor invoice → vendor transaction → settlement → journal
item + inventory dimensions → inventory movement → on-hand
production start → component issue; report as finished → receipt; end → variance journal
project item journal → movement; project invoice → customer invoice → journal
retail statement → sale movement + tender cash entry → journal
payroll pay → cash entry + expense and liability lines → journal
```

Foreign keys already in the database:

| From | Column | To |
| --- | --- | --- |
| `epc_erp_sales_order_lines` | `sales_order_id` | `epc_erp_sales_orders.id` |
| `epc_erp_sales_orders` | `sales_invoice_id` | sales invoice row |
| `epc_erp_po_lines` | `po_id` | `epc_erp_purchase_orders.id` |
| `epc_erp_po_receipts` | `po_id` | `epc_erp_purchase_orders.id` |
| `epc_erp_purchases` | `po_id` | `epc_erp_purchase_orders.id` |
| `epc_erp_purchase_inv_lines` | `purchase_id` | `epc_erp_purchases.id` |
| `epc_erp_purchase_inv_lines` | `movement_id` | `epc_erp_inv_movements.id` |
| `epc_erp_supplier_accounting` | `purchase_id`, `cash_entry_id` | invoice and payment |
| `epc_erp_cash_bank_entries` | `order_id`, `purchase_id`, `transfer_pair_id`, `account_id` | order, invoice, the other transfer leg, bank |
| `epc_erp_gl_lines` | `journal_id`, `coa_id` | journal and main account |
| `epc_erp_gl_journals` | `source_type`, `source_id` | the document that posted |
| `epc_erp_inv_stock` | `warehouse_id`, `item_id` | warehouse and item |
| `epc_erp_pm_business_units` | `legal_entity_id` | legal entity |
| `epc_erp_pm_dimension_values` | `dimension_id` | dimension |

`source_type` values that exist: `manual`, `sales`, `purchase`, `payment`, `cash`, `opening`, `adjustment`, `sales_invoice`. A new chain adds a value only when `ErpGlPostingService` knows it.

## 6. Module to process

| AX menu | ECOM AE process | Field file | Build status |
| --- | --- | --- | --- |
| Sales and marketing | Order-to-Cash | `02-sales-and-marketing.md` | Header and lines exist. Delivery, tax, site, and lot id do not |
| Accounts receivable | Invoice and collection | `03-accounts-receivable.md` | Invoice link exists. Open `CustTrans` and settlement are thin |
| Procurement and sourcing | Procure-to-Pay | `04-procurement-and-sourcing.md` | PO, lines, receipt header exist. Requisition and RFQ do not |
| Accounts payable | Vendor invoice and payment | `05-accounts-payable.md` | `epc_erp_purchases` and supplier accounting exist. Match tolerances do not |
| Product and inventory | Inventory / WMS | `06`, `11`, `12` | Stock and movements exist. Counted setup forms are not columns yet |
| General ledger | Record-to-Report | `07-general-ledger.md` | Journal header and lines exist. Account structure enforcement does not |
| Cash and bank, fixed assets | Treasury, assets | `08-cash-bank-assets.md` | Cash entries exist. Asset header does not |
| Production, project, service, retail, HR | Those processes | `09-production-project-service-retail-hr.md` | Use movements and the journal. Do not add a private stock |
| Organization and system | Company, numbers, security | `10-organization-and-system.md` | Legal entity and voucher sequence exist. Duties do not |

## 7. Counted fields the architecture already depends on

| Form | Count | File |
| --- | --- | --- |
| Tracking dimension groups | 12 controls, 21 values | `11-item-tracking-fields.md` |
| Item model groups | 19 | `12-inventory-setup-field-counts.md` |
| Storage dimension groups | 38 | `12-inventory-setup-field-counts.md` |
| Product dimension groups | 11 | `12-inventory-setup-field-counts.md` |
| Inventory parameters | 72 named fields | `12-inventory-setup-field-counts.md` |

Every other form is named in the module files or in the uncounted list in `12-inventory-setup-field-counts.md`. A builder does not add a column for a form that has no count.

## 8. Setup order

Use the order in `10-organization-and-system.md`: legal entity, number sequence, posting accounts, terms and tax, site and warehouse, Post permission, header and lines, inventory movement, subledger, journal. Inventory setup from file 12 sits in the posting-accounts and warehouse steps. Item model group 19 and tracking 21 are required before a serialized or batch-controlled item can post.
