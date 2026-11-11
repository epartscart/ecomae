# General ledger

Menu path: **General ledger**.

## Menu sections

| Section | List page | Details form |
| --- | --- | --- |
| Journals | General journals, periodic journals | `LedgerJournalTable` |
| Chart of accounts | Main accounts | `MainAccount` |
| Account structures | Account structures | `DimensionHierarchy` |
| Financial dimensions | Financial dimensions | `DimensionAttribute` |
| Trial balance and statements | Reports | Read posted entries. Do not store the statement |
| Period close | Ledger calendar | `LedgerFiscalCalendar` |
| Allocations | Allocation rules | `LedgerAllocationRule` |
| Consolidations | Consolidate online | Elimination company |
| Budgets | Budget register | `BudgetTransactionHeader` |

## Main account — `MainAccount`

| AX field | Meaning | ECOM AE |
| --- | --- | --- |
| `MainAccountId` | Account number | Chart row. Seeder is `ErpGlChartOfAccountsSeeder`. `epc_erp_gl_lines.coa_id` points here |
| `Name` | | Account name |
| `Type` | Profit and loss, Balance sheet, Total, Reporting | `account_type` on the chart table |
| `CurrencyCode` | Default currency | GAP if the chart has no currency |
| `PostingType` | None, or a system type (tax, customer, vendor, bank) | GAP. System accounts such as cash in transit `1090` are chosen by the posting service, not typed on the journal |
| `Closed` | Block posting | `active` |
| `DefaultDimension` | | `epc_erp_dim_links` |

Account structures (`DimensionHierarchy`) say which dimensions are required for a main account. A journal line that fails the structure does not post. ECOM AE does not enforce a structure yet. The audit in `D365_FO_STRUCTURE_PARITY_AUDIT.md` is the gate.

## Financial dimensions

| Form | AX field | ECOM AE |
| --- | --- | --- |
| Dimension | `DimensionAttribute.Name`, backing entity | `epc_erp_pm_dimensions.code`, `name`, `dim_type`, `active` |
| Value | `DimensionAttributeValue` | `epc_erp_pm_dimension_values.dimension_id`, `code`, `name`, `active` |
| On a document | `DefaultDimension` | `epc_erp_dim_links` |
| On a posted line | `LedgerDimension` (main account + values) | `epc_erp_gl_lines.coa_id` only. Combination is GAP |

Legal entity and business unit are not dimensions in the loose sense. They are `epc_erp_pm_legal_entities` (`code`, `name`, `country_code`, `currency_code`, `trn`) and `epc_erp_pm_business_units` (`code`, `name`, `legal_entity_id`, `parent_id`, `manager`). Class units are `epc_erp_pm_class_units`.

## General journal — `LedgerJournalTable` / `LedgerJournalTrans`

Header FastTabs: **General**. Line grid plus **General** and **Financial dimensions** on the line.

| Section | AX field | Links to | ECOM AE |
| --- | --- | --- | --- |
| Header | `JournalNum` | Number sequence | `epc_erp_gl_journals.journal_no` |
| Header | `JournalName` | `LedgerJournalName` (Daily, Periodic, Approval) | GAP. `source_type` is the kind: manual, sales, purchase, payment, cash, opening, adjustment, sales_invoice |
| Header | `Description` | | `description` |
| Header | `Posted` | | A row in this table is the posted journal. Unposted capture is GAP |
| Header | `Voucher` series | | Same as `journal_no` unless a line voucher is added |
| Header | Company | `DataAreaId` | Company column added by the multi-entity PHP path. Keep one company per journal |
| Header | Source | Subledger document | `source_type`, `source_id` |
| Line | `LineNum` | | GAP. Order is insert order |
| Line | `AccountType` | Ledger, Cust, Vend, Bank, Project, Fixed assets | Ledger lines use `coa_id`. Cust and Vend on this journal are GAP. Bank is a cash entry, not a GL account type column |
| Line | `LedgerDimension` | Main account + dimensions | `coa_id` |
| Line | `OffsetLedgerDimension` | Balancing account | A second line. AX offset is not a column on `epc_erp_gl_lines` |
| Line | `AmountCur` debit | | `debit` |
| Line | `AmountCur` credit | | `credit` |
| Line | `CurrencyCode`, `ExchRate` | | GAP. Amounts are accounting currency |
| Line | `Txt` | | `line_note` |
| Line | `TransDate` | | Header `journal_date` only |
| Line | `Invoice`, `PaymentReference` | | GAP |
| Line | `TaxGroup`, `TaxItemGroup` | `TaxTrans` | GAP. Tax must be its own lines, not a percent hidden in the expense line |
| System | | | `admin_id`, `active`, `time_created` |

Posting rule already in the code: debit sum equals credit sum after rounding each line to two decimals. A line is debit or credit, not both. Reversal is a new journal with the sides swapped and `source_type = adjustment`, linked to the original `source_id`. Do not edit `debit` / `credit` on a posted line.

## Trial balance and statements

Read `epc_erp_gl_lines` joined to `epc_erp_gl_journals` and the chart. Opening balance is `source_type = opening`. The statement is not a table. Drill-down opens the journal by `journal_id`.

## Period close

`LedgerFiscalCalendarPeriod`: period start, end, status (Open, On hold, Closed). A closed period rejects new journals. ECOM AE period lock is a process gate, not a column on the journal. The journal date must be inside an open period before `PostJournalAsync`.

## Budget

`BudgetTransactionHeader` / `BudgetTransactionLine`: budget model, date, `LedgerDimension`, amount. ECOM AE is `epc_erp_pm_budgets` (`code`, `name`, `fiscal_year`, `business_unit_id`, `is_master`) and `epc_erp_pm_budget_lines` (`budget_id`, `account_code`, `account_name`, `month_no`, `amount`). Link `account_code` to the chart. A budget line does not post to `epc_erp_gl_lines`.

## Allocation and consolidation

Allocation rule: source dimension, destination dimension, percentage. The result is a journal (`source_type = adjustment`). Consolidation reads each company's journals into an elimination company. Intercompany due-to / due-from is still unproven. Do not post elimination by editing the source company journal.

## Sales tax

`TaxTable`: `TaxCode`, percent, `TaxAccount` (payable or receivable). `TaxGroupData` joins `TaxGroup` to `TaxCode`. `TaxItemGroup` joins the item side. `TaxTrans` stores the tax per voucher line: `TaxCode`, `Source`, `SourceTableId`, `SourceRecId`, base, amount, `Voucher`. ECOM AE stores VAT on the document header (`vat_amount`, `vat_rate`, `uae_vat_treatment`). Per-code `TaxTrans` is GAP. The GL lines for output VAT and input VAT still go through the posting service.
