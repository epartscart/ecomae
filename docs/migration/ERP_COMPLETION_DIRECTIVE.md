# ERP completion directive — completion, not expansion

## Platform objective (2026-10-05)

ECOM AE is an enterprise ERP + BOS + CRM platform. It is not evaluated only as
a PHP-to-ASP.NET migration, and it is not complete when PHP is removed.

The functional and visual benchmark is Microsoft Dynamics 365 Finance &
Operations and Dynamics 365 Sales, SAP S/4HANA and SAP Sales Cloud, and Oracle
Fusion ERP and Oracle Fusion SCM. PHP functionality, data, workflow, and
accounting parity is the minimum for ERP, not the final standard. A module is
not complete because its routes exist. The benchmark matrix, ownership, and
acceptance rules are in `ASPNET_MIGRATION_TRACKER.md` under "Enterprise
platform benchmark". Nothing in that matrix is accepted unless this directive's
board says so.

### Who owns what

Devin owns ERP. ERP is the transactional system of record. Every ERP module is
an end-to-end business process: Order-to-Cash, Procure-to-Pay, Inventory/WMS,
Record-to-Report, Treasury, Tax/Compliance, and Fixed Assets, plus HR/Payroll,
Production, Retail, Service, Jewellery, and Fit-out. Each process needs
transaction creation, validation, approval, posting, accounting impact,
reversal or correction, an audit trail, reporting, permissions, tenant /
company / site isolation, and production evidence. Finance role workspaces for
CFO, Finance Manager, Accountant, Sales Manager, Purchasing Manager, and
Warehouse Manager are part of that ERP bar.

Cursor owns BOS, Control Panel, CRM, storefront/frontend, marketing, and
tenants. BOS is a business-operations control tower: CRM, Customer 360,
Supplier 360, executive dashboards, approvals, tasks, exceptions, KPIs,
notifications, analytics, cross-company visibility, and AI recommendations.
CRM is benchmarked against Dynamics 365 Sales and SAP Sales Cloud, from lead
through qualification, account and contact, opportunity, activity, quotation,
and approval, then into the ERP order, fulfillment, invoice, and collection
chain. Cursor also owns the CEO, CFO, Sales, Purchasing, Operations, and
Management workspaces that sit on top of those processes, and the Control
Panel control plane (tenant configuration, users, roles, permissions,
subscriptions, deployment, monitoring, localization, and platform admin).

Cursor's current storefront and Control Panel baseline is draft PR #1970 on
`cursor/cp-frontend-parity-4911`. The tip recorded for this plan is
`08373de0f5840065d53d150436b680a915ab56be` (it had been `22df603f9` and can
move). That branch is PHP-parity work. It is the minimum, not enterprise
completion. Do not rebase it from this plan and do not treat it as an accepted
process.

### One business engine

Do not create a second order, inventory, invoice, payment, approval, or
ledger engine in BOS, CRM, or the Control Panel. Those surfaces consume the
shared ERP domain services, APIs, and workflows. One transaction has one
source of truth, one authorization model, one workflow engine, one audit
trail, one accounting result, and one API contract. Role screens may differ.
Business logic stays shared. Python remains AI-only and must not own the
transaction.

Owner instruction (2026-10-01, still in force for ERP): complete the ASP.NET
Core ERP as early as possible. For Devin, the objective is time to accepted
ERP, not more ASP.NET code. Enterprise comparison does not replace the
acceptance gates below.

## Scope freeze

Until the ERP exit gate passes, Devin's ERP sessions must not spend effort on
marketing, storefront modernisation, optional Control Panel improvements,
experiments, animation, or cosmetic polish beyond the shared design system.
Only security, regulatory, data-integrity, or unavoidable architectural
requirements may interrupt the ERP sequence. Cursor may work the surfaces it
owns in parallel, and must call ERP for any transaction. New ERP ideas go to
the backlog below as `POST-MIGRATION ENHANCEMENT` and are not implemented in
an ERP session. The benchmark matrix is a planning record, not a build list
for this pass.

## Official execution sequence

Devin, inside ERP: `ERP → ERP acceptance → remaining CP / cross-surface
handoff → storefront handover → operations → final cutover`.

Cursor, in parallel and without a second engine: BOS control tower, CRM,
Control Panel control plane, storefront/frontend, marketing, and tenants.

Within ERP: Wave 1 shared foundation (built once: shell, design system,
navigation, permissions, company switch, form/grid/document header/action bar/
status/totals/attachments/notes/audit/print/approval/validation) → Wave 2
Order-to-Cash → Wave 3 Procure-to-Pay → Wave 4 Inventory/WMS → Wave 5 Finance →
Wave 6 remaining ERP, Jewellery, Fit-Out. Each wave is BUILD → TEST → ACCEPT →
LOCK; accepted processes are not reworked without a regression.

