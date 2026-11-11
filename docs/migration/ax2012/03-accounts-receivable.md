# Accounts receivable

Menu path: **Accounts receivable**. The sales order is created under Sales and marketing. This menu owns the customer, the free-text invoice, the open transaction, settlement, and collection.

## Menu sections

| Section | List page | Details form |
| --- | --- | --- |
| Customers | All customers | `CustTable` |
| Free text invoices | Free text invoices | `CustInvoiceTable` |
| Invoices | Invoice journal | `CustInvoiceJour` |
| Payments | Payment journal | `LedgerJournalTable` with journal type Customer payment |
| Collections | Collections | `CustAging` work lists, interest, collection letter |
| Setup | Parameters, posting profiles, terms | Parameter forms below |

## Customer details — `CustTable`

FastTabs: **General, Address, Invoice and delivery, Payment, Financial dimensions, Credit and collections, Sales demographics**.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| General | `AccountNum` | Number sequence. This is the key every order stores | `customer_user_id` is an int user id, not this account string. Keep the user id. Add `account_num` only if orders must show the AX account |
| General | `Party` | `DirPartyTable.RecId` (name, addresses) | Customer display name lives on the user, not on an ERP party table |
| General | `CustGroup` | `CustGroup.CustGroup` | `epc_erp_pm_customer_groups.id` is the master. GAP on the customer row |
| General | `Currency` | `Currency.CurrencyCode` | GAP |
| General | `Blocked` | No, Invoice, All | GAP |
| General | `VATNum` | | GAP |
| General | `TaxGroup` | `TaxGroupHeading.TaxGroup` | GAP |
| General | `InventSiteId`, `InventLocationId` | Default site and warehouse | GAP |
| Address | Postal address, purpose Delivery / Invoice | `LogisticsPostalAddress` | GAP on the ERP customer. Do not overwrite the storefront profile from this form |
| Invoice | `InvoiceAccount` | Another `CustTable.AccountNum` | GAP |
| Invoice | `PaymTermId` | `PaymTerm.PaymTermId` | `epc_erp_pm_pay_terms` |
| Invoice | `PaymMode` | `CustPaymModeTable.PaymMode` | `epc_erp_pm_pay_methods` |
| Invoice | `CashDisc` | `CashDisc.CashDiscCode` | GAP |
| Invoice | `PostingProfile` | `CustLedger.PostingProfile` | GAP. Profile is what picks the summary account |
| Payment | `BankAccount` | `CustBankAccount.AccountID` | GAP |
| Dimensions | `DefaultDimension` | Dimension set | `epc_erp_dim_links` |
| Credit | `CreditMax`, `CreditRating`, `MandatoryCreditLimit` | | GAP. Do not block orders until this process is accepted |

`CustGroup` fields that affect posting: `CustGroup`, `PaymTermId`, `ClearingPeriod`, and the ledger accounts on `CustLedgerAccounts` (summary, settle, discount). ECOM AE customer group columns are `code`, `name`, `terms_id`, `note`, `active` on `epc_erp_pm_customer_groups`. Ledger accounts on the group are GAP.

## Posted sales invoice — `CustInvoiceJour` / `CustInvoiceTrans`

Opened from the order action pane or from the invoice journal. Header is the posted document. It is not editable except for notes.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| Header | `InvoiceId` | Number sequence | `epc_erp_sales_orders.sales_invoice_id` points at the invoice row. Dashboard reads `epc_erp_sales_invoices`. Use that table. Do not create another invoice header |
| Header | `InvoiceDate` | | GAP until the invoice schema in PHP is read for this column. Do not guess the column name |
| Header | `SalesId` | `SalesTable.SalesId` | `epc_erp_sales_orders.id` |
| Header | `InvoiceAccount` | `CustTable.AccountNum` | GAP |
| Header | `OrderAccount` | `CustTable.AccountNum` | `customer_user_id` |
| Header | `CurrencyCode` | | GAP |
| Header | `InvoiceAmount` | Sum of lines + tax | `total_amount` on the order is the order total, not a substitute for the invoice |
| Header | `SumTax` | | `vat_amount` |
| Header | `LedgerVoucher` | `GeneralJournalEntry.SubledgerVoucher` | `epc_erp_gl_journals.journal_no` with `source_type = sales_invoice` and `source_id` = invoice id |
| Header | `DueDate` | From payment terms | GAP |
| Line | `InvoiceId` | Header | Invoice line table. `epc_einvoice_lines.document_id` is the tax-invoice line, not this subledger line |
| Line | `ItemId`, `Name`, `Qty`, `SalesPrice`, `LineAmount` | Copied from `SalesLine` | Copy from `epc_erp_sales_order_lines` |
| Line | `InventTransId` | `InventTrans` | GAP |
| Line | `TaxGroup`, `TaxItemGroup`, `TaxAmount` | `TaxTrans` | GAP per line |
| Line | `OrigSalesId` | Source order | `sales_order_id` |

