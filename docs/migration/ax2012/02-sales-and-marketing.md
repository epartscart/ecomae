# Sales and marketing

Menu path in `Ax32.exe`: **Sales and marketing**.

List pages open first. The details form has a header view and a line view. Posting from the action pane creates the next document. It does not update the order status by itself without writing that next document.

## Menu sections

| Section | List page | Details form | Posts |
| --- | --- | --- | --- |
| Sales quotations | All sales quotations | `SalesQuotationTable` | Confirmation creates `SalesTable` |
| Sales orders | All sales orders | `SalesTable` | Confirmation, packing slip, invoice |
| Return orders | All return orders | `SalesTable` with `SalesType = ReturnItem` | Packing slip (receipt) and credit note |
| Customers | All customers | `CustTable` | Master, not a posting |
| Sales agreements | Sales agreements | `AgreementHeader` (sales) | Release order creates `SalesTable` |
| Prospects | Prospects | `smmBusRelTable` | Convert creates `CustTable` |

Common list setup forms, not documents: `SalesQuotationType`, `SalesOrigin`, `SalesPool`, `CommissionSalesGroup`, `FormletterRemarks`.

## Sales quotation details — `SalesQuotationTable` / `SalesQuotationLine`

Header FastTabs: General, Delivery, Price/discount, Financial dimensions.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| General | `QuotationId` | Number sequence | GAP. No quotation header table. Do not store a quote only as a sales order |
| General | `QuotationStatus` | Sent, Confirmed, Lost, Cancelled | GAP |
| General | `CustAccount` | `CustTable.AccountNum` | Will become `epc_erp_sales_orders.customer_user_id` when confirmed |
| General | `BusRelAccount` | `smmBusRelTable.BusRelAccount` | GAP |
| General | `QuotationName` | | GAP |
| General | `CustRef` / customer reference | | GAP |
| General | `ConfirmDate` | | GAP |
| General | `ReasonId` | Lost reason | GAP |
| Delivery | `DeliveryDate` | | GAP |
| Delivery | `DlvMode` | `DlvMode.Code` | GAP |
| Delivery | `DlvTerm` | `DlvTerm.Code` | GAP |
| Delivery | `InventSiteId` | `InventSite.SiteId` | GAP |
| Delivery | `InventLocationId` | `InventLocation.InventLocationId` | GAP |
| Price | `CurrencyCode` | `Currency.CurrencyCode` | GAP |
| Price | `PriceGroupId` | `PriceDiscGroup` | GAP |
| Price | `DiscPercent` | Header discount | GAP |
| Price | `TaxGroup` | `TaxGroupHeading.TaxGroup` | GAP |
| Dimensions | `DefaultDimension` | Dimension set | `epc_erp_dim_links` when the entity type is added |

Line grid `SalesQuotationLine`:

| AX field | Links to | ECOM AE |
| --- | --- | --- |
| `QuotationId` | Header | GAP |
| `LineNum` | | GAP |
| `ItemId` | `InventTable.ItemId` | `epc_erp_inv_items.sku` |
| `Name` | | Line description |
| `SalesQty` | | Quantity |
| `SalesUnit` | `UnitOfMeasure.Symbol` | GAP (`epc_erp_inv_items.unit` is the item default, not the line unit) |
| `SalesPrice` | | Unit price |
| `LineDisc`, `LinePercent` | | GAP |
| `LineAmount` | | Extended amount |
| `InventDimId` | `InventDim` | See `00-build-rules.md` |
| `InventTransId` | `InventTrans` (quotation issue) | GAP |
| `TaxItemGroup` | `TaxItemGroupHeading` | GAP |

Confirm quotation writes `SalesTable.QuotationId = SalesQuotationTable.QuotationId` and copies lines. The quotation stays. It is not deleted.

## Sales order details — `SalesTable` / `SalesLine`

This is the form the builder should match. Header view FastTabs: **General, Setup, Delivery, Invoice, Price and discount, Foreign trade, Financial dimensions**.