Rules: work by complete business process, never by route/shell/digest count;
PHP is the minimum specification (`PHP → inspect → document → port → test →
compare → accept`), and the Dynamics 365 / SAP / Oracle benchmark is the
target; shared components (e.g. `ErpDocumentWorkspace`) are authoritative and
reused across SO/SI/PO/PI/RFQ/GRN/delivery/voucher; automate PHP-vs-ASP.NET
comparison (routes, fields, actions, permissions, validation, DB writes,
reports, tenant scope, side-by-side screenshots) where practical.

## Definition of done (per process)

PHP route, field, action, calculation, validation, permission, and workflow
parity is the minimum. The process is still open until it also has transaction
creation, validation, approval, posting, accounting impact, reversal or
correction, an audit trail, reporting, permissions, tenant/company/site
isolation, and production evidence, plus DB read/write verification,
source-document links, print/export, visual/UX parity against both the PHP
reference and the Dynamics 365 / SAP / Oracle benchmark, desktop evidence,
performance, rollback, automated tests, and human acceptance. Then **LOCK**.
Routes, shells, and digests do not close the process.
Priority classes: P0 functionality/accounting/security/data/permissions/
workflows (perfect before acceptance); P1 major UX/layout/forms/grids/
dashboards/reports (professional parity before acceptance); P2 micro-polish
(backlog).

## Completion board (evidence-based; shells/routes/digests do not count)

Sources: `artifacts/erp-dummy-fixture-matrix.json`, `artifacts/erp-b*-*.json`,
`ErpTenantAcceptanceCatalog`. "Accepted" requires all gates incl. isolation,
rollback, UAT; none are accepted yet. Percentages are approximate bands and
must be re-derived from the artifacts on each report. They are implementation
telemetry only, not acceptance credit: a process remains **OPEN** when any
mandatory acceptance gate lacks direct evidence, even if its development
percentage reaches 95% or 100%.

2026-10-05 update: all 321 `ajax_erp.php` cases have live ASP.NET coverage
(`evidence/write-dryruns/erp-ajax-case-coverage-2026-10-05.md`); every
functional ERP ajax route is a live PHP twin behind `confirmWrites` + session
csrf_guard_key, and a consolidated O2C+P2P cycle ran end-to-end on the
throwaway DB (`evidence/write-dryruns/erp-consolidated-cycle-2026-10-05.md`).
Sixteen end-to-end business-cycle rehearsals ran live on the throwaway DB
with SQL corroboration and zero residue (`evidence/write-dryruns/erp-*-cycle-2026-10-05.md`):
O2C+P2P consolidated, inventory receipt/issue, period lock/reopen, WMS
transfer, fixed-asset depreciation, payroll, treasury (petty cash, bank
transfer, payment batch), e-invoice credit note, PO lifecycle, contacts
sync, HR expense, RFQ award, delivery note, opening-balance batch,
workspace writes. One real defect found and fixed (payment-batch status
param binding). Remaining deltas are acceptance-side evidence (isolation,
rollback, UAT, production), not missing routes.

