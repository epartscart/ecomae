# ERP cash journal atomicity + accounting-integrity scan (2026-10-12)

Audit findings 1, 2 and 4 from NOTE_FOR_DEVIN_2026-10-07 fixed.

## Finding 1 — cash entry without journal
`TryPostCashEntryAsync`/`TryPostAdvanceAsync` swallowed `ErpWriteException`/`DbException`,
so a failed GL post left a committed row with `gl_journal_id = 0`. Now the entry INSERT and
`PostCashEntryAsync` run inside the same document transaction on every path
(generic entry, receipt voucher, payment voucher, transfer legs); a posting failure
(e.g. missing COA) refuses the whole write. Advance receipts still post only the
advance-liability journal (no double post).

## Finding 2 — integrity scan invariants
`ErpIntegrityService.ScanAccountingAsync` adds posting checks the orphan scan could not see:
`gl_unbalanced` (active journals whose lines do not balance after 2dp rounding),
`cash_entries_missing_journal`, and sub-ledger vs control drift for AR→1100, AP→2000,
cash→1000/1010. Rows surface under `accounting[]` in `/erp/ajax/integrity-scan`.

## Finding 4 — gap-free voucher numbers
Voucher `NextAsync` moved inside the document transaction (RV/PV/TV and GV journals);
the sequence row lock and increment roll back with the document, so failed writes no
longer burn numbers. `IErpVoucherNumberService.EnsureSchemaAsync` creates the sequence
table before the transaction opens (DDL cannot live inside it).

## Proof (ErpCashJournalAtomicityTests, throwaway ecomae_cpw_* schema)
- missing revenue/AR COA: write refused, 0 entries, 0 journals, RV last_seq = 0
- retry after failure reissues the same next voucher number; no orphan entries
- clean DB: all accounting checks `clean`; seeded orphan + unbalanced journal → `dirty`
- 242 cash/voucher/journal/integrity tests pass, zero residue
