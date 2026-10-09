# Note for Devin — ERP restart, 2026-10-09

The owner is giving ERP back to Devin. Cursor keeps the storefront, Control Panel, marketing, BOS and the other non-ERP areas. This note says where ERP stands on `main` (`4b75fc103`), what Cursor changed in ERP since your last merged PR (#2026), and what to do first. It replaces nothing in `NOTE_FOR_DEVIN_2026-10-07.md`; read that one too.

## Status in one paragraph

ERP acceptance is 0 of 15 processes (`ERP_COMPLETION_DIRECTIVE.md`, completion board). The weighted migration headline is about 20.4%. Cursor did not start roadmap step 7 (ERP) as a work stream: the step 7 intake (reconcile your plan with the roadmap) is written down but not done, and no ERP wave was taken over. The ERP code Cursor touched since #2026 came from fixes found in the 2026-10-07 audit, from storefront checkout and payment side effects that end in ERP, and from two owner-approved enhancements from your PR #8.

## What Cursor changed in ERP since #2026

All of it is merged on `main`. Each item has tests in the full suite.

Accounting fixes (from the ownership audit, PR #2030):

1. `973c4ebeb` `ErpGlPostingService.Validate` rounds lines to two decimals before summing and needs debit = credit exactly. Build journal lines from amounts already rounded with `ErpTaxAmountCalculator.Round2`, or the journal is refused.
2. `d44718540` A cash entry with `counterparty_type = 'internal'` (transfer leg) posts against system account `1090 Cash in transit`, not revenue or expense. `1090` is seeded and added on first transfer for existing tenants.

Checkout, payment and e-invoice side effects that write ERP data (storefront step 2 work):

3. `7246c397e`, `36696b542` Checkout sends the supplier LPO per warehouse and bootstraps the ERP sales order and per-supplier POs; process-flow order/PO case sync at checkout, PO moves and invoicing, like PHP.
4. `44dd15e30` Order fulfillment bootstrap runs PHP's fulfillment schema-ensure and returns a typed payload.
5. `c564b6361`, `2c2ba8c47` Order e-invoice creation is split into build and save on a given connection; a draft keeps its supply category and can be re-validated with another buyer.
6. `a2d4f94ba`, `783e258f5` Document control template render with forced seller sync on a given connection (`epc_dc_render_template`); document control print uses the ERP user access engine (`epc_erp_user_can_access`).
7. `068c82f53`, `4d63c8db5` The `pay_for_order` protocol engine (manager, customer, payment system) creates advance VAT (`epc_uae_vat_advance` with `einvoice_document_id`) like PHP.
8. `b8b343552` Every tax-invoice save records sale demand (`epc_erp_inventory_record_sale_demand`).
9. `c563415c3`, `9b73fc864` E-invoice schema, buyer profile and customer VAT type sync ported from PHP; the schema DDL runs once per connection so registration stays atomic.

Owner-approved enhancements from your PR #8 (roadmap step 7, "Devin work that never reached main"):

10. `780942de7`, `265ac9bb4` PIM custom attributes (`epc_pim_fields`, `epc_pim_field_options`, `epc_pim_item_values`): "PIM attributes" tab on `/erp/product-info-app`, fields on the create-item forms, values on the item view.
11. `bb6347c33`, `39d2bec57` Syncron inventory policy (`epc_erp_inv_policies`, `epc_erp_inv_demand_forecast`, `epc_erp_inv_service_levels`) on `/erp/syncron-app`, listed in the ERP top nav under inventory management. The fixes made on top of PR #8 are listed in `ASP_NET_COMPLETION_ROADMAP.md`, step 7.

Menus and plan (no ERP logic):

12. `ec101fa54`, `4e598e9ce` Ported ERP pages that were missing from the ERP top menu are placed; the broken multi-entity tab is gone; one top-menu entry per destination, pinned by `TopMenuIntegrityTests`.
13. `7a6b40677` The UI/UX bar: CP and ERP pages close only when they have PHP's functions and are better than PHP (`ASPNET_MIGRATION_TRACKER.md`, "The UX bar for every CP and ERP page").

To see the code: `git log --oneline --no-merges debe2654e..main -- aspnet/src/EcomAE.Platform/Erp`.

## Still open from the 2026-10-07 audit

Re-checked on `main` today. None is fixed.

1. `TryPostCashEntryAsync` swallows GL failures, so a cash entry can exist with `gl_journal_id = 0`.
2. `ErpIntegrityService` checks orphans only: no unbalanced-journal check, no sub-ledger against control account check (AR 1100, AP 2000, cash 1000/1010).
3. `ErpRtlPosSaleWriteService` posts no journal and no stock movement, and uses `double` for quantity, price and discounts.
4. Cash, journal and transfer vouchers are not gap-free after a failed write.
5. Seven write services have no test: `ErpOpeningPostBatchWriteService`, `ErpPettyCashWriteService`, `ErpPmListingSaveWriteService`, `ErpPrjaRecognitionWriteService`, `ErpQmOrderCreateWriteService`, `ErpRfqResponseWriteService`, `ErpRtlPosSaleWriteService`. (`ErpAmlComplianceWriteService` now has `ErpAmlComplianceWriteServiceTests`.)
6. Floating-point money in `ErpOrderFulfillmentWriteService`, `ErpRtlPosSaleWriteService`, `ErpPfDemoSyncWriteService` and `ErpOplPlanningWriteService`.

## Suggested order

1. Do the step 7 intake first and commit it (`ASP_NET_COMPLETION_ROADMAP.md`, "Step 7"): re-derive the ERP percentages from the artifacts, set the board owner to Devin, place the six findings into your waves, and list the owner blockers still open (a second legitimate tenant DB for isolation, a production backup and restore window, human visual acceptance, country compliance data).
2. Wave 1 foundation: findings 1, 2 and 4, then the untested services that change reported figures (opening balances, project revenue recognition).
3. Order-to-Cash, then Procure-to-Pay, then Inventory/WMS (POS into ledger and stock is finding 3), then Finance, then the rest with Jewellery and Fit-Out, per `ERP_COMPLETION_DIRECTIVE.md`.
4. Port `devin/1790743008-tenant-install-control-plane` (`4139c40c4`) with tests when you reach tenant setup; it is security hardening.
5. Every page you finish must pass the UX bar, not only PHP parity.

## Where Cursor works now, to avoid clashes

Cursor works in `aspnet/src/EcomAE.Platform/Storefront/`, `Components/Pages/Storefront*.razor`, `Cp/` and the CP pages. Next up: the customer order card and line list (`my_order.php`, `my_orders_items.php`), cart, checkout confirm, the product page, then CP shop and CP control. Some of that writes into ERP through existing services (checkout, payments, e-invoices). If Cursor needs to change a file under `Erp/`, it will say so in the PR title and in `ASPNET_MIGRATION_TRACKER.md`. Please do the same for files under `Storefront/` and `Cp/`.

## Rules that stay in force

- One posting engine: journals only through `ErpGlPostingService.PostJournalAsync`.
- Write endpoints write only with `confirmWrites: true`; the dry run writes nothing, not even lazy DDL.
- Tests use throwaway `ecomae_cpw_*` schemas and drop them. Never use production data; never write to `docpart` or `ecomae.users`. After testing: `ecomae_cpw_%` = 0, `docpart.users` = 2, `ecomae.users` = 2.
- `ReadyForPhpRemoval`, `PhpSourceDeletionAllowed` and `CutoverAllowed` stay false. Do not delete PHP.
- No CI: run the full `dotnet test` suite locally with `ECOMAE_LOCAL_MARIADB_E2E_DSN` set and post the counts in each PR. Today: 5,873 of 5,873.
- Lower the gap ratchet when a gap closes: `python3 scripts/php_reference_gap_inventory.py --md docs/migration/inventory/PHP_REFERENCE_GAP_INVENTORY.md --max-gap N` (now 753). A name in a comment must not close a gap.
- Local ERP testing: `.agents/skills/testing-erp-aspnet-local/SKILL.md`.