Fact check against `origin/main` `3ebfe31fa` (#2026), which is later than the
#1987 read-only review. Write tranches #1971–#1987 are merged, and later ERP
PRs through #2026 are merged as well. That does not accept any process. The
"not missing routes" sentence above is the #2013 route-coverage claim. It does
not mean payroll GL, credit-note XML/ASP submission, the on-premises pack, or
the ajax-writes catch-all are live.

- E-invoice create, ASP poll, and seller sync are live `confirmWrites` twins
  (#1988, #1990, #1991). An unconfirmed post still returns the dry-run payload.
- Credit-note document creation is a live twin of
  `epc_einvoice_create_credit_note` and was rehearsed on a throwaway database
  (`erp-credit-note-cycle-2026-10-05.md`: type 381 document and cumulative
  cap). That note does not record credit-note XML generation or ASP submission
  of the credit note.
- Document upload/delete and logo/attachment upload write through
  `IErpDocControlWriteService` when `confirmWrites` is true (#1989). Without
  confirmation they stay on the dry-run evaluator.
- Customs declaration save, submit, and delete (#1992) and the declaration PDF
  parser (#1994) are live twins. The save/submit route comment still leaves
  PDF attach, box autofill, LGP, and schema-ensure on PHP.
- Jewellery sample seed is a live twin (#1993). Acceptance gates for Jewellery
  stay open on the board below.
- Still dry by design, per `erp-ajax-case-coverage-2026-10-05.md`: the
  `/erp/ajax-writes/dry-run/{action}` catch-all, and the six on-premises CLI
  rows (`health`, `license-activate`, `activate-license-cli`,
  `health-check-pack`, `setup-wizard`, `backup`).
- Payroll generate, approve, and pay was rehearsed
  (`erp-payroll-cycle-2026-10-05.md`) as a cash payment. `ErpPayrollPayWriteService`
  states that COA GL posting stays PHP. Payroll GL posting is not accepted.

The board paragraph above was last edited in #2013 and names sixteen throwaway
cycles. Later merged notes, through #2026, add throwaway rehearsals for
collections, contracts, subscriptions, insurance, org admin, after-sales,
cash and treasury, RBAC, and withholding under
`docs/migration/evidence/write-dryruns/`, plus landed cost, VAT refund, and a
lazy-schema sweep under `aspnet/docs/migration/evidence/write-dryruns/`.
Those notes did not change the percentage bands in the table and did not
accept a process. They are not production evidence. The live-gate file
`evidence/presentation/MODULE_FUNCTION_PARITY_STATUS_LIVE.md` still records
interactive ASP.NET complete **0** and PHP as the authoritative runtime.

| Process | Functional | UI/UX | Reports | Writes | Testing | Accepted |
|---|---:|---:|---:|---:|---:|---:|
| Foundation (shared shell/workspace/permissions) | 90% | 75% | n/a | n/a | 60% | No |
| Order-to-Cash (B2) | 85% | 65% | 40% | 85% | 45% (cycle proven locally; isolation/rollback/UAT pending) | No |
| Procure-to-Pay (B3) | 85% | 70% | 45% | 85% | 70% (PO lifecycle + RFQ award cycles proven) | No |
| Inventory/WMS (B4) | 75% | 65% | 40% | 70% | 55% (receipt/issue + transfer cycles proven) | No |
| Finance/GL (B5) | 75% | 60% | 40% | 75% | 25% (period lock/reopen + opening batch cycles proven) | No |
| AR/AP | 70% | 60% | 40% | 70% | 25% (open-balance + knock-off + contacts sync proven) | No |
| Treasury (B7) | 60% | 55% | 30% | 60% | 30% (bank import/reconcile + petty cash + batch cycles proven) | No |
| Tax/E-Invoice (B8) | 75% | 60% | 40% | 70% | 15% (PINT-AE issue/poll/credit-note live + cycle proven) | No |
| Fixed Assets | 40% | 30% | 10% | 40% | 15% (depreciation cycle proven) | No |
| CRM | 65% | 60% | 30% | 55% | 0% | No |
| HR/Payroll | 40% | 40% | 20% | 35% | 20% (payroll + expense claim cycles proven) | No |
| Reporting/IFRS (B6) | 60% | 65% | 55% | n/a | 0% (no fixture) | No |
| Jewellery (BJ) | 70% | 60% | 40% | 60% | 25% (seed/lifecycle live; acceptance gates open) | No |
| Fit-Out (BF) | 65% | 60% | 45% | 55% | 0% (no fixture) | No |
| Administration/Org settings | 70% | 65% | n/a | 60% | 15% | No |

Formal weighted tracker headline remains **20.4% complete / 79.6% pending**
(`ASPNET_MIGRATION_TRACKER.md`); ERP accepted-process count is **0/15** —
implementation telemetry moved materially (route coverage complete) but no
mandatory acceptance gate (isolation, rollback, UAT, production evidence) has
new evidence, which remains blocked on server access per the blockers below.

Tenant promotion is tenant-specific. The gate contract in
`TENANT_BY_TENANT_ERP_MIGRATION_GATE.md` and its machine-readable board in
`evidence/tenant-by-tenant-erp-migration-gate.json` require an independent
evidence bundle for every named tenant. One tenant's rehearsal, weighted
percentage, health response, or release cannot satisfy another tenant's ERP
acceptance; backup/restore, rollback, UAT, and release-owner approval remain
mandatory before any tenant ownership switch.

## Session output format

Every session ends with: COMPLETED & ACCEPTED · COMPLETED BUT AWAITING
ACCEPTANCE (evidence remaining) · CURRENTLY IN PROGRESS · BLOCKED (exact owner
input required) · NEXT SESSION · REMAINING ERP % (recalculated from accepted
process gates).

## Known blockers requiring owner input

- Legitimate second tenant database for tenant-isolation evidence (currently
  only one throwaway local DB; live tenants are read-only).
- Production backup/restore and ownership-rollback rehearsal window.
- Human visual acceptance of PHP vs ASP.NET side-by-side captures.
- Country compliance data / external integrations for Tax, E-Invoice, payroll.

## Enhancement record

The enterprise benchmark is the target standard now. It is not waiting on the
words "PHP removed". Devin still does not implement new ERP scope ahead of the
open acceptance gates. Cursor does not implement a second transaction engine.
The seeded matrix in `ASPNET_MIGRATION_TRACKER.md` is the record. This plan
pass does not implement it.

- Enterprise ERP, BOS, and CRM benchmark matrix (Dynamics 365, SAP, Oracle).
  Acceptance status on every seeded row: not accepted.
