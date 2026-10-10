# Cursor non-ERP progress report (2026-10-09)

Audience: Devin (ERP owner) and the project owner. Scope: everything Cursor owns, which is everything except ERP (storefront, Control Panel, BOS, CRM, marketing, tenants). ERP is Devin's.

Sources: `docs/migration/ASPNET_MIGRATION_TRACKER.md`, `docs/migration/inventory/PHP_REFERENCE_GAP_INVENTORY.md` (regenerated today by `scripts/php_reference_gap_inventory.py`), and the PR history. Numbers below are measured, not estimated.

## 1. Headline (read this first)

- Weighted migration headline: still about **20.4% done / 79.6% pending** (Phase A 24/24, B-F open). Page-level slices do not move it.
- Accepted ERP processes: **0/15**. Formal interactive acceptance for CP, ERP, BOS and storefront: **0**. Nothing in this report is production acceptance.
- PHP file gap (files that nothing in ASP.NET references): **876 on 2026-10-07 to 479 today** (223,787 lines still unreferenced). Functions not named anywhere in ASP.NET: **8,152 to 6,606 of 9,870**.
- "Mentioned" in the inventory is a lead, not parity. Parity is claimed only where a PHP 8.3 runtime golden exists, and each tracker checkpoint says what is and is not golden-covered.
- Last verified state: PlanQ1Hull (CP social login). Hull 4/4, area functionality 10/10, PlanQ1 **201 / 201**, full platform suite **6169 / 6169**. Area runner: auth 151, storefront commerce 123, CP/BOS 167, tenants/jobs/social 27. Production update paste is `docs/migration/PRODUCTION_UPDATE_PASTE.md` (from `main` only). 2026-10-10 CloudPanel publish of `b7f1554bf` was healthy (`/health` `/ready` 200); smoke capture blocked on missing keys — do not invent them. Throwaway test schemas left over: 0. Production counts unchanged (`docpart.users` 2, `ecomae.users` 2, `docpart.sessions` 73).
- Method for every slice: read the PHP, build a PHP 8.3 harness that runs the real script on a throwaway MariaDB schema, record a golden, make ASP.NET equal it, document intentional deviations (usually security hardening), run the full suite, update tracker and inventory ratchet, open a PR.
- Executable next queue (not a progress narrative): `docs/migration/CURSOR_NON_ERP_NEXT_PLAN.md`. Refresh buckets with `scripts/php_non_erp_gap_buckets.py`.
- Owner clarification 2026-10-10: Cursor owns UI/UX, design, presentation, graphics, and functionality testing on storefront / CP / BOS / CRM / marketing / tenants. Devin owns the same concerns on ERP finance screens and the 15 ERP processes. Overall structure is shared (one SoR / one auth / one workflow). About 1000 tenants (site-only, ERP-only, mixed); some have 1000+ users. Each tenant owns its users, profiles, sessions, credentials, and documents. Super-CP may pick a tenant; a tenant operator never sees another tenant.

## 2. What Cursor built (non-ERP), by area