Posting creates `CustTrans` (the open customer balance) and the GL journal in one transaction. If the journal fails, the invoice row must not stay posted. `CustTrans` fields:

| AX field | Links to | ECOM AE |
| --- | --- | --- |
| `AccountNum` | `CustTable.AccountNum` | Customer on the invoice |
| `TransDate` | | Journal date |
| `Voucher` | Same as `LedgerVoucher` | `epc_erp_gl_journals.journal_no` |
| `Invoice` | `CustInvoiceJour.InvoiceId` | Invoice id |
| `AmountCur` | Invoice currency amount | `total_amount` |
| `SettleAmountCur` | Amount already settled | GAP. Open amount is `AmountCur − SettleAmountCur` |
| `AmountMST` | Accounting currency | GAP. One currency today |
| `DueDate` | | GAP |
| `Closed` | Fully settled | GAP |
| `TransType` | Sales, Payment, Credit note | GAP |

## Free text invoice — `CustInvoiceTable` / `CustInvoiceLine`

No item and no `InventTransId`. FastTabs: Invoice, Invoice lines, Financial dimensions.

| AX field | Links to | ECOM AE |
| --- | --- | --- |
| `InvoiceId` | Number sequence | GAP as its own header. Do not reuse a sales order with a blank item to fake it |
| `OrderAccount` | `CustTable.AccountNum` | Customer id |
| `InvoiceDate`, `DueDate` | | GAP |
| `CurrencyCode` | | GAP |
| `TaxGroup` | | GAP |
| Line `Description`, `AmountCur`, `LedgerDimension`, `TaxItemGroup` | Main account via ledger dimension | A journal line `coa_id` after post. The revenue account is on the line, not looked up from an item group |

Posting still writes `CustInvoiceJour` (free-text flag), `CustTrans`, and the voucher.

## Payment journal

List: customer payment journals. Details: `LedgerJournalTable` / `LedgerJournalTrans`.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| Header | `JournalNum` | Number sequence | Cash side is `epc_erp_cash_bank_entries`. GL side is `epc_erp_gl_journals` |
| Header | `JournalName` | `LedgerJournalName.JournalName` with type Customer payment | GAP |
| Header | `Posted` | | `epc_erp_gl_journals` exists only after post. There is no unposted journal header yet |
| Line | `AccountType = Cust` | `CustTable` via `LedgerDimension` | `counterparty_type = customer`, `counterparty_id` |
| Line | `OffsetAccountType = Bank` | Bank main account | `epc_erp_cash_bank_entries.account_id` |
| Line | `AmountCur` | | `amount` |
| Line | `Invoice` | `CustTrans.Invoice` being settled | GAP on the cash row. `order_id` is the only document link today |
| Line | `PaymReference`, `PaymMode` | | `reference` |
| Line | `SettleVoucher` | Creates `CustSettlement` | `epc_erp_settlement_allocations` |

`CustSettlement` links two `CustTrans.RecId` values (`TransRecId` and `OffsetRecId`) and stores `SettleAmountCur`. ECOM AE `epc_erp_settlement_allocations` is the table to extend. Do not store settlement only as a note on the cash entry.

## Collection and interest

| Form | Fields that matter | ECOM AE |
| --- | --- | --- |
| Aging | As-of date, aging buckets from due date on `CustTrans` | GAP. Compute from open `CustTrans`, do not store a second balance |
| Collection letter | `CustCollectionLetterJour`: account, date, fee | GAP |
| Interest note | `CustInterestJour`: account, fee, invoice | GAP. Posts a free-text-style customer transaction |

## Setup forms that change posting

| Form | Field | Effect |
| --- | --- | --- |
| Accounts receivable parameters | `CustParameters` posting, credit, settlement | Company-wide. One row per company |
| Customer posting profile | `CustLedger.PostingProfile`, summary account | Picks the AR control account on the voucher |
| Payment terms | `PaymTerm.PaymTermId`, `NumOfDays` | `epc_erp_pm_pay_terms.net_days` |
| Cash discount | `CashDisc.CashDiscCode`, percent, days | GAP |
| Tax group | `TaxGroupHeading` / `TaxGroupData` / `TaxTable` | Percent and the tax ledger account. Header `vat_amount` is not this setup |
