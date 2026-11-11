# Accounts payable

Menu path: **Accounts payable**.

## Menu sections

| Section | List page | Details form |
| --- | --- | --- |
| Vendors | All vendors | `VendTable` |
| Invoices | Invoice journal, pending invoices | `VendInvoiceJour`, `VendInvoiceInfoTable` |
| Payments | Payment journal | `LedgerJournalTable` vendor payment |
| Promissory notes / checks | Checks | Check layout. ECOM AE `epc_erp_pm_cheques` |
| Settlements | Settle open transactions | `VendSettlement` |

## Vendor details — `VendTable`

FastTabs: **General, Address, Invoice and delivery, Payment, Financial dimensions, Purchase demographics**.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| General | `AccountNum` | Key on every PO | `epc_erp_purchase_orders.supplier_id` |
| General | `Party` | `DirPartyTable.RecId` | Supplier name is the supplier master, not a second party table |
| General | `VendGroup` | `VendGroup.VendGroup` | `epc_erp_pm_vendor_groups.id`. GAP on the vendor row |
| General | `Currency` | | GAP |
| General | `Blocked` | No, Invoice, All | GAP |
| General | `VATNum` | | GAP |
| General | `TaxGroup` | `TaxGroupHeading` | GAP |
| General | `InventSiteId`, `InventLocationId` | | GAP |
| Invoice | `InvoiceAccount` | Another vendor | GAP |
| Invoice | `PaymTermId` | `PaymTerm` | `epc_erp_pm_pay_terms` via the group `terms_id` |
| Invoice | `PaymMode` | `VendPaymModeTable` | `epc_erp_pm_pay_methods` |
| Invoice | `CashDisc` | | GAP |
| Invoice | `PostingProfile` | `VendLedger` | GAP. Picks the AP control account |
| Payment | `BankAccount` | `VendBankAccount.AccountID` | GAP |
| Dimensions | `DefaultDimension` | | `epc_erp_dim_links` |

`VendGroup` holds the group id and the posting profile. Group master columns today: `code`, `name`, `terms_id`, `note`, `active`.

## Vendor invoice — pending then posted

Pending invoice (editable): `VendInvoiceInfoTable` / `VendInvoiceInfoLine`. Posted invoice: `VendInvoiceJour` / `VendInvoiceTrans`.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| Pending | `Num` | Internal invoice id | `epc_erp_purchases.id` |
| Pending | `InvoiceAccount` | `VendTable.AccountNum` | `supplier_id` |
| Pending | `InvoiceDate`, `DueDate` | | `purchase_date` is the invoice date (unix). Due date is GAP |
| Pending | `InvoiceNum` | Vendor's invoice number | `invoice_number` |
| Pending | `CurrencyCode` | | GAP |
| Pending | `PurchId` | `PurchTable.PurchId` | `po_id` |
| Pending | `Description` | | `note` |
| Line | `ItemId`, `ReceiveNow`, `PurchPrice`, `LineAmount` | PO line and product receipt | `epc_erp_purchase_inv_lines`: `purchase_id`, `item_id`, `warehouse_id`, `qty`, `unit_cost`, `batch_no`, `expiry_date`, `movement_id` |
| Line | `InventTransId` | `InventTrans` from the receipt | `movement_id` → `epc_erp_inv_movements.id` |
| Line | `TaxGroup`, `TaxItemGroup` | | Header `vat_applicable`, `vat_rate`, `vat_amount`, `uae_vat_treatment`, `uae_tax_legislation_ref` |
| Match | Quantity and price vs receipt and PO | `VendInvoiceMatch` | GAP. Do not post when received qty and invoice qty disagree unless the user accepts the difference |
| Posted | `LedgerVoucher` | GL | `voucher_no` and `gl_journal_id` → `epc_erp_gl_journals.id` |
| Posted | `InvoiceAmount`, `SumTax` | | `total_amount`, `vat_amount`, `amount_ex_vat` |
| Posted | `OrderAccount` | | `supplier_id` |
| Source | Shop order | | `order_id` |
| Source | Warehouse | | `storage_id` |

Posting also writes `VendTrans` and a supplier subledger row.

`VendTrans` fields: `AccountNum`, `TransDate`, `Voucher`, `Invoice`, `AmountCur`, `SettleAmountCur`, `DueDate`, `Closed`, `TransType`. ECOM AE subledger is `epc_erp_supplier_accounting`:

| AX | ECOM AE column |
| --- | --- |
| Vendor | `supplier_id` |
| Date | `time` |
| Debit/credit | `is_credit` |
| Amount | `amount` |
| Invoice | `purchase_id` |
| Shop order | `order_id` |
| Payment | `cash_entry_id` |
| Text | `reference`, `note`, `entry_kind` |

Open amount is not a column. It is the unpaid part of the invoice row. Settlement must not create a second supplier balance table.

## Matching

Three-way match compares `PurchLine.PurchQty` and `PurchPrice`, `VendPackingSlipTrans.Qty`, and `VendInvoiceInfoLine` quantity and price. Tolerances live on Accounts payable parameters. ECOM AE has the three documents (`epc_erp_po_lines`, `epc_erp_po_receipts`, `epc_erp_purchase_inv_lines`) and no tolerance fields. Add tolerance on the company parameters, not on each line, unless the line must override it.

## Vendor payment journal

Same journal form as the customer payment, with `AccountType = Vend`.

| AX field | ECOM AE |
| --- | --- |
| Bank offset | `epc_erp_cash_bank_entries.account_id` |
| Vendor | `counterparty_type = supplier`, `counterparty_id` |
| Amount | `amount`, `direction = 0` for a payment |
| Invoice settled | `purchase_id` |
| Reference | `reference` |
| Settlement | `epc_erp_settlement_allocations` and `VendSettlement` pair of `VendTrans` |

`epc_erp_pm_cheques` is the check: `bank_account_id`, `cheque_no`, `pay_to`, `amount`, `cheque_date`, `memo`, `status`. Link the check to the payment journal line. The check number is not the voucher number.

## Setup that changes posting

| Form | Effect |
| --- | --- |
| Vendor posting profile | AP summary account, discount account |
| Invoice matching policy | Two-way or three-way, price tolerance, quantity tolerance |
| Vendor hold | `Blocked` stops invoice or all |
| Charges (`MarkupTrans`) | Extra amount on the invoice, its own ledger account. GAP |