### Header

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| General | `SalesId` | Number sequence | `epc_erp_sales_orders.so_no` |
| General | `SalesType` | Journal, Sales, ReturnItem, Subscription | GAP. Returns are the same table with this type, not a new table |
| General | `SalesStatus` | None, Backorder, Delivered, Invoiced, Canceled | `status`: draft, confirmed, invoiced, cancelled. Delivered and partial are GAP |
| General | `CustAccount` | `CustTable.AccountNum` | `customer_user_id` |
| General | `SalesName` | Delivery name, copied from customer | `title` |
| General | `InvoiceAccount` | `CustTable.AccountNum` | GAP |
| General | `CustomerRef` | | GAP |
| General | `PurchOrderFormNum` | Customer's PO number | GAP |
| General | `ContactPersonId` | `ContactPerson.ContactPersonId` | `contact_id` |
| General | `WorkerSalesTaker` | `HcmWorker.RecId` | GAP |
| General | `WorkerSalesResponsible` | `HcmWorker.RecId` | GAP |
| General | `SalesPoolId` | `SalesPool.SalesPoolId` | GAP |
| General | `SalesOriginId` | `SalesOrigin.OriginId` | GAP |
| General | `LanguageId` | `LanguageTable.LanguageId` | GAP |
| General | `QuotationId` | `SalesQuotationTable.QuotationId` | GAP |
| General | `InterCompanyOrder` | Paired `PurchTable.PurchId` | GAP. Do not invent a second order |
| Setup | `SalesGroup` | `CommissionSalesGroup` | GAP |
| Setup | `PostingProfile` | From `CustGroup` | GAP |
| Setup | `SettleVoucher` | Settlement on invoice | GAP |
| Delivery | `DeliveryDate` | Header requested date | GAP |
| Delivery | `ReceiptDateRequested` | | GAP |
| Delivery | `ShippingDateRequested` | | GAP |
| Delivery | `DlvMode` | `DlvMode.Code` | GAP |
| Delivery | `DlvTerm` | `DlvTerm.Code` | GAP |
| Delivery | `DeliveryName`, address fields | Logistics postal address | GAP on the order. Do not copy the customer address onto a new customer row |
| Delivery | `InventSiteId` | `InventSite.SiteId` | GAP |
| Delivery | `InventLocationId` | `InventLocation.InventLocationId` | GAP |
| Invoice | `CurrencyCode` | `Currency.CurrencyCode` | GAP. Amounts today are a single currency |
| Invoice | `Payment` | `PaymTerm.PaymTermId` | GAP. Terms master is `epc_erp_pm_pay_terms` |
| Invoice | `PaymMode` | `CustPaymModeTable.PaymMode` | GAP. Methods master is `epc_erp_pm_pay_methods` |
| Invoice | `TaxGroup` | `TaxGroupHeading.TaxGroup` | VAT is header `vat_amount` only |
| Invoice | `VATNum` | | GAP |
| Invoice | `CashDisc` | `CashDisc.CashDiscCode` | GAP |
| Invoice | `FixedDueDate` | | GAP |
| Price | `PriceGroupId` | `PriceDiscGroup.GroupId` | GAP |
| Price | `LineDisc` | Line discount group | GAP |
| Price | `MultiLineDisc` | Multiline discount group | GAP |
| Price | `EndDisc` | Total discount group | GAP |
| Price | `MarkupGroup` | `MarkupGroup.GroupId` | GAP |
| Foreign trade | `TransactionCode`, `Transport`, `Port`, `StatProcId` | Intrastat tables | GAP. Skip until a filing process is in scope |
| Dimensions | `DefaultDimension` | Dimension set | `epc_erp_dim_links` |
| Totals | Sum of lines | | `amount_ex_vat`, `vat_amount`, `total_amount` |
| Notes | Document attachment / notes | `DocuRef` | `notes` |

`sales_invoice_id` is the link to the posted invoice. It stays 0 until the invoice document exists. `admin_id`, `time_created`, `time_updated` are the system columns.

### Lines — `SalesLine`

