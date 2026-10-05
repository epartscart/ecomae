# ERP period-close cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

Same authenticated session, live routes only.

| Step | Route | Result |
|---|---|---|
| calendar | POST /erp/ajax/fin-periods-generate `fy=2026` | ok — `epc_fin_periods` 12 rows (Jan–Dec 2026), idempotent ON DUPLICATE |
| lock | POST /erp/periods/lock `yearMonth=2026-09` | ok — `epc_erp_periods` status=locked, `epc_erp_fiscal_locks` lock_date=month-end, close_log row; checklist written (1 pending e-invoice warning, 0 blockers) |
| journal in locked month | POST /erp/gl-journals/manual `journalDate=2026-09-15` | verbatim refusal `Journal posting is blocked because the accounting period is locked`, zero rows |
| reopen | POST /erp/periods/reopen `yearMonth=2026-09` | ok — period back to open, fiscal lock cleared |
| journal after reopen | same POST | ok — `epc_erp_gl_journals` 136 dated 2026-09-15, balanced Dr 1100=100 / Cr 4000=100 |

Fixture note: `epc_fin_periods` (PHP install-time table, columns per `epc_erp_fin_advanced.php:18-30`) was absent from the local fixture and was provisioned additively with the verbatim PHP DDL — same class of fixture gap as `users`, not a code bug.

Cleanup: journal, lines, period, fiscal locks, close log and the 12 generated fin periods deleted — residue 0.
