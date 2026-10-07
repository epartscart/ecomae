# Note for Devin — ERP handover, 2026-10-07

Your quota ran out and your sessions stopped. All of your ERP pull requests up to #2026 are merged into `main`. `main` at `debe2654e` is live on CloudPanel as release `20261007084903`. `/health` and `/ready` return 200.

## Who owns what now

- The owner has assigned ERP to Cursor. Cursor finishes the CP, storefront, marketing, tenant, BOS, industry, and LifeOS migration first, then continues ERP.
- If you start again, check the open Cursor ERP pull requests before you edit `aspnet/src/EcomAE.Platform/Erp/`, and confirm with the owner which items you take. Do not start a parallel ERP branch on files with an open pull request.

## What Cursor changed in ERP (PR #2030, not merged yet)

1. `ErpGlPostingService.Validate` rounds every line to two decimals before summing and requires debit to equal credit exactly. The old four-decimal sum with a 0.0001 tolerance let a journal that is one fils out be stored in `decimal(14,2)` columns. Build journal lines from amounts already rounded with `ErpTaxAmountCalculator.Round2`. Otherwise the journal is refused.
2. A cash entry with `counterparty_type = 'internal'` (a transfer leg) posts against `1090 Cash in transit`, not 4000 revenue or 6100 expense. `1090` is in `ErpGlChartOfAccountsSeeder.SystemAccounts` and is added on the first transfer for existing tenants. Tests that list the seeded chart now include `1090`.

## Already fixed in your code (confirmed)

- The sales-order Cancel through `/erp/ajax/so-status` now runs `StatusTransitionError`.
- `epc_erp_supplier_accounting` has the same columns whichever module creates it first.

## Open ERP findings, in the order Cursor plans to work them

These are in `docs/migration/ERP_OWNERSHIP_AUDIT_2026-10-07.md`.

1. `TryPostCashEntryAsync` swallows GL failures, so a cash entry can exist with `gl_journal_id = 0`.
2. `ErpIntegrityService` checks orphans only. It has no unbalanced-journal check and no sub-ledger against control-account check.
3. `ErpRtlPosSaleWriteService` posts no journal and no stock movement. It also uses `double` for quantity and price.
4. Voucher numbers are used up by failed writes, because numbering runs before the document transaction.
5. These write services have no test: `ErpAmlComplianceWriteService`, `ErpOpeningPostBatchWriteService`, `ErpPettyCashWriteService`, `ErpPmListingSaveWriteService`, `ErpPrjaRecognitionWriteService`, `ErpQmOrderCreateWriteService`, `ErpRfqResponseWriteService`, `ErpRtlPosSaleWriteService`.
6. Floating-point money appears in `ErpOrderFulfillmentWriteService`, `ErpPfDemoSyncWriteService`, and `ErpOplPlanningWriteService`.

## Rules that stay in force

- One posting engine. Journals go through `ErpGlPostingService.PostJournalAsync`. Do not insert into `epc_erp_gl_journals` or `epc_erp_gl_lines` anywhere else, and do not rebuild ERP logic inside BOS, CP, or CRM.
- PHP parity is the minimum. Where PHP is wrong in accounting terms, as with the transfer posting above, fix it in ASP.NET and write down the difference.
- Write endpoints write only when the request sends `confirmWrites: true`. The dry-run branch must write nothing, not even lazy DDL.
- Tests use throwaway `ecomae_cpw_*` schemas and drop them. Never test against production data, and never write to `docpart` or `ecomae.users`.
- `ReadyForPhpRemoval`, `PhpSourceDeletionAllowed`, and `CutoverAllowed` stay false. Do not delete PHP.
- There is no CI on this repository. Run the full `dotnet test` suite locally with `ECOMAE_LOCAL_MARIADB_E2E_DSN` set, and post the counts in the PR.
- Do not merge your own pull requests or deploy. The owner merges and runs the CloudPanel deploy.

## Things that tripped up this deploy

- A route handler in `StorefrontPhpAjaxEndpoints` that takes two services the host has not registered makes every route in that map fail with HTTP 500. GET/POST routes cannot infer a body. Mark service parameters `[FromServices]`. This was fixed in #1970.
- The 2026-10-07 deploy ran with `ECOMAE_EMERGENCY_PUBLISH=1`, which skips the server-side tests. Do not rely on server tests having run.
- Local testing details are in `.agents/skills/testing-erp-aspnet-local/SKILL.md`, which now describes the transfer posting.
