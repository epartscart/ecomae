# All modules, all standard forms

This is the full AX 2012 client menu for the build agent. Every standard area is here. Each row is a form the user can open from `Ax32.exe`.

Field status:

- **Counted** means a Microsoft form topic was read and the control count is in the linked file. Build those controls.
- **Fields listed** means the business fields and join keys are in the module file. Build those fields.
- **Named** means the form is real and the table is known, and the field list was not counted. Do not invent its controls.

`Ax32.exe` was not opened. Custom forms in a CUS, VAR, or ISV model are not on this list. R3 warehouse forms beyond the ones named here are omitted where the form topic said they were omitted.

## Sales and marketing

Field file: `02-sales-and-marketing.md`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | All sales quotations | `SalesQuotationTable` / `SalesQuotationLine` | Fields listed |
| Common | All sales orders | `SalesTable` / `SalesLine` | Fields listed |
| Common | All return orders | `SalesTable` with `SalesType = ReturnItem` | Fields listed |
| Common | Customers | `CustTable` | Fields listed in `03` |
| Common | Sales agreements | `AgreementHeader` / `AgreementLine` | Fields listed |
| Common | Prospects | `smmBusRelTable` | Fields listed |
| Journals | Confirmation journal | `CustConfirmJour` / `CustConfirmTrans` | Fields listed |
| Journals | Packing slip journal | `CustPackingSlipJour` / `CustPackingSlipTrans` | Fields listed |
| Journals | Invoice journal | `CustInvoiceJour` / `CustInvoiceTrans` | Fields listed in `03` |
| Setup | Modes of delivery | `DlvMode` | Named |
| Setup | Terms of delivery | `DlvTerm` | Named |
| Setup | Payment terms | `PaymTerm` | Named. Master exists: `epc_erp_pm_pay_terms` |
| Setup | Sales origins, sales pools, charges | `SalesOrigin`, `SalesPool`, `MarkupTable` | Named |
| Setup | Customer groups | `CustGroup` | Named. Master exists: `epc_erp_pm_customer_groups` |

## Accounts receivable

Field file: `03-accounts-receivable.md`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | All customers | `CustTable` | Fields listed |
| Common | Free text invoices | `CustInvoiceTable` / `CustInvoiceLine` | Fields listed |
| Common | Invoice journal | `CustInvoiceJour` / `CustInvoiceTrans` | Fields listed |
| Journals | Payments | `LedgerJournalTable` / `LedgerJournalTrans` | Fields listed |
| Collections | Collection letters | `CustCollectionLetterJour` | Fields listed |
| Collections | Interest notes | `CustInterestJour` | Fields listed |
| Collections | Aging | `CustTrans` by due date | Fields listed |
| Periodic | Settle open transactions | `CustSettlement` | Fields listed. Table: `epc_erp_settlement_allocations` |
| Setup | Accounts receivable parameters | `CustParameters` | Named |
| Setup | Customer posting profiles | `CustLedger` | Named. This picks the AR control account |
| Setup | Methods of payment | `CustPaymModeTable` | Named. Master: `epc_erp_pm_pay_methods` |
| Setup | Cash discounts | `CashDisc` | Named |
| Setup | Customer bank accounts | `CustBankAccount` | Named |

## Procurement and sourcing

Field file: `04-procurement-and-sourcing.md`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Purchase requisitions | `PurchReqTable` / `PurchReqLine` | Fields listed |
| Common | Requests for quotations | `PurchRFQCaseTable` / `PurchRFQTable` | Fields listed |
| Common | All purchase orders | `PurchTable` / `PurchLine` | Fields listed |
| Common | Purchase agreements | `AgreementHeader` | Fields listed |
| Common | Vendors | `VendTable` | Fields listed in `05` |
| Journals | Product receipt | `VendPackingSlipJour` / `VendPackingSlipTrans` | Fields listed |
| Setup | Procurement categories | `EcoResCategory` | Named |
| Setup | Purchasing policies | policy tables | Named |

## Accounts payable

Field file: `05-accounts-payable.md`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | All vendors | `VendTable` | Fields listed |
| Common | Pending vendor invoices | `VendInvoiceInfoTable` / `VendInvoiceInfoLine` | Fields listed |
| Common | Invoice journal | `VendInvoiceJour` / `VendInvoiceTrans` | Fields listed |
| Journals | Payments | `LedgerJournalTable` vendor payment | Fields listed |
| Journals | Checks | `epc_erp_pm_cheques` is the ECOM AE check | Fields listed |
| Periodic | Settle open transactions | `VendSettlement` | Fields listed |
| Setup | Accounts payable parameters | `VendParameters` | Named. Holds match tolerances |
| Setup | Vendor posting profiles | `VendLedger` | Named. Picks the AP control account |
| Setup | Vendor groups | `VendGroup` | Named. Master: `epc_erp_pm_vendor_groups` |
| Setup | Matching policy | invoice matching parameters | Named |
| Setup | Charges | `MarkupTrans` | Named |