Storefront customer pages and ajax (PHP-golden verified unless noted):
- Login: password sign-in page, e-mail code sign-in send and verify (with provisioning and session), six-box code modal, admin login rate limit, bcrypt upgrade, password reset pages, contact confirmation, login code, `send_notify.php`.
- Registration: form render and post, captcha, user agreement module, registration fields, KYC documents, e-invoice buyer profiles, contact uniqueness check, social sign-in buttons, country list, page access checks (`check_user_access.php`).
- Profile: profile page with currency change request, profile edit page and save, trade accounts and storefront currencies (`epc_customer_trade.php`, `epc_currency.php`).
- Catalogue and product: product page (`printProduct_Info.php`, `product_page_for_customer.php`), multi-office offers and add-to-cart script (`common_add_to_basket.php`), customer offices (`get_customer_offices.php`), PIM custom attributes and Syncron inventory policy (owner-accepted enhancements), **catalogue product count `ajax_get_products_count.php` (PR #2069, open)**.
- Cart and checkout: cart, bottom-panel cart refresh, checkout login offer, checkout confirmation, quotes page, guest order page, supplier PO and LPO e-mails at checkout, staff and customer order e-mails.
- Customer orders: order list, order card, order line list, shared order-status/office reference loader (`orders_background.php`).
- Order side effects: process-flow case sync, order and line status protocol, return requests and returns pages, order print (receipt and UAE tax invoice), Document Control print, payment method picker, order payments with UAE gateway stubs, SMS (14 operators) and WhatsApp notification fan-out.
- Public free-tools API and four small ajax/API gaps.

Control Panel and shell:
- Every `cp/content` ajax endpoint has an ASP.NET route (referenced in the inventory: cp-ajax 108 of 110, ajax 89 of 91, api 28 of 29, cron 10 of 11; a reference is a lead, not parity).
- CP, ERP and BOC top menus fixed (ported ERP pages missing from the ERP top menu were added), eight PHP CP redirect pages, forty PHP CSS/JS wrapper files, CP script-text-leak fix, language-prefix preservation on ported users pages.

Hardening applied across these slices: raw SQL values and reflected input are context-encoded or parameterised (documented per checkpoint); GET pages validate sessions without minting guest sessions.

## 3. What is pending, outside ERP (Cursor responsibility)

Gap files from the inventory (files nothing in ASP.NET references), grouped by tracker plan step. ERP is listed last for contrast only.

| Plan step | Area | Gap files | Lines |
|---|---|---:|---:|
| 1 | Storefront: catalogue (`content/shop/catalogue` + `modules/shop/catalogue`) | 30 | 8,978 |
| 1 | Storefront: modules (other `modules/*`) | 18 | 3,446 |
| 1 | Storefront: other shop (remaining `content/shop/*` except docpart, catalogue, finance, price_engine) | 17 | 6,022 |
| 1 | Storefront: parts/docpart (`content/shop/docpart`) | 25 | 33,260 |
| 1 | Storefront: templates `expan` / `modex` / `limo` | 3 | 2,689 |
| 1 | Storefront: users/plugins (`content/users`, `plugins`) | 3 | 2,192 |
| 3 | CP other (users, requests, 2FA/auth plugins, CP modules, leftover `cp/*`) | 12 | 3,578 |
| 3 | CP shop core (`order_process`, catalogue product, `prices_upload`) | 27 | 12,897 |
| 3 | CP shop smaller (rest of `cp/content/shop` except finance) | 23 | 7,804 |
| 4 | CP control/portal (`cp/content/control`, including portal) | 40 | 10,296 |
| 5 | Marketing, BOS and industries (everything else non-ERP: `content/general_pages`, industry templates, social, OEM API, cron, deploy) | 93 | 48,643 |
| 6 | Price engine (`content/shop/price_engine` only; `epc_auto_price_engine.php` is 6,768 lines) | 18 | 16,113 |
| 7 | **ERP (Devin): finance libraries and CP finance pages** | 166 | 66,191 |
| 8 | Core/root (`core/dp_*.php`, root PHP, `lib/DocpartMailer`) | 4 | 1,678 |
| | **Total (measured now)** | **479** | **223,787** |

This table is generated by `scripts/php_non_erp_gap_buckets.py` from `/tmp/gap_inv.json` and **sums to the inventory**. Non-ERP **313 / 157,596**; ERP finance (Devin, unchanged) **166 / 66,191**. Biggest remaining non-ERP blocks: `content/general_pages` and `content/shop/docpart`. The “unnamed functions” count is unmentioned identifiers (PHP already named them); ready-to-build twins are in `docs/migration/inventory/PHP_UNMENTIONED_FUNCTIONS.md` (59 ≤200-line non-ERP PHP functions still waiting). Storefront `orders_background.php` stays a gap on purpose (CP helper of the same basename is not ported). Tenant PDO, BOS login, and CP social login are closed; remaining ready ≤200 rows are still mostly Q2/Q3. `part_search_page.php` stays open (user kernel + warehouse + article-match + sibling pages).

Beyond gap files, these are open for every surface regardless of file counts:
- Same-to-same PHP vs ASP.NET dual samples per tenant host, human acceptance, and the three combined browser regression rounds.
- CP: generated menu to page to write to permission/audit to presentation, with tenant and Super-CP browser evidence. CP and ERP UI/UX must be better than the PHP reference (owner requirement, 2026-10-08), not just equal.
- BOS and CRM as experiences over shared ERP services: Customer 360, Supplier 360, approvals, KPIs, lead-to-quotation flow handing orders to ERP. Role workspaces for CEO, CFO, Sales, Purchasing, Operations, Management are not accepted yet.
- Marketing, industry and demo hosts: forms, provisioning writes, expiry/restore, SEO/sitemap parity, brand-host probes.
- Tenant CP: isolation, country profiles, rollback, host-by-host shadow approval. On-prem and hybrid registration, sync and backup recovery.
- Release evidence: nothing here is assumed live. Each merged release still needs deployment from `main`, `/health` and `/ready`, exact-route probes and rollback verification. PHP removal stays prohibited until `/migration/php-decommission-readiness` is ready and the release owner approves.

## 4. In progress right now

- PlanQ1Hull (this slice): CP social login (`epc_auth_social.php`) against PHP 8.3 goldens, plus `NonErpAreaFunctionalityTests` across auth / CP / BOS / tenants / storefront. Leftover auth-common / OTP / buttons stay injected. CloudPanel 2026-10-10 publish of `main` `b7f1554bf` is live; this branch is not. Finance-path files stay Devin.
- Remaining 313 non-ERP files cannot close in one leftover. Ready ≤200 rows are still Q2/Q3 (industry templates, `*_h` guides, APE, OEM catalog, `printProducts*`). Those are not closed by mention-only.
- After those named helpers: `printProducts.php` / `printProducts_2.php` shells once the catalogue list kernel is in, then `part_search_page.php` and the parts agent.

## 5. ERP handoff notes for Devin

- In the last session (2026-10-09, all slices above) **no ERP service or ERP page file was edited.** ERP is untouched by this work.
- ERP-adjacent changes Cursor made earlier (2026-10-07 to 08) are already listed in `docs/migration/NOTE_FOR_DEVIN_2026-10-07.md` and `NOTE_FOR_DEVIN_2026-10-09.md`: order e-invoice creation split into build and save on a given connection (`ErpInvoiceFromOrderWriteService`), sale-demand capture on every tax-invoice save (`ErpSalesInvoiceWriteService`), the Syncron policy and PIM custom fields, and ported ERP pages added to the ERP top menu. Please review those before changing the same services.
- Storefront code that calls into finance data: order payment (`ajax_create_operation`) reads and writes `shop_users_accounting`; order print and Document Control print read ERP context. If you change those tables or services, run the storefront suites too (`StorefrontPhpShopTests`, `ShopOrderProtocolTests`, `StorefrontOrderPrintTests`).
- Housekeeping: the order-payment test fixture was missing the `order` column on `shop_orders_items_statuses_ref` and failed on `main` after PR #2068. Fixed on PR #2069.
- ERP gap still on your side: 166 finance PHP files (about 66k lines), the write side of the 118 mapped ERP tabs, the ERP portal half of `epc_erp_access.php`, and the deferred findings in the ERP restart note. ERP accepted processes remain 0/15.
- Owner split for UI/UX, design, presentation, graphics, and ERP functionality testing is yours for finance screens and the 15 processes. Cursor does not take ERP chrome. Tenant isolation (site-only / ERP-only / mixed, including 1000-user tenants) still means ERP rows stay on that tenant ledger only.

## 6. Open questions that still need the owner

1. Syncron: no `epc_erp_syncron_policy.php` in the repo. Send the original policy or confirm the twin of `epc_erp_scm.php` plus `epc_erp_order_fulfillment.php`.
2. ERP-only staff role: PHP RBAC allows it, ASP.NET auth grants CP and ERP together. Confirm wanted.
3. Jewellery legacy schema: read-only schema or approved transaction evidence is needed before treating proposed fields as legacy columns.
4. Fit-out pack: Phase-1 scope is pending implementation and live tenant-database validation.
