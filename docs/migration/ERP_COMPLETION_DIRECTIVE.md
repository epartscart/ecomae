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

| Process | Functional | UI/UX | Reports | Writes | Testing | Accepted |
|---|---:|---:|---:|---:|---:|---:|
| Foundation (shared shell/workspace/permissions) | 85% | 75% | n/a | n/a | 60% | No |
| Order-to-Cash (B2) | 70% | 65% | 40% | 60% | 35% (6 verified, 0 open gaps listed; isolation/rollback pending) | No |
| Procure-to-Pay (B3) | 80% | 70% | 45% | 75% | 65% (17 verified, 2 open) | No |
| Inventory/WMS (B4) | 70% | 65% | 40% | 60% | 45% (11 verified, 14 open) | No |
| Finance/GL (B5) | 55% | 60% | 40% | 45% | 0% (5 open, no fixture) | No |
| AR/AP | 55% | 60% | 40% | 45% | 10% | No |
| Treasury (B7) | 45% | 55% | 30% | 40% | 0% (no fixture) | No |
| Tax/E-Invoice (B8) | 50% | 60% | 40% | 40% | 0% (no fixture) | No |
| Fixed Assets | 20% | 30% | 10% | 10% | 0% | No |
| CRM | 60% | 60% | 30% | 50% | 0% | No |
| HR/Payroll | 35% | 40% | 20% | 30% | 0% | No |
| Reporting/IFRS (B6) | 55% | 65% | 55% | n/a | 0% (no fixture) | No |
| Jewellery (BJ) | 60% | 60% | 40% | 50% | 20% (contract/service coverage; persistence and acceptance gates open) | No |
| Fit-Out (BF) | 65% | 60% | 45% | 55% | 0% (no fixture) | No |
| Administration/Org settings | 60% | 65% | n/a | 50% | 10% | No |

Formal weighted tracker headline remains **20.4% complete / 79.6% pending**
(`ASPNET_MIGRATION_TRACKER.md`); ERP accepted-process count is **0/15**.

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