## Product information management

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Released products | `InventTable` | Fields listed in `06` |
| Common | All products | `EcoResProduct` | Fields listed in `06` |
| Setup | Tracking dimension groups | `EcoResTrackingDimensionGroup` | Counted. 12 controls, 21 values. `11-item-tracking-fields.md` |
| Setup | Storage dimension groups | `EcoResStorageDimensionGroup` | Counted. 38 values. `12-inventory-setup-field-counts.md` |
| Setup | Product dimension groups | `EcoResProductDimensionGroup` | Counted. 11 values. `12` |
| Setup | Item groups | `InventItemGroup` | Named. Holds inventory, COGS, receipt, and issue accounts |
| Setup | Units | `UnitOfMeasure` | Named |
| Setup | Categories | `EcoResCategory` | Named |

## Inventory and warehouse management

Field files: `06-inventory-and-product.md`, `11`, `12`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | On-hand | `InventSum` | Fields listed |
| Common | Transactions | `InventTrans` | Fields listed |
| Common | Transfer orders | `InventTransferTable` / `InventTransferLine` | Fields listed |
| Journals | Movement | `InventJournalTable` type Movement | Fields listed |
| Journals | Inventory adjustment | `InventJournalTable` type LossProfit | Fields listed |
| Journals | Transfer | `InventJournalTable` type Transfer | Fields listed |
| Journals | Counting | `InventJournalTable` type Count | Fields listed |
| Journals | Tag counting | tag journal, then counting | Named |
| Journals | Bills of materials | `InventJournalTable` type BOM | Named |
| Journals | Item arrival | arrival journal | Named |
| Journals | Production input | production input journal | Named |
| Periodic | Closing and adjustment | `InventSettlement` | Named |
| Inquiries | Batches | `InventBatch` | Named in `11` |
| Inquiries | Serial numbers | `InventSerial` | Named in `11` |
| Inquiries | Item tracing | trace form | Named in `11`. Input fields only |
| Orders | Quarantine orders | `InventQuarantineOrder` | Named |
| Orders | Quality orders | quality order tables | Named |
| Orders | Nonconformances | nonconformance tables | Named |
| Orders | Inventory blocking | blocking tables | Named |
| Setup | Inventory and warehouse parameters | `InventParameters` | Counted. 72 named fields. `12` |
| Setup | Item model groups | `InventModelGroup` | Counted. 19 fields. `12` |
| Setup | Sites | `InventSite` | Fields listed |
| Setup | Warehouses | `InventLocation` | Fields listed |
| Setup | Locations | `WMSLocation` | Named |
| Setup | Journal names | `InventJournalName` | Named |
| Setup | Batch number groups | `InventNumGroup` | Named |
| Setup | Serial number groups | `InventNumGroup` | Named |

R3 **Warehouse management** (a separate area): waves, loads, work, license plates, location directives, work templates, mobile device menus. Not counted. Do not invent those fields.

## General ledger

Field file: `07-general-ledger.md`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Journals | General journal | `LedgerJournalTable` / `LedgerJournalTrans` | Fields listed |
| Common | Main accounts | `MainAccount` | Fields listed |
| Setup | Account structures | `DimensionHierarchy` | Fields listed |
| Setup | Financial dimensions | `DimensionAttribute` / `DimensionAttributeValue` | Fields listed |
| Setup | Ledger calendar | `LedgerFiscalCalendar` / period | Fields listed |
| Periodic | Allocations | `LedgerAllocationRule` | Fields listed |
| Periodic | Consolidation | elimination company journals | Fields listed |
| Budgeting | Budget register | `BudgetTransactionHeader` / line | Fields listed |
| Setup | Sales tax codes | `TaxTable` | Fields listed |
| Setup | Sales tax groups | `TaxGroupHeading` / `TaxGroupData` | Fields listed |
| Setup | Item sales tax groups | `TaxItemGroupHeading` | Fields listed |
| Journals | Sales tax transactions | `TaxTrans` | Fields listed |
| Setup | Journal names | `LedgerJournalName` | Named |
| Setup | Currencies and exchange rates | `Currency`, `ExchangeRate` | Named |
| Setup | Fiscal year close parameters | year close | Named |

## Cash and bank management

Field file: `08-cash-bank-assets.md`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Bank accounts | `BankAccountTable` | Fields listed |
| Common | Bank transactions | `BankAccountTrans` | Fields listed |
| Journals | Deposit slip | customer payments into the bank | Fields listed |
| Periodic | Bank reconciliation | statement lines matched to bank transactions | Fields listed |
| Setup | Bank groups | `BankGroup` | Named |
| Setup | Transaction types | bank transaction type | Named |

