# INDUS LIVE Jewellery field and workflow mapping

This record preserves the two supplied INDUS LIVE studies:

- `INDUS_LIVE_replacement_system_study.docx`
- `INDUS_LIVE_metal_diamond_master_linkage.docx`

The studies are UI-derived replacement-system references, not a verified INDUS
database dictionary. The ASP.NET migration must use the PHP/schema references
for confirmed legacy behavior and use this document to retain the observed
INDUS field vocabulary, workflow inventory, proposed target mappings, and
validation backlog.

The additional `INDUS_Jewellery_ERP_Devin_AI_Build_Blueprint.docx` is also
included in this evidence record. It defines the target chain, engineering
principles, and G1–G10 acceptance gates, but it does not replace PHP as the
legacy behavior or schema authority.

## Evidence rules

- **Observed**: a menu, label, field, tab, or transaction value was visible.
- **Inferred**: a relationship is suggested by repeated codes or workflow use.
- **Proposed**: a replacement-system design recommendation, not a legacy column.
- **Open**: schema, formula, lifecycle, or permission behavior still requires
  read-only evidence or an approved transaction trace.

Do not represent proposed target names as existing PHP or INDUS table names.

## Confirmed operating context

- INDUS LIVE is a Windows desktop application using VB6 runtime components,
  ADO/OLE DB SQL Server connectivity, and Crystal Reports.
- The client is branch and financial-year aware; the observed title showed HO,
  FY 2026, and `INDUS2008R2`.
- No INDUS database or source code was available in the studies. No records
  were changed.
- The replacement must preserve tenant/company, branch, location, financial
  year, user/role, audit, reversal, and country-driven compliance context.

## Master-data menu and target mapping

| Observed menu/field | Target concept | Migration use |
| --- | --- | --- |
| Metal | `MetalType` / material catalogue | Master referenced by stock, purchase, manufacture, transfer, sale |
| Rate Type | `MetalRateType` | Rate vocabulary and historical rate selection |
| Karat / Purity | `Karat` | Purity validation and dated karat rates |
| Division | `MetalDivision` | Preserve `G`, `S`, `T`, `D`, `P` where applicable |
| Prefix | `StockCodePrefix` / numbering rule | Code generation and uniqueness scope |
| Design | `MetalDesign` / `JewelleryDesign` | Classification used by items and reports |
| Cost And Price Types | `CostPriceType` / price-tier rule | Cost and selling-price selection |
| Diamond Design | `DiamondDesign` | Stone and jewellery classification |
| Jewellery | `JewelryItem` | Sellable design/unit grain remains an acceptance question |
| Loose Stone / Pearl / Watch / Color Stone | `StoneItem` with typed attributes | Keep specialised domains without flattening all fields |
| Manufactured Items | `ManufacturedJewelry` | Parent item plus component and valuation lifecycle |
| Diamond Master Amendment | amendment event | Approval/audit trail for master changes |
| Currency / Location / Cost Centre | shared dimensions | Required on transaction and accounting projections |
| Price List | `PriceTier` / price list | Effective-dated price selection |
| VAT Master | country VAT rules | Tenant-country compliance, not universal constants |
| Daily Rate / Daily Karat Rate | `DailyRate` | Effective dating and transaction snapshots |

## Manufacture, metal and diamond linkage

The focused linkage study identifies the following observed fields and target
contracts. Target names are ASP.NET domain concepts, not claims about legacy
columns:

| INDUS area | Observed fields | Target mapping |
| --- | --- | --- |
| Manufacture header | Branch, voucher type/date/no, item code, description, currency, cost centre, type, brand, design, category, sub-category, country, vendor ref, grade | `JewelryItem` plus organization, product classification, source-party and financial-context links |
| Manufacture metals | Stock Code, Purity, Gross Wt, Rate Type, Metal Rate, Making Rate, Amount FC/LC | `ItemComponent` with material-item link, purity/rate snapshots, measured weight, and FC/LC amount |
| Manufacture stones | Stones tab, stone total quantity/amount | `ItemStoneComponent`; detailed stone attributes remain open |
| Manufacture other | Others / Info tab, other total | Other-charge component lines |
| Manufacture valuation | Cost FC/LC, Price 1–5 FC/LC, metal/stone/other totals | `PriceTier` and immutable cost/value snapshots |
| Manufacture identity | Item Code, Tag Details | `JewelryItem` plus `InventoryUnit` / barcode identity; unit-vs-design grain must be confirmed |
| Metal purchase | Code, PCS, gross weight, stone weight, purity, pure weight, metal rate, making rate, metal/stone amount | `Purchase`/`PurchaseLine` with measured quantities and valuation snapshots |
| Branch transfer out | Metal Stock Code, PCS, gross/st./net/pure weight, purity, rate type/rate | `StockMovement` with source/destination branch/location and immutable rate/weight snapshots |
| POS sale | Karat, metal rate, stock code, description, PCS, gross weight, total with VAT | `TradeSale`/`PosSale` and `SaleLine` linked to tagged inventory |
| Metal stock balance | Karat, type, stock code, category, brand | As-of stock projection grouped by branch/division/location and stock identity |

