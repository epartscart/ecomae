# Jewellery ERP completion plan

This plan is the Jewellery (`BJ`) workstream under the ERP completion directive.
The attachment reconciliation and A–L first-deliverable register are maintained
in `INDUS_JEWELLERY_REPLACEMENT_TRACEABILITY.md`.
It
uses the supplied INDUS studies and photo pack as requirements evidence, while
keeping PHP as the behavioral authority until each process is accepted.
The supplied `INDUS_LIVE_Maximum_Fields_Legacy_Style_ERP_2.xlsx` is now also
recorded as field-catalogue evidence; it expands the explicit acceptance
surface but does not establish PHP schema or behavior by itself.

## Evidence boundary

The supplied material describes an INDUS Windows/VB6 application and includes
observed screens for metal purchase/fixing, barcode generation, stock
verification, retail/POS, metal sale/fixing, diamond purchase, sales return,
petty cash, journal voucher, metal stock balance, and metal sales analysis.
The material also distinguishes `OBSERVED`, `INFERRED`, `PROPOSED`, and `OPEN`
requirements. Observed controls are parity targets; inferred or proposed
behavior must not be presented as legacy fact until confirmed with PHP data or
an approved business decision.

The photo pack is visual evidence only. It does not authorize schema changes,
posting rules, or live-tenant writes.

## Target process chain

`masters → purchase → material stock → manufacture/tag → transfer/verification
→ sale/return → repair/fixing → finance/reporting`

Every process must pass `BUILD → TEST → ACCEPT → LOCK`.

| Process slice | PHP authority and ASP.NET surface | Required functional evidence |
| --- | --- | --- |
| Karat, metal, rate type, currency, stone, pearl, diamond, design | `epc_erp_jewellery.php`; Jewellery masters workspace | PHP field/action parity, company scope, dry-run denial, confirmed persistence, SQL readback, update parity |
| Metal and diamond linkage | `epc_jewel_design`, `epc_jewel_design_metals`, `epc_jewel_design_stones`, diamond master; master/design readback | Component-level metal/stone/diamond linkage, purity/carat/weight preservation, existing-master update, SQL readback |
| Purchase and fixing | PHP jewellery voucher/fixing functions; fixing and retail workspaces | Fixed/unfixed state, rate date/rate snapshot, gross/net/pure weight, making/metal/stone/wastage values, branch/division scope, posting/audit |
| Barcode/tag identity | PHP barcode/tag functions; masters/retail workspace | Unique company-scoped tag, barcode lookup, availability guard, location/stock status transition, retry and cleanup |
| Stock and verification | PHP metal-stock/barcode tables; stock-verification workspace | Computer vs physical pieces/weights, scan batch, branch/location, variance controls, SQL readback, audit |
| Retail/POS and returns | PHP voucher, receipt, return, tourist VAT flows; retail workspace | Customer/identity, stock-code lines, karat rate, old-gold exchange, advance/scheme, VAT, tender/refund/due, return guard |
| Repair/workshop lifecycle | PHP repair, transfer, workshop receive, delivery tables; repairs workspace | Create → transfer → receive → delivery/status, customer/weight/stone details, company scope, audit and reversal behavior |
| Finance/history/reporting | PHP voucher, journal, petty cash and report projections | Balanced source linkage, numbering, finance history, purchase/sale/advance/journal/fixing/repair readback, report filters |

## Non-negotiable domain controls

- Pieces and weights are first-class quantities.
- Gross weight, stone weight, net metal weight, purity, pure weight, carat
  weight, and monetary decomposition remain line-level data.
- Fixed/unfixed metal pricing retains the rate and effective date used.
- Tags/barcodes identify physical stock units and are company/location scoped.
- Branch, location, division, and financial year are explicit dimensions.
- Stock movement and posting must be auditable and safely reversible; no
  replacement table may be invented over a PHP-owned schema.
- Dummy data is allowed only in a throwaway tenant database with tagged cleanup.
  Live tenants and production remain untouched.

## Acceptance gates

The following gates are tracked separately and none may be inferred from route
presence or static contract tests:

1. PHP route/field/action/validation/permission parity.
2. ASP.NET UI/browser acceptance against the observed screens.
3. Positive and negative functional paths using isolated dummy data.
4. Persistence and direct SQL readback from PHP-owned tables.
5. PHP audit-log readback where the authority records an audit event.
6. Company/tenant isolation with a legitimate second physical database.
7. Recovery/rollback and ownership rollback.
8. UAT and production acceptance by the release owner.

## Current verified boundary

The existing ASP.NET Jewellery unit/contract suite covers the current UI
surfaces, company guards, voucher atomicity/totals, master update fields,
barcode/tag guards, fixing guards, repair lifecycle, and history projections.
The supplied blueprint has now been reconciled into this plan and focused
regression coverage.

This does **not** close Jewellery acceptance. Confirmed throwaway-database
write/readback, authenticated browser workflows, second-database isolation,
recovery/rollback, UAT, and production acceptance remain open until captured
separately.

## Next executable tranche

1. Provision or reuse only an isolated throwaway tenant database after
   `SHOW CREATE TABLE` inspection of every PHP-owned Jewellery table.
2. Seed tagged karat/metal/stone/pearl/diamond/design/tag rows.
3. Rehearse purchase/fixing → barcode/tag → stock verification → retail sale/
   return, plus repair transfer/receive/delivery and finance history.
4. Capture native UI, SQL, audit, denial, retry, and cleanup evidence.
5. Update the Jewellery fixture entry only for gates actually observed.

The plan is intentionally completion-focused; new replacement architecture
ideas from the studies remain post-migration backlog items until their legacy
rules are verified and the acceptance gates above are complete.