## Fixed assets

Field file: `08-cash-bank-assets.md`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Fixed assets | `AssetTable` | Fields listed |
| Common | Asset books | `AssetBook` | Fields listed |
| Journals | Acquisition, depreciation, disposal | fixed-asset journal lines | Fields listed |
| Setup | Asset groups | `AssetGroup` | Named. Holds the ledger accounts |
| Setup | Depreciation profiles | `AssetDepreciationProfile` | Named |
| Setup | Books | `AssetBookTable` | Named |

## Production control

Field file: `09-production-project-service-retail-hr.md`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | All production orders | `ProdTable` | Fields listed |
| Common | BOM lines on the order | `ProdBOM` | Fields listed |
| Common | Route on the order | `ProdRoute` | Fields listed |
| Common | Bills of materials | `BOMTable` / `BOM` | Fields listed |
| Common | Routes | `RouteTable` / `Route` | Fields listed |
| Common | Resources | `WrkCtrTable` | Named |
| Setup | Production parameters | `ProdParameters` | Named |
| Setup | Cost groups and costing versions | `InventItemPrice` | Named |
| Journals | Picking list, report as finished | production journals | Named |

## Master planning

Field file: `09`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Planned orders | `ReqPO` / `ReqTrans` | Fields listed |
| Setup | Coverage groups | `ReqGroup` | Named |
| Setup | Master plans | `ReqPlan` | Named |

## Project management and accounting

Field file: `09`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | All projects | `ProjTable` | Fields listed |
| Journals | Hour, fee, item, expense | `ProjJournalTrans` | Fields listed |
| Common | Invoice proposals | `ProjProposalJour` | Fields listed |
| Setup | Project groups | `ProjGroup` | Named. Holds WIP and revenue accounts |
| Setup | Categories | project category | Named |
| Periodic | Revenue recognition | estimate | Fields listed |

## Service management

Field file: `09`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Service orders | `SMAServiceOrderTable` | Fields listed |
| Common | Service agreements | `SMAAgreementTable` | Fields listed |
| Setup | Service parameters | service parameters | Named |

## Retail

Field file: `09`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Stores | `RetailStoreTable` | Fields listed |
| Common | Terminals | `RetailTerminalTable` | Fields listed |
| Journals | Statements | `RetailStatementTable` | Fields listed |
| Setup | Tender types | retail tender | Named |

## Human resources, payroll, expense

Field file: `09`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Workers | `HcmWorker` | Fields listed |
| Common | Positions | `HcmPosition` | Fields listed |
| Common | Employment | `HcmEmployment` | Fields listed |
| Payroll | Pay statement | earning, deduction, tax, net lines | Fields listed |
| Expense | Expense reports | `TrvExpTable` / `TrvExpTrans` | Fields listed |
| Setup | Departments as operating units | `OMOperatingUnit` | Fields listed in `10` |

## Organization administration

Field file: `10-organization-and-system.md`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Legal entities | `CompanyInfo` | Fields listed |
| Common | Operating units | `OMOperatingUnit` | Fields listed |
| Common | Organization hierarchies | `OMHierarchyType` | Fields listed |
| Common | Number sequences | `NumberSequenceTable` | Fields listed |
| Setup | Workflows | `WorkflowTable` | Fields listed |
| Setup | Document types | `DocuType` | Fields listed |

## System administration

Field file: `10`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Users | `UserInfo` | Fields listed |
| Setup | Security roles | `SecurityRole` | Fields listed |
| Setup | Duties | `SecurityDuty` | Fields listed |
| Setup | Privileges | `SecurityPrivilege` | Fields listed |
| Setup | Assign users to roles | `SecurityUserRole` | Fields listed |
| Setup | Data security policies | `SecurityPolicy` | Named |
| Setup | Batch jobs | `BatchJob` | Fields listed |
| Setup | Inbound ports | AIF port | Named |
| Setup | Database log | `DatabaseLog` | Named |
| Setup | Email templates | `SysEmailTable` | Named |

## Compliance and internal controls

Field file: `10`.

| Section | Form | Table | Status |
| --- | --- | --- | --- |
| Common | Audit policies | `SysPolicy` | Fields listed |
| Common | Audit cases | case linked by document id | Fields listed |

## How the agent builds from this register

1. Read `13-architecture-mapping.md` before any form.
2. If status is **Counted**, use that count and no extra controls.
3. If status is **Fields listed**, use the module file’s field table and its ECOM AE column.
4. If status is **Named**, stop. Do not add columns for that form until it is counted the way tracking dimension groups were counted.
5. Post only through `ErpGlPostingService.PostJournalAsync`.
6. Extend the existing `epc_erp_*` table. Do not create a parallel AX table.