## Proposed replacement entities

These entities are the durable target model suggested by the studies:

- `Company`, `Branch`, `Location`, `FinancialYear`, `User`, `Role`
- `Party`, `Account`, `RetailCustomer`, identity documents, address, AML review
- `MetalType`, `Karat`, `RateType`, `Division`, `Design`, `StoneItem`,
  `JewelryItem`, `ItemComponent`, `InventoryUnit`, `PriceTier`, `DailyRate`
- `Purchase`, `PurchaseLine`, `Fixing`, `StockMovement`, `Transfer`,
  `StockVerification`, `Adjustment`
- `TradeSale`, `PosSale`, `SaleLine`, `Tender`, `SalesOrder`, `Return`
- `Voucher`, `LedgerLine`, `Allocation`, `TaxEntry`, `ExchangeRate`
- `LegacyKeyMap` for traceable migration from PHP/INDUS identifiers

Every transaction should retain source document/type/id/line, effective rates,
transaction/posting dates, financial year, branch/location, reversal linkage,
and an immutable audit record.

## Full INDUS workflow inventory

The replacement plan must account for these observed menu capabilities:

- Masters: general, location, currency, account, debtor/creditor, staff
  advances, cost centre, metal, diamond, consumable, price list, additional
  amount, POS receipt, KYC, documents, VAT, salesperson, service, daily rate,
  daily karat rate.
- Procurement: metal/diamond purchase, fixing, import, returns, consignment,
  and related auto-generation variants.
- Sales/POS: metal/diamond/consignment sales and returns, export/exhibition/
  proforma sale, POS sale/receipt/customer, orders, advances, old diamond
  purchase, tourist VAT refund.
- Inventory/manufacturing: manufacture, branch/location transfers, stock
  adjustment, stock verification, tag printing, barcode generation,
  reconciliation, and stock enquiries.
- Reports: metal/diamond ledgers, movement, transaction summaries, stock
  balance, sales/transfer/age/analysis reports, manufacture issues, and
  verification.
- Finance: receipts, payments, petty cash, journal vouchers, opening balance,
  credit/debit notes, statements, audit trail, trial balance, VAT/MIS reports.
- Payroll and administration: employee/pay components/attendance/loans/salary,
  settings, allocation, back-dated statements, backup/restore, year-end,
  unauthorized documents, recalculation, and repair utilities.

The PHP Jewellery tranche currently prioritizes the master, purchase, stock,
sales, repair, finance, and compliance subsets. The remaining INDUS workflows
stay explicit acceptance gates rather than being marked complete from menu
presence alone.

## Confirmed PHP master-form parity tranche

The ASP.NET Jewellery master workspace now presents the PHP-evidenced field
surface for diamond, pearl, and color-stone masters, including classification,
vendor, certificate, measurement, charge, pricing, and compliance inputs where
those fields are present in the PHP forms. The list headers also follow the
corresponding PHP master grids. This is presentation and request-surface parity;
it does not claim that fields omitted by the PHP save functions are persisted,
nor does it close the INDUS schema, posting, formula, or transaction-lineage
validation gates.

## Required validation backlog

Before declaring a mapping complete, obtain read-only evidence for:

1. Actual tables, columns, keys, indexes, views, procedures, triggers, and
   report definitions.
2. One approved metal record and one loose-stone/diamond record.
3. One manufactured jewellery item with metal and stone components.
4. Purchase → manufacture → transfer → POS sale with stock and ledger effects.
5. Daily-rate, karat-rate, price-type, and price-list selection with historic
   snapshots.
6. Metal and diamond stock reports for a chosen date, including in-transit,
   repair, ownership, zero-quantity, and branch scope.
7. Purity/pure-weight formulas, rounding, cost layers, journal postings,
   tag/barcode uniqueness, and reversal behavior.
8. Roles, approval states, segregation of duties, audit, backup/recovery, tax,
   AML/KYC, barcode/printer, import/export, and country-specific compliance.

Until these items are evidenced, use the PHP implementation and schema as the
behavioral authority, keep ASP.NET routes guarded/shadowed, and retain PHP as
fallback.

## Additional blueprint acceptance gates

The blueprint adds the following explicit readiness gates to this backlog:

