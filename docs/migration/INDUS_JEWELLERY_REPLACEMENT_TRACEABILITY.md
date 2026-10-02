# INDUS Jewellery replacement traceability and gap register

This document reconciles the replacement-system blueprint supplied on
2026-10-02 with the current ECOM AE ASP.NET migration. It is a planning and
acceptance artifact, not proof that an unverified INDUS rule exists in PHP.
PHP tables, routes, formulas, permissions, and readback remain authoritative
until the corresponding gate is accepted.

## Decision boundary

The attachment requires a complete replacement architecture, but the current
program is an ERP-first PHP coexistence migration. Therefore:

- existing PHP-compatible screens and services are accepted only through the
  per-process `BUILD → TEST → ACCEPT → LOCK` gates;
- proposed normalized entities below are target architecture, not permission to
  create shadow tables over PHP-owned schemas;
- missing target architecture is recorded as a controlled gap or
  `POST-MIGRATION ENHANCEMENT` unless it is required for data integrity,
  security, accounting, or an already-scoped acceptance tranche;
- live tenants remain read-only for rehearsal and PHP fallback remains active.

## A–L first-deliverable status

| Deliverable | Current evidence | Status / next gate |
| --- | --- | --- |
| A. Complete module/menu tree | `module-function-inventory.json`, PHP/ASP.NET route catalogs, Jewellery plan | **Partial**: functional menu coverage exists; final screenshot and permission reconciliation remains open |
| B. Legacy-to-new field mapping | `evidence/jewellery/INDUS_LIVE_FIELD_AND_WORKFLOW_MAPPING.md`, PHP schema inspection | **Partial**: observed fields mapped; every transaction still needs field/action/validation/DB/report trace rows |
| C. Proposed database ERD | Target entities are described in the mapping and D365 audit | **Missing as a single artifact**; ERD below is now the canonical proposal |
| D. Transaction state machine | Per-process lifecycle gates and repair/purchase/fixing status contracts | **Partial**; common state contract below is target policy, while PHP-specific transitions require fixtures |
| E. Inventory movement architecture | Stock, tag, transfer, verification evidence and PHP ownership notes | **Partial**; immutable movement ledger is a target gap, not a new PHP table |
| F. Accounting posting architecture | `ErpGlPostingService`, GL journal/line readback, finance workspace | **Partial**; central posting rules and full Jewellery source-to-posting matrix remain open |
| G. Role/permission matrix | ERP RBAC, capability windows, company/site scope, audit | **Partial**; Jewellery AML/cost/margin/backdate permissions need explicit acceptance |
| H. Reporting catalogue | PHP/ASP.NET report inventories and Jewellery history projections | **Partial**; drill-down/export/print parity is not accepted for every report family |
| I. Migration/reconciliation strategy | tenant safety, PHP decommission, ERP acceptance and Jewellery plans | **Present**; Jewellery stock/WIP/consignment reconciliation needs executable fixtures |
| J. Automated testing strategy | contract tests, focused service tests, throwaway rehearsal skill | **Partial**; golden transaction scenarios need stock + accounting + tax + audit + reversal assertions |
| K. Screenshot/Excel gap list | visual parity matrix, field mapping, acceptance board | **Present and expanded below**; Excel workbook was not supplied in this attachment |
| L. Revised roadmap and measurable gates | ERP completion directive and Jewellery completion plan | **Present**; this document adds dependency order and target architecture gates |

## Target ERD (proposal, not PHP schema)

```mermaid
erDiagram
  COMPANY ||--o{ BRANCH : owns
  BRANCH ||--o{ LOCATION : contains
  COMPANY ||--o{ PARTY : scopes
  PARTY ||--o| CUSTOMER : specializes
  PARTY ||--o| SUPPLIER : specializes
  JEWELLERY_ITEM ||--o{ ITEM_COMPONENT : contains
  JEWELLERY_ITEM ||--o{ INVENTORY_UNIT : produces
  INVENTORY_UNIT ||--o| TAG : identifies
  INVENTORY_UNIT ||--o{ STOCK_MOVEMENT : moves
  DOCUMENT ||--o{ DOCUMENT_LINE : contains
  DOCUMENT_LINE }o--|| INVENTORY_UNIT : references
  DOCUMENT ||--o{ POSTING_BATCH : posts
  POSTING_BATCH ||--|| JOURNAL_HEADER : creates
  JOURNAL_HEADER ||--|{ JOURNAL_LINE : balances
  DOCUMENT ||--o{ APPROVAL : requires
  DOCUMENT ||--o{ AUDIT_EVENT : records
  LEGACY_KEY_MAP }o--|| DOCUMENT : maps
```

Minimum proposed invariants:

1. `CompanyId`, `BranchId`, `LocationId`, and `FinancialYearId` are explicit
   on every posted stock or accounting source.
2. `InventoryUnit` keeps item code, stock code, tag/barcode, batch/lot,
   gross weight, stone weight, net metal weight, purity, pure weight, carat,
   and valuation distinct.
3. `StockMovement` is append-only; corrections reference a reversal movement.
4. `JournalHeader` cannot post unless debits equal credits in LC.
5. `LegacyKeyMap` preserves every imported PHP/INDUS identifier and source
   system.

## D. Common transaction lifecycle

```text
Draft
  -> Submitted (validation, dimensions, period, permissions)
  -> Approved (when approval policy requires it)
  -> Posted (atomic stock/tax/accounting/audit effects)
  -> Reversed (controlled compensating document)
  -> Cancelled (only before posting, with reason and audit)
```

PHP may use different labels or fewer states for a specific module. The
ASP.NET implementation must preserve the PHP transition and expose the richer
state only when the source behavior and acceptance evidence support it.

