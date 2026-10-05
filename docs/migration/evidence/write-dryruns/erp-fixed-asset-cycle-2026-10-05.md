# ERP fixed-asset depreciation cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

Same authenticated session, live routes only.

| Step | Route | Result |
|---|---|---|
| register | POST /erp/ajax/fa-create-asset | ok — `epc_erp_fa_assets` 1 (CYC-FA-1, cost 12000, SLM 12mo) |
| depreciate | POST /erp/ajax/fa-run-depreciation `periodMonth=2026-01` | ok — run 1, `epc_erp_fa_depreciation_lines` 1000.00 (12000/12), accumulated 1000 → book_value 11000 |
| GL | `epc_erp_gl_journals` 137 `Fixed asset depreciation 2026-01` | balanced Dr 5100 depreciation exp 1000 / Cr 1550 accumulated dep 1000 |
| duplicate-period guard | same POST again | verbatim refusal `Depreciation already posted for 2026-01`, no second run |

Cleanup: GL lines+journal, depreciation lines+run, asset deleted — residue 0.