| Gate | Evidence required | Current status |
| --- | --- | --- |
| G1 Domain | Confirm identities, code scopes, precision, and relationships | Open |
| G2 Calculations | Approve purity, pure-weight, rate, making, stone, tax, and rounding rules | Open |
| G3 Inventory | Reconstruct stock by branch/location/tag as of a date | Partial; PHP balance projection added |
| G4 Finance | Produce balanced, reproducible operational postings | Open |
| G5 Reconciliation | Reconcile pieces, weights, value, AR/AP, and trial balance | Open |
| G6 Security | Test RBAC, maker-checker, audit, exports, and overrides | Partial; route/company gates covered |
| G7 Performance | Meet POS, stock enquiry, and report SLAs | Open |
| G8 Recovery | Test restore, retry/idempotency, failed posting, and DR | Open |
| G9 UAT | Sign purchase, manufacture, transfer, verification, POS, return, melting, and finance scenarios | Open |
| G10 Cutover | Complete rehearsal, rollback, opening balances, and freeze sign-off | Open |

The ASP.NET stock-balance slice intentionally implements only the observed PHP
projection: positive `epc_jewel_metal_stock` rows grouped by company, metal,
and karat, with pieces, grams, and value totals. It does not claim a new
immutable ledger or invent an as-of-date formula.

## Screenshot-driven transaction tranche

The supplied screenshot set is now treated as an acceptance baseline for the
transaction screens. The first functional tranche extends the ASP.NET voucher
save path beyond a header-only write:

| Screenshot family | PHP evidence | ASP.NET status |
| --- | --- | --- |
| Retail / metal sales / sales return | `epc_jewel_voucher` + `epc_jewel_voucher_lines`; `jw_*_save` aliases | Header and repeated multi-line browser entry now bind through the guarded voucher endpoint; PHP-owned fallback remains |
| Metal / diamond purchase and purchase window | Voucher registry plus shared voucher lines | Shared line contract persists stock code, division, description, pieces, weights, purity, metal/making/stone/discount amounts |
| POS advance / petty cash / journal voucher | `ADV`/`PCV`/`JVL` registry and voucher header | Voucher header path remains guarded; detailed receipt/accounting posting stays open |
| Purchase / sales fixing | `epc_jewel_fixing` and fixing aliases | Existing fixing endpoint remains separate; purchase/sales voucher forms expose the shared line contract |

Line valuation follows the PHP-observed calculation shape when derived values
are not supplied: pure weight is gross weight multiplied by purity, making
amount is gross weight multiplied by making rate, metal amount is pure weight
multiplied by metal rate, and total amount includes metal, making, stone, and
discount values. This is **Observed/Inferred PHP parity**, not a claim that
all INDUS posting and inventory triggers have been independently verified.

The ASP.NET retail workspace now provides a tenant-scoped read-only lookup for
tag number, barcode, and stock code across the observed `epc_jw_tags` and
`epc_jewel_barcode` registries. It intentionally returns descriptive identity
and weight/price fields only; it does not claim stock reservation, deduction,
sale allocation, or tag-level movement reconstruction.

The guarded tag-sale endpoint now consumes only an `in_stock` tag and applies
the selected company scope when supplied, so an already sold tag or a tag from
another company cannot be consumed through this path. This is a narrow
availability/transition guard, not a claim of complete retail stock,
tender, invoice, or movement-posting parity.

The guarded fix/unfix settlement path now allows only an open `unfix` purchase,
requires the selected company scope when supplied, and records one settlement
transition plus its settlement row. This closes repeat-settlement and
cross-company consumption on the observed path; full finance posting and
reconciliation remain unverified.

The fit-out acceptance surface now includes a project-scoped estimate/BOQ
revision comparison readback with cost, selling, line-count, status, and
adjacent-revision delta evidence. It remains a read-only evidence surface;
approval sign-off, live tenant-database corroboration, and full scenario
acceptance remain open.
Project-scoped fit-out approval evidence now combines the pending approval
queue with decision audit history; this remains read-only machine evidence and
does not replace human approval sign-off.
The fit-out workspace also exposes estimate/BOQ detail readback with component
rates and derived totals for machine verification; it remains read-only
evidence and does not claim production acceptance.

The remaining screenshot acceptance gates are explicit: stock availability and deduction, receipt/tender
allocation, VAT/TRN rules, fixing settlement effects, repair item-level
transfers, report filters, and live MariaDB corroboration. These stay PHP
authoritative until schema and transaction traces are verified.

## Shared ERP versus industry-specific access boundary

The ASP.NET surface treats finance, purchasing, sales, inventory, tax,
approvals, and other general ERP workspaces as shared capabilities. Jewellery
and fit-out/construction workspaces are separate industry packs:

| Surface | Required industry context | Enforcement |
| --- | --- | --- |
| Jewellery CP/ERP routes | `jewellery` / `jewelry` host or industry pack | Navigation filtering plus request middleware |
| Fit-out project accounting and `/erp/fitout/*` routes | `fitout`, `fit_out`, `construction`, or `construction_contracting` host or industry pack | Request middleware |
| Shared ERP routes | No industry-specific pack | Remain available subject to normal tenant, RBAC, and module-pack rules |

Direct URL and form requests are denied before Blazor route rendering when the
tenant context does not satisfy the required industry pack. Super-ERP
diagnostic surfaces remain governed by their existing privileged host/session
gates; this policy does not broaden ordinary tenant access.
