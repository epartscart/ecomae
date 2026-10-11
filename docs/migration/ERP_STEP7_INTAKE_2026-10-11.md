# ERP step-7 intake — Devin's reconciliation of plan ↔ roadmap (2026-10-11)

Roadmap step 7 requires this reconciliation to be committed before further ERP
code. Devin's ERP PRs through #2026 are merged; Cursor's ERP commits (#2030 and
the checkout/payment/e-invoice side effects listed in
`NOTE_FOR_DEVIN_2026-10-09.md`, plus PR #8 enhancements — PIM attributes and
`/erp/syncron-app` already on `main`) are on top. Ownership: ERP returns to
Devin (owner message 2026-10-11); Cursor keeps storefront, CP, BOS, CRM,
marketing, tenants.

## Read in the required order — done

1. `ERP_COMPLETION_DIRECTIVE.md` — wave order, definition of done, board,
   scope freeze, owner blockers.
2. `ERP_OWNERSHIP_AUDIT_2026-10-07.md` + `NOTE_FOR_DEVIN_2026-10-07.md` /
   `NOTE_FOR_DEVIN_2026-10-09.md` — the six open findings.
3. `TENANT_BY_TENANT_ERP_MIGRATION_GATE.md` + gate JSON — per-tenant evidence.
4. Rehearsal evidence: 26 `*-cycle-*` notes in
   `docs/migration/evidence/write-dryruns/` (incl. the 2026-10-11
   `erp-costing-cycle` from #2072) + 3 under `aspnet/docs/…`, the 321-case
   `ajax_erp.php` coverage audit, and the B1–B8 decommission runbooks.
5. Enterprise benchmark matrix in `ASPNET_MIGRATION_TRACKER.md` — no row
   accepted.

## Board owner and percentages (re-derived from artifacts)

Board owner is **Devin** again (was Cursor during the 2026-10-07→10-09
handover). Re-derived bands on `origin/main` after #2072, counting only
direct evidence: functional/writes bands unchanged since #2013 (route coverage
complete at 321/321 `ajax_erp.php` cases, lazy-schema sweep closed the
`not provisioned` guards); **testing** bands tick only where new cycle
evidence exists — `erp-costing-cycle-2026-10-11.md` adds the cost-centre +
costing-model cycle (Finance/GL +5 → 30%). Every process remains **0/15
accepted**: isolation, rollback, UAT and production evidence are unchanged and
still blocked below. Weighted tracker headline 20.4% is telemetry, not
acceptance.

## The six open findings, placed into waves

From `ERP_OWNERSHIP_AUDIT_2026-10-07.md`, re-checked on `main` by Cursor on
2026-10-09 — none fixed:

| # | Finding | Wave / order |
|---|---|---|
| 1 | `TryPostCashEntryAsync` swallows GL failures → cash entry with `gl_journal_id = 0` | **Wave 1** (foundation: posting integrity) — first |
| 2 | `ErpIntegrityService` orphans only → add unbalanced-journal and sub-ledger-vs-control (AR 1100, AP 2000, cash 1000/1010) checks | **Wave 1** — second |
| 4 | Cash/journal/transfer voucher numbers not gap-free after failed write (numbering before the document transaction) | **Wave 1** — third |
| 5 | Untested write services: `ErpOpeningPostBatchWriteService`, `ErpPettyCashWriteService`, `ErpPmListingSaveWriteService`, `ErpPrjaRecognitionWriteService`, `ErpQmOrderCreateWriteService`, `ErpRfqResponseWriteService`, `ErpRtlPosSaleWriteService` (`ErpAmlComplianceWriteService` now has tests) | **Wave 1** — with the service being fixed, plus opening-batch + PRJA recognition first (reported figures) |
| 3 | `ErpRtlPosSaleWriteService` posts no journal and no stock movement; `double` money | **Wave 4** (Inventory/WMS: POS → ledger + stock) — fixed together with #6 in that file |
| 6 | Floating-point money in `ErpOrderFulfillmentWriteService`, `ErpRtlPosSaleWriteService`, `ErpPfDemoSyncWriteService`, `ErpOplPlanningWriteService` | each fixed in its own wave (3 in W4; PF/OPL in W4 adjacent; fulfillment already in O2C writes) |

Then the remaining waves per the directive: O2C → P2P → Inventory/WMS →
Finance → rest incl. Jewellery and Fit-Out, every finished page held to the
better-than-PHP UX bar (`ASPNET_MIGRATION_TRACKER.md`, "The UX bar").

## Deferred items carried from step 7

- Port `devin/1790743008-tenant-install-control-plane` (`4139c40c4`) with
  tests — security hardening, at tenant setup (or earlier if needed).
- `devin/1790970551-b8-acceptance-audit` evidence edits — keep only what is
  still true vs merged B8 evidence.
- PR #8 leftovers: bank reconciliation and intercompany stay unported (`main`
  has its own); PIM and Syncron are already on `main` per #2030 work — no
  action.

## Owner blockers still open (acceptance gates cannot pass without these)

1. Legitimate second tenant DB for tenant-isolation evidence.
2. Production backup/restore + ownership-rollback rehearsal window.
3. Human visual acceptance (PHP vs ASP.NET side-by-side).
4. Country compliance data / external integrations (Tax, E-Invoice, payroll).
5. Missing `epc_erp_syncron_policy.php` reference (Syncron built from PR #8
   instead).
6. ERP-only staff role parity (PHP ERP-only vs ASP.NET CP+ERP).

## Rules carried forward verbatim

Journals only via `ErpGlPostingService.PostJournalAsync`; `confirmWrites:true`
is the only write path and dry-run writes nothing (not even lazy DDL);
throwaway `ecomae_cpw_*` schemas dropped after tests; never write `docpart` /
`ecomae.users`; `ReadyForPhpRemoval`, `PhpSourceDeletionAllowed`,
`CutoverAllowed` stay false; no PHP deletion; full `dotnet test` locally with
`ECOMAE_LOCAL_MARIADB_E2E_DSN` and counts posted per PR; gap ratchet via
`scripts/php_reference_gap_inventory.py` only when a gap truly closes.
