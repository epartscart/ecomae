# Cash and bank, and fixed assets

## Cash and bank management — menu sections

| Section | Details form | Table |
| --- | --- | --- |
| Bank accounts | Bank account | `BankAccountTable` |
| Deposit slip | Deposit slip | `CustPayment` deposit into the bank |
| Reconciliation | Bank reconciliation | `BankStmtISOReportEntry` matched to bank transactions |
| Promissory notes | Note journal | `VendPromissoryNote` / customer note |

### Bank account — `BankAccountTable`

| AX field | Links to | ECOM AE |
| --- | --- | --- |
| `AccountID` | Key | `epc_erp_cash_bank_accounts` id |
| `Name` | | Name column on that table |
| `CurrencyCode` | `Currency` | GAP if the account table has no currency |
| `LedgerDimension` | Main account for this bank | `coa_id` on `epc_erp_cash_bank_accounts` |
| `BankGroupId` | `BankGroup` | GAP |
| `RegistrationNum`, `AccountNum` (bank number) | | GAP |
| `CompanyPaymId` | | GAP |
| `Active` | | Active flag |

### Bank transaction

AX `BankAccountTrans`: `AccountId`, `TransDate`, `Voucher`, `AmountCur`, `AmountMST`, deposit or withdrawal, reconciled flag. ECOM AE `epc_erp_cash_bank_entries`:

| AX | Column |
| --- | --- |
| Bank | `account_id` |
| Date | `time` |
| Receipt, payment, transfer in, transfer out, adjustment | `entry_type` |
| Sign | `direction` (1 increases, 0 decreases) |
| Amount | `amount` |
| Customer, vendor, or internal | `counterparty_type`, `counterparty_id` |
| Sales document | `order_id` |
| Vendor invoice | `purchase_id` |
| The other side of a transfer | `transfer_pair_id` |
| Text | `reference`, `note` |
| Posted voucher | `gl_journal_id` when the column is present |

An internal transfer is two rows (`transfer_out` and `transfer_in`) with the same `transfer_pair_id`, and the GL uses system account `1090` Cash in transit. Do not post the transfer to revenue.

Reconciliation matches a statement line to `epc_erp_cash_bank_entries.id`. Store the match id. Do not change `amount` when the line is cleared.

## Fixed assets

Menu path: **Fixed assets**.

| Section | Form | Table |
| --- | --- | --- |
| Fixed assets | Fixed asset | `AssetTable` |
| Books | Asset book | `AssetBook` |
| Journals | Acquisition, depreciation, disposal | `LedgerJournalTrans` with account type Fixed assets |
| Lending | Asset lending | `AssetLending` |

### Asset — `AssetTable`

| AX field | Links to | ECOM AE |
| --- | --- | --- |
| `AssetId` | Number sequence | GAP. No fixed-asset header in the schemas read for this map |
| `Name` | | GAP |
| `AssetGroup` | `AssetGroup` (type, major type) | GAP. The group holds the default books and accounts |
| `Location`, `SerialNum`, `Make`, `Model` | | GAP |
| `WorkerResponsible` | `HcmWorker` | GAP |
| `DefaultDimension` | | `epc_erp_dim_links` once the entity type exists |

### Book — `AssetBook`

| AX field | Meaning |
| --- | --- |
| `BookId` | `AssetBookTable`. One asset can have several books |
| `DepreciationProfile` | `AssetDepreciationProfile`: method (straight line, reducing balance), interval, service life |
| `AcquisitionDate`, `AcquisitionPrice` | |
| `DepreciationStartDate` | |
| `Status` | Open, Suspended, Closed, Sold, Scrapped |
| `AssetGroup` accounts | Acquisition, depreciation, disposal sale, disposal scrap |

Depreciation proposal creates a journal. Each line: debit depreciation expense, credit accumulated depreciation, `TransDate`, asset id, book id. Disposal posts the acquisition cost out, the accumulated depreciation out, and the gain or loss. All of those lines are `epc_erp_gl_journals` / `epc_erp_gl_lines`. The asset book stores the status and the amounts. It does not store a private balance that can disagree with the GL.

ECOM AE build order: asset header, one book, acquisition journal, depreciation journal, disposal journal. A depreciation preview that does not post is not the asset.