Line FastTabs: **General, Delivery, Price and discount, Address, Product dimensions, Financial dimensions**.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| General | `SalesId` | `SalesTable.SalesId` | `epc_erp_sales_order_lines.sales_order_id` |
| General | `LineNum` | | `line_no` |
| General | `ItemId` | `InventTable.ItemId` | `item_code` → `epc_erp_inv_items.sku` |
| General | `Name` | | `description` |
| General | `SalesQty` | | `qty` |
| General | `SalesUnit` | Unit of measure | GAP |
| General | `SalesStatus` | Open, Delivered, Invoiced, Canceled | GAP on the line. Header `status` is the only status today |
| General | `SalesType` | Copied from header | GAP |
| Delivery | `ShippingDateRequested`, `ConfirmedDlv`, `ReceiptDateRequested` | | GAP |
| Delivery | `RemainSalesPhysical` | Not yet packing-slipped | GAP |
| Delivery | `RemainSalesFinancial` | Not yet invoiced | GAP |
| Price | `SalesPrice` | | `unit_price_ex_vat` |
| Price | `LineDisc`, `LinePercent`, `MultiLnDisc`, `MultiLnPercent` | | GAP |
| Price | `LineAmount` | Qty × price − discount, rounded | `line_ex_vat` |
| Price | `TaxGroup` | Header default, overridable | GAP |
| Price | `TaxItemGroup` | `TaxItemGroupHeading` | GAP |
| Product | `InventDimId` | `InventDim` | GAP on the line. See shared dimension list |
| Product | `InventTransId` | `InventTrans.InventTransId` | GAP. Required before stock can be tied to this line |
| Dimensions | `DefaultDimension` | Line override of header | `epc_erp_dim_links` with the line id |

Category lines (no item) use `SalesCategory` → `EcoResCategory.RecId` instead of `ItemId`. ECOM AE has no category line. A line without `item_code` is a description line only.

### Action pane and the documents it writes

| Action | Writes | Link back to the order |
| --- | --- | --- |
| Confirmation | `CustConfirmJour` / `CustConfirmTrans` | `SalesId`. Line: `InventTransId` |
| Packing slip | `CustPackingSlipJour` / `CustPackingSlipTrans` | `SalesId`, `PackingSlipId`. Updates `RemainSalesPhysical` and `InventTrans` status from OnOrder to Deducted |
| Invoice | `CustInvoiceJour` / `CustInvoiceTrans`, then `CustTrans`, then the voucher | `SalesId`, `InvoiceId`, `LedgerVoucher`. Sets `sales_invoice_id` and header status invoiced |
| Cancel | Only while nothing is posted | Status cancelled. Do not delete posted lines |

`CustConfirmJour` fields the next form needs: `ConfirmId`, `ConfirmDate`, `SalesId`, `InvoiceAccount`, `DeliveryDate`. Lines: `ItemId`, `Qty`, `InventTransId`, `InventDimId`.

`CustPackingSlipJour` fields: `PackingSlipId`, `DeliveryDate`, `SalesId`, `InvoiceAccount`. Lines: `ItemId`, `Qty`, `InventTransId`, `InventDimId`, `Remain`.

Invoice field list is in `03-accounts-receivable.md`. The sales form does not own those columns.

## Return order

Same `SalesTable` form. `SalesType = ReturnItem`. `SalesQty` is negative. Packing slip receives stock (`InventTrans` status Received). Invoice is a credit note: `CustInvoiceJour` with a negative amount, linked by `SalesId`. ECOM AE has no return-order type. Add it as `SalesType` on `epc_erp_sales_orders`, not as a new header table. Credit stock uses `epc_erp_inv_movements.movement_type = return_in`.

## Sales agreement

`AgreementHeader` (sales) and `AgreementLine`. Fields: `AgreementId`, `CustAccount`, `AgreementState` (OnHold, Effective, Closed), `DefaultAgreementLineEffectiveDate`, `DefaultAgreementLineExpirationDate`, `Currency`. Line: `ItemId`, `InventDimId`, committed quantity, price. Release creates `SalesTable` and stores the agreement id on the order. GAP. Do not create the agreement as a sales order with a long delivery date.

## Customer list from this menu

The customer details form is `CustTable`. Field list is in `03-accounts-receivable.md`. Sales and marketing and Accounts receivable open the same form.