## E. Inventory movement architecture

The target movement record is:

```text
MovementId, CompanyId, BranchId, FinancialYearId,
SourceDocumentType, SourceDocumentId, SourceLineId,
ItemId, InventoryUnitId/TagId,
FromLocationId, ToLocationId,
Qty, GrossWeight, StoneWeight, NetWeight, Purity, PureWeight, StoneCarat,
CostValueFC, CostValueLC, CurrencyId, ExchangeRate,
EventTime, PostedBy, ReversalMovementId
```

Jewellery acceptance must prove both the physical-unit path (tag/barcode) and
the measured-metal path (PCS/gross/stone/net/pure weight). The current
throwaway rehearsals prove selected PHP-owned writes and readback; they do not
yet prove a universal immutable movement ledger or manufacture/WIP
reconciliation.

## F. Accounting posting architecture

The target posting pipeline is:

```text
PostedDocument
  -> PostingRulesEngine
  -> PostingBatch
  -> JournalHeader
  -> JournalLines
  -> AuditEvent
```

Required line context is account, debit/credit, FC/LC amount, currency,
exchange rate, branch, cost centre, party, tax code, source document/line,
allocation reference, and narration. Candidate mappings such as POS sale and
purchase in the attachment remain **open** until matched to PHP account
configuration and a balanced throwaway fixture.

## G. Role and permission matrix

| Capability | Operator | Sales | Inventory | Finance | Approver | Auditor |
| --- | --- | --- | --- | --- | --- | --- |
| View operational records | scoped | scoped | scoped | scoped | scoped | scoped |
| Create/edit draft | yes | sales | inventory | finance | no | no |
| Submit | yes | sales | inventory | finance | no | no |
| Approve/post | no | no | no | policy | approved scope | no |
| Reverse/cancel | no | pre-post only | controlled | finance | approved scope | no |
| View cost/margin | policy | policy | policy | yes | policy | policy |
| View AML/KYC | no | masked | no | no | approved scope | approved scope |
| Backdate/reopen period | no | no | no | no | explicit grant | no |
| Export/print | scoped | scoped | scoped | scoped | scoped | yes |

This matrix is a target control contract. Actual grants must remain
company/branch/location/module/action scoped and be corroborated by audit
readback.

## H. Reporting catalogue

Required Jewellery report families:

- metal stock balance and metal stock ledger;
- diamond/stone stock and own-versus-consignment;
- branch/location/in-transit/repair stock;
- stock verification and variance;
- sales, POS collection, salesman, customer and supplier statements;
- account position, trial balance, P&L, balance sheet and VAT;
- purchase, fixing, manufacture, wastage/purity-difference and tag history;
- audit trail, document register and management dashboard.

Each report gate is `View → Filter → Drill down → Export Excel → PDF/Print`
where the PHP reference supports that action. Sample rows are never reported
as populated live-source parity.

## J. Golden test strategy

The minimum scenario suite is:

1. purchase and purchase fixing;
2. manufacture with metal/stone/other components and approved loss;
3. branch/location transfer and receipt;
4. stock verification and adjustment;
5. POS sale, multiple tenders and sale return;
6. old-gold exchange and scheme redemption;
7. receipt, payment and journal voucher;
8. repair receipt → transfer → workshop receive → delivery;
9. period close and controlled reversal.

Every scenario must assert document result, stock result, accounting result,
tax result, audit result, negative path, retry behavior, and reversal where
the PHP process supports it. Browser, second-database isolation, recovery,
UAT, and production gates remain separate from service tests.

## K. Current concrete gap list

The attachment identifies requirements that are not yet accepted in the
current migration:

- no single accepted ERD-backed universal `StockMovement`/reversal ledger;
- manufacture component/WIP/pure-weight loss reconciliation remains open;
- complete Jewellery POS, old-gold, scheme, tender/refund, and sale-return
  accounting parity remains open;
- customer KYC/AML field, permission, attachment, and approval parity remains
  open;
- common posting-rules engine coverage across Jewellery source documents
  remains incomplete;
- repair receipt → transfer → workshop receive → delivery persistence is now
  evidenced on throwaway data, but browser, audit, reversal, isolation,
  recovery, UAT, and production gates remain open;
- report filter/drill/export/print parity is not accepted for every listed
  report family;
- the workbook/Excel-specific field gap list cannot be closed until the
  workbook is available for inspection.

These are acceptance gaps, not authorization to invent PHP columns or migrate
live data.

## L. Dependency-ordered roadmap

1. **Foundation:** organization scope, period control, permissions, audit,
   numbering, shared workspace, and source-document links.
2. **Inventory truth:** normalized item/tag identity, measured quantities,
   movement/reversal contract, stock verification, and branch/location scope.
3. **Manufacturing:** component BOM, issue/WIP/receipt, pure-weight
   reconciliation, wastage approval, and cost/price snapshots.
4. **Commercial:** purchase/fixing, POS/tenders/returns, old-gold and schemes.
5. **Repair/service:** complete lifecycle, additions/removals, karigar,
   charges, delivery, reversal, and status reporting.
6. **Accounting/reporting:** centralized posting rules, balanced journals,
   VAT, statements, stock/finance reports, and export/print parity.
7. **Migration:** LegacyKeyMap, trial-balance/customer/supplier/cash/metal/
   diamond/tag/WIP/consignment/in-transit reconciliation.
8. **Acceptance:** native/browser UI, negative paths, SQL/audit readback,
   second physical tenant DB, recovery/rollback, UAT, and release-owner
   production approval.

No module is marked accepted from route or page existence alone.
