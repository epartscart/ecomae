# Note for Devin — ERP wakeup, 2026-10-11

Read this first, then `NOTE_FOR_DEVIN_2026-10-09.md` and `NOTE_FOR_DEVIN_2026-10-07.md`. The 9 Oct note is still the ERP code handoff. This note is the wakeup status only.

## Ownership (unchanged)

- **Devin:** ERP system of record, ERP finance UI, the 15 ERP processes, finance PHP under `content/shop/finance` and `cp/content/shop/finance`.
- **Cursor:** storefront, CP, BOS, CRM, marketing, tenants. Cursor does not take ERP chrome.

ERP acceptance is still **0 / 15**. The weighted headline is still about **20.4%**. That is not acceptance.

## What Cursor did since 9 Oct

Cursor ran leftover non-ERP twins on `cursor/small-gaps-storefront-done-5dbd` (stacked leftover queue). Latest measured inventory after PlanQ1Howe:

| | Files | Lines |
|---|---:|---:|
| Non-ERP still open (Cursor) | **245** | **132,211** |
| ERP finance still open (Devin) | **166** | **66,191** |
| Total PHP gap | **411** | **198,402** |

Unmentioned functions: **6,031** of 9,870. Ready ≤200-line non-ERP rows: 46, mostly Q2/Q3 (catalogue list, APE, `*_h` guides). Cursor is not closing those by mention-only.

**No file under `aspnet/src/EcomAE.Platform/Erp/` was edited on this leftover stream.** Finance PHP was not edited. Do not treat leftover BOC wrappers (channel / command-center / vendor / warehouse / audit-log) as ERP process work. They are Super-CP shells; the finance collectors stay yours.

Production-like local counts after Cursor leftovers: `docpart.users` 2, `ecomae.users` 2, leftover `ecomae_cpw_%` = 0. Keep that.

CloudPanel `main` `b7f1554bf` (2026-10-10) was healthy (`/health` `/ready` 200). This leftover branch is not production.

## Still open from the 9 Oct audit (none of these were fixed by Cursor)

1. `TryPostCashEntryAsync` can leave a cash entry with `gl_journal_id = 0`.
2. `ErpIntegrityService` still has no unbalanced-journal or sub-ledger-vs-control check.
3. `ErpRtlPosSaleWriteService` posts no journal and no stock movement; `double` money.
4. Cash / journal / transfer vouchers are not gap-free after a failed write.
5. Seven write services still have no test (opening post, petty cash, PM listing, PRJA recognition, QM order, RFQ response, RTL POS).
6. Floating-point money in fulfillment, RTL POS, PF demo sync, OPL planning.

## First hour when you wake

1. Re-read `ERP_COMPLETION_DIRECTIVE.md` and do the step 7 intake if it is still uncommitted (`ASP_NET_COMPLETION_ROADMAP.md`).
2. Do not rebase onto or edit Cursor leftover branches. Work ERP from `main` (or your own ERP branch).
3. Wave 1 still starts with findings 1, 2 and 4, then untested services that change reported figures.
4. Local ERP testing: `.agents/skills/testing-erp-aspnet-local/SKILL.md`. Throwaway `ecomae_cpw_*` only. Writes stay dry-run unless `confirmWrites: true`.
5. If you change tables or services that storefront already calls (`shop_users_accounting`, e-invoice / document-control / `pay_for_order` / sale-demand), run `StorefrontPhpShopTests`, `ShopOrderProtocolTests`, `StorefrontOrderPrintTests` as well as the ERP suite.

## Clash rule

- Cursor stays in `Storefront/`, CP, BOS, leftover `PhpPlanQ1*` wrappers, marketing, tenants.
- Devin stays in `Erp/`, finance PHP, ERP razor pages, the 15 processes.
- If either side must touch the other tree, say so in the PR title and in `ASPNET_MIGRATION_TRACKER.md`.

## Owner blockers still open (same as 9 Oct)

Second legitimate tenant DB for isolation, production backup/restore window, human visual acceptance, country compliance data. Syncron original policy file is still missing from the repo (`epc_erp_syncron_policy.php`). ERP-only staff role in PHP vs CP+ERP together in ASP.NET is still unconfirmed.

## Rules that stay in force

- One posting engine: `ErpGlPostingService.PostJournalAsync`.
- `ReadyForPhpRemoval`, `PhpSourceDeletionAllowed`, `CutoverAllowed` stay false. Do not delete PHP.
- GET does not mint a guest or impersonation session.
- Tenant isolation: site-only / ERP-only / mixed. ERP rows stay on that tenant ledger only.
