# ERP completion directive — completion, not expansion

Owner instruction (2026-10-01): complete the ASP.NET Core ERP as early as
possible. Objective is **time to accepted ERP**, not more ASP.NET code.

## Scope freeze

Until the ERP exit gate passes, primary sessions must not spend effort on
marketing, storefront modernisation, optional CP improvements, experiments,
animation, or cosmetic polish beyond the shared design system. Only security,
regulatory, data-integrity or unavoidable architectural requirements may
interrupt the sequence. New ideas go to the backlog below as
`POST-MIGRATION ENHANCEMENT` and are not implemented.

## Official execution sequence

`ERP → ERP acceptance → remaining CP / cross-surface → storefront → operations → final cutover`

Within ERP: Wave 1 shared foundation (built once: shell, design system,
navigation, permissions, company switch, form/grid/document header/action bar/
status/totals/attachments/notes/audit/print/approval/validation) → Wave 2
Order-to-Cash → Wave 3 Procure-to-Pay → Wave 4 Inventory/WMS → Wave 5 Finance →
Wave 6 remaining ERP, Jewellery, Fit-Out. Each wave is BUILD → TEST → ACCEPT →
LOCK; accepted processes are not reworked without a regression.

Rules: work by complete business process, never by route/shell/digest count;
PHP is the specification (`PHP → inspect → document → port → test → compare →
accept`); shared components (e.g. `ErpDocumentWorkspace`) are authoritative and
reused across SO/SI/PO/PI/RFQ/GRN/delivery/voucher; automate PHP-vs-ASP.NET
comparison (routes, fields, actions, permissions, validation, DB writes,
reports, tenant scope, side-by-side screenshots) where practical.

## Definition of done (per process)

PHP route, field, action, calculation, validation, permission and workflow
parity; DB read/write verified; source-document links; reports; print/export;
audit; tenant/company isolation; visual/UX parity; desktop; performance;
rollback; automated tests; human acceptance evidence. Then **LOCK**.
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

## Post-Migration Enhancement Backlog

Record only; do not implement before ERP acceptance.

- (none recorded yet)
