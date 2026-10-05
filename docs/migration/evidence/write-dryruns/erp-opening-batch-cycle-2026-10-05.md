# ERP opening-balance batch cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| batch | POST /erp/ajax/opening-create-batch (coa, 2026-01-01) | ok — `epc_erp_opening_batches` 1 draft |
| line | POST /erp/ajax/opening-add-coa-line (acct 1 Dr 1000) | ok — `COA opening line added` |
| line | acct 5 Cr 1000 | ok |
| post | POST /erp/ajax/opening-post-batch | ok — `Opening batch posted`, status `posted`, `opening_balance` 1000.00 / -1000.00 |

Fixture gap: `epc_erp_opening_batches`/`_lines` absent from the throwaway DB (install-time; schema-ensure stays PHP) — provisioned verbatim DDL from `content/shop/finance/epc_erp_opening.php:13-40`; missing them fails closed with `Opening batches table is not provisioned`.

Cleanup: batch+lines deleted, `opening_balance` reset — residue 0.
