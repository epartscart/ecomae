# Wave-5 Finance close-out — 2026-10-11

Scope: Record-to-Report (Board B5) + tax/treasury workspace polish. Order followed the wave plan: aging → chart of accounts → GL journals → bank reconciliation → period close. VAT, multi-currency GL and cash/bank pages were inspected and already meet the workspace bar (VAT position bar + legislation, FX pair KPIs + rate detail/siblings, cash accounts with named entries + siblings) — no changes needed there.

## Merged items

- #2109 `/erp/aging-app`: totals row per AR/AP/Inventory table (`rows.Sum`, zero extra queries); 90+ bucket cells render red.
- #2110 `/erp/coa-accounts-app`: Parent column + detail Parent field resolve `parent_id` to `code — name` via `AccountLabel` (numeric-id fallback).
- #2111 `/erp/gl-journals-app`: totals row (debit/credit/balance), "Unbalanced in page" KPI + red nonzero-balance highlight; fixed a misplaced `else` hiding the reverse caption when a note excerpt exists.
- #2112 `/erp/bank-reconciliation-app`: `LEFT JOIN epc_erp_cash_bank_accounts` on the lines and detail queries; `AccountName` on both models; name-with-id-fallback on list + opened line.
- #2113 `/erp/period-close-app`: Open / Soft closed / Locked KPI counts; locked rows highlighted.

## Verified live

Local Debug app (throwaway-capable `ecomae` DB): aging totals rows and red 90+ cells; COA `1000 — Cash on hand` parent label (fixture row, removed after); GL totals row + unbalanced KPI; bank-recon `Main cash — AED` account name (fixture line, removed after); period-close KPI counts. All fixture writes were deleted; no residue.

## Remaining gaps (not wave-5 scope)

- Formal acceptance still needs owner-side items: second tenant DB, backup/restore window, human visual acceptance, country compliance data, `epc_erp_syncron_policy.php`, ERP-only staff role.
- Journal reverse stays Classic-compatible; FTA filing / EmaraTax submit stays on ERP ajax.
- Bank statement import UI polish and FX revaluation posting UX are candidates for a wave-5.1 if prioritised.

## Board B5

functional ~80% · UI/UX ~75% · reports ~70% · writes ~80% · testing ~70% — journals/COA/aging/recon/period surfaces now carry totals, resolved names and overdue/unbalanced/locked signalling at the better-than-PHP bar.
