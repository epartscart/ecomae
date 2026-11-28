# Production, projects, service, retail, and human resources

These menus are in the client. ECOM AE processes for them are open. Build on inventory movements and the GL journal. Do not add a second stock or a second ledger inside the module.

## Production control

| Section | Details form | Header fields | Line fields | Posts |
| --- | --- | --- | --- | --- |
| Production orders | `ProdTable` | `ProdId`, `ItemId`, `QtySched`, `ProdStatus` (Created, Estimated, Scheduled, Started, Reported finished, Ended), `InventDimId`, `BOMId`, `RouteId`, `DlvDate` | `ProdBOM`: `ItemId`, `BOMQty`, `InventDimId`, `InventTransId`. `ProdRoute`: `OprNum`, `OprId`, hours | Estimate locks the cost. Start picks components (`InventTrans` issue). Report as finished receives the finished item. End posts variances to the GL |
| BOM | `BOMTable` / `BOM` | `BOMId`, `ItemId` (finished), site | Component `ItemId`, `BOMQty`, unit, `InventDimId` | No stock until a production order starts |
| Routes | `RouteTable` / `Route` | `RouteId`, `ItemId` | Operation, resource `WrkCtrId`, time | Cost comes from the cost category |
| Resources | `WrkCtrTable` | `WrkCtrId`, type (machine, tool, vendor, HR) | Cost categories | GAP |
| Costing | `InventItemPrice` / cost sheet | `ItemId`, version, from date, price | Cost groups | Standard cost is GAP. On-hand cost today is `avg_unit_cost` |

Status must move forward only. Reported finished writes `epc_erp_inv_movements` receipt for the finished `item_id` and issues for components. The voucher is `source_type = adjustment` until a `production` source type is added. Do not add that enum value without the posting service knowing it.

## Master planning

`ReqTrans` and `ReqPO`: item, `InventDimId`, requirement date, quantity, reference (sales line, production BOM, safety stock). Firming a planned order creates `PurchTable` or `ProdTable` and stores the reference. ECOM AE forecast tables are `epc_erp_inv_demand_forecast` and `epc_erp_inv_policies` / `epc_erp_inv_service_levels`. A forecast row is not a purchase order. Firming is the step that creates `epc_erp_purchase_orders`.

## Project management and accounting

| Section | Form | Fields | Ledger link |
| --- | --- | --- | --- |
| Projects | `ProjTable` | `ProjId`, `Name`, `CustAccount`, `ProjGroupId`, status, `DefaultDimension`, contract id | `epc_erp` project budget tables if present. Fit-out uses the project process, not a new GL |
| Journals | `ProjJournalTrans` hour, fee, item, expense | `ProjId`, category, `Qty`, `CostPrice`, `SalesPrice`, worker or item | Item journal also writes `InventTrans`. All types can post WIP or P&L through the project group accounts |
| Invoice proposal | `ProjProposalJour` | `ProjInvoiceId`, `ProjId`, lines selected from posted project transactions | Posts `CustInvoiceJour` and `CustTrans`. The project id stays on the line |
| Revenue recognition | Estimate form | Percent complete, contract value, cost to date | Journal only. Do not edit prior project cost rows |

`ProjGroup` holds the ledger accounts (WIP cost, WIP invoiced, cost, payroll allocation, revenue). Those accounts are GAP until the project group form exists.

## Service management

| Section | Form | Fields | Links |
| --- | --- | --- | --- |
| Service orders | `SMAServiceOrderTable` | `ServiceOrderId`, `CustAccount`, `AgreementId`, status, preferred technician | Lines: item or hour, `Qty`, `ProjId` optional |
| Agreements | `SMAAgreementTable` | `AgreementId`, customer, start, end, interval | Invoice proposal creates a free-text or sales invoice |
| Repair | Repair line | `ItemId`, serial, symptom, resolution | Serial is GAP on `epc_erp_inv_items` |

Service cost and billing post through inventory and the customer invoice. A service order is not a second sales order table if it sells a stocked item. Link `SalesId` when a part is sold.

## Retail

| Section | Form | Fields | Posts |
| --- | --- | --- | --- |
| Stores | `RetailStoreTable` | `StoreNumber`, warehouse, customer for the store, tax group | The store warehouse is `InventLocationId` |
| Terminals | `RetailTerminalTable` | `TerminalId`, `StoreNumber` | |
| Statements | `RetailStatementTable` | `StatementId`, store, date, status | Calculated lines: sales, returns, tender. Posting creates sales invoices or customer transactions and the tender payment |
| Tender | Payment transaction | Tender type, amount, `StatementId` | Maps to `epc_erp_cash_bank_entries.entry_type` and a card clearing account |

POS in ECOM AE must end as an ERP sale: stock `sale_out`, customer or cash transaction, and one journal. A POS table that never reaches `epc_erp_gl_journals` is not posted.

## Human resources and payroll

| Section | Form | Fields | Ledger |
| --- | --- | --- | --- |
| Workers | `HcmWorker` | `PersonnelNumber`, name, `DirPerson` | Linked from sales taker, buyer, asset responsible |
| Positions | `HcmPosition` | `PositionId`, department, reports-to | Department is an operating unit, which can be a financial dimension |
| Employment | `HcmEmployment` | Worker, legal entity, start, end | The company is the same `DataAreaId` |
| Payroll (country pack) | Pay statement | Earning lines, deduction lines, tax lines, net | Generate does not post. Approve then pay writes the cash entry and the GL: expense and liability. UAE WPS is a file from the pay statement, not a second payroll |

`epc_erp` payroll that only writes `epc_erp_cash_bank_entries` is the payment. The expense and the liability still need `epc_erp_gl_lines`. Do not post payroll with a private journal.

## Travel and expense

`TrvExpTable` / `TrvExpTrans`: expense report id, worker, `TransDate`, category, amount, `TaxGroup`. Approval then posting credits the worker (vendor or employee vendor) and debits the expense account. Reimbursement is a vendor payment. Map the worker to `counterparty_type` only after an employee vendor exists. Until then the report is GAP.
