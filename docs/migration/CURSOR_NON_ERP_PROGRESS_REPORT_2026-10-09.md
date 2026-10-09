# Cursor non-ERP progress report (2026-10-09)

Audience: Devin (ERP owner) and the project owner. Scope: everything Cursor owns, which is everything except ERP (storefront, Control Panel, BOS, CRM, marketing, tenants). ERP is Devin's.

Sources: `docs/migration/ASPNET_MIGRATION_TRACKER.md`, `docs/migration/inventory/PHP_REFERENCE_GAP_INVENTORY.md` (regenerated today by `scripts/php_reference_gap_inventory.py`), and the PR history. Numbers below are measured, not estimated.

## 1. Headline (read this first)

- Weighted migration headline: still about **20.4% done / 79.6% pending** (Phase A 24/24, B-F open). Page-level slices do not move it.
- Accepted ERP processes: **0/15**. Formal interactive acceptance for CP, ERP, BOS and storefront: **0**. Nothing in this report is production acceptance.
- PHP file gap (files that nothing in ASP.NET references): **876 on 2026-10-07 to 539 today** (249,732 lines still unreferenced). Functions not named anywhere in ASP.NET: **8,152 to 7,306 of 9,870**.
- "Mentioned" in the inventory is a lead, not parity. Parity is claimed only where a PHP 8.3 runtime golden exists, and each tracker checkpoint says what is and is not golden-covered.
- Last verified state: PlanQ1Rest plus prior PlanQ1 suites **61 / 61 passing**. Full platform suite **6019 / 6019**. Throwaway test schemas left over: 0. Production counts unchanged (`docpart.users` 2, `ecomae.users` 2, `docpart.sessions` 73).
- Method for every slice: read the PHP, build a PHP 8.3 harness that runs the real script on a throwaway MariaDB schema, record a golden, make ASP.NET equal it, document intentional deviations (usually security hardening), run the full suite, update tracker and inventory ratchet, open a PR.
- Executable next queue (not a progress narrative): `docs/migration/CURSOR_NON_ERP_NEXT_PLAN.md`. Refresh buckets with `scripts/php_non_erp_gap_buckets.py`.

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
| 1 | Storefront: catalogue (`content/shop/catalogue` + `modules/shop/catalogue`) | 35 | 10,764 |
| 1 | Storefront: modules (other `modules/*`) | 18 | 3,446 |
| 1 | Storefront: other shop (remaining `content/shop/*` except docpart, catalogue, finance, price_engine) | 23 | 7,989 |
| 1 | Storefront: parts/docpart (`content/shop/docpart`) | 31 | 35,605 |
| 1 | Storefront: templates `expan` / `modex` / `limo` | 3 | 2,689 |
| 1 | Storefront: users/plugins (`content/users`, `plugins`) | 3 | 2,192 |
| 3 | CP other (users, requests, 2FA/auth plugins, CP modules, leftover `cp/*`) | 12 | 3,578 |
| 3 | CP shop core (`order_process`, catalogue product, `prices_upload`) | 31 | 13,857 |
| 3 | CP shop smaller (rest of `cp/content/shop` except finance) | 24 | 8,039 |
| 4 | CP control/portal (`cp/content/control`, including portal) | 41 | 10,505 |
| 5 | Marketing, BOS and industries (everything else non-ERP: `content/general_pages`, industry templates, social, OEM API, cron, deploy) | 128 | 65,472 |
| 6 | Price engine (`content/shop/price_engine` only; `epc_auto_price_engine.php` is 6,768 lines) | 19 | 16,422 |
| 7 | **ERP (Devin): finance libraries and CP finance pages** | 166 | 66,191 |
| 8 | Core/root (`core/dp_*.php`, root PHP, `lib/DocpartMailer`) | 5 | 2,983 |
| | **Total (measured now)** | **539** | **249,732** |

This table is generated by `scripts/php_non_erp_gap_buckets.py` from `/tmp/gap_inv.json` and **sums to the inventory**. Non-ERP **373 / 183,541**; ERP finance (Devin, unchanged) **166 / 66,191**. Biggest remaining non-ERP blocks: `content/general_pages` and `content/shop/docpart`. The “unnamed functions” count is unmentioned identifiers (PHP already named them); ready-to-build twins are in `docs/migration/inventory/PHP_UNMENTIONED_FUNCTIONS.md` (93 ≤200-line non-ERP PHP functions still waiting). Storefront `orders_background.php` stays a gap on purpose (CP helper of the same basename is not ported).

Beyond gap files, these are open for every surface regardless of file counts:
- Same-to-same PHP vs ASP.NET dual samples per tenant host, human acceptance, and the three combined browser regression rounds.
- CP: generated menu to page to write to permission/audit to presentation, with tenant and Super-CP browser evidence. CP and ERP UI/UX must be better than the PHP reference (owner requirement, 2026-10-08), not just equal.
- BOS and CRM as experiences over shared ERP services: Customer 360, Supplier 360, approvals, KPIs, lead-to-quotation flow handing orders to ERP. Role workspaces for CEO, CFO, Sales, Purchasing, Operations, Management are not accepted yet.
- Marketing, industry and demo hosts: forms, provisioning writes, expiry/restore, SEO/sitemap parity, brand-host probes.
- Tenant CP: isolation, country profiles, rollback, host-by-host shadow approval. On-prem and hybrid registration, sync and backup recovery.
- Release evidence: nothing here is assumed live. Each merged release still needs deployment from `main`, `/health` and `/ready`, exact-route probes and rollback verification. PHP removal stays prohibited until `/migration/php-decommission-readiness` is ready and the release owner approves.

## 4. In progress right now

- Open stacked PRs: #2069 catalogue count, #2070 catalogue list/page ids, #2071 small storefront fragments through PlanQ1Rest (ratchet 717 → … → 548 → 544 → **539**).
- PlanQ1Rest (this slice): five files — `epc_demand_country_iso.php`, `epc_storefront_industry_themes.php`, `epc_cp_brochure_topic_photos.php`, `epc_portal_theme_templates.php`, `epc_portal_storefront_packages.php`. Theme templates inject industry maps; package dump omits header/home/footer sibling paths. Industry templates stay skipped (`_base_template.php` parent). `addContentToDump` stays skipped (`DP_ContentRecord` dump page). SKU-media stays skipped (`epc_sku_media.php` parent). Core engine `dp_core.php` stays a gap (path concatenated in the license bundle helper).
- Next work is the remaining Q1 leftovers in `CURSOR_NON_ERP_NEXT_PLAN.md` (skip OEM `Functions.Common.php` and `epc_platform_jobs.php` until `epc_portal_tenant.php`). Search tabs, `printProducts*`, `side_menu`, page-builder render, BOC consoles, product-line href pages, `*_h` guide wrappers, APE adapters, logistics helpers and the marketing-broadcast panel stay skipped until their parents land.
- After those named helpers: `printProducts.php` / `printProducts_2.php` shells once the catalogue list kernel is in, then `part_search_page.php` and the parts agent.

## 5. ERP handoff notes for Devin

- In the last session (2026-10-09, all slices above) **no ERP service or ERP page file was edited.** ERP is untouched by this work.
- ERP-adjacent changes Cursor made earlier (2026-10-07 to 08) are already listed in `docs/migration/NOTE_FOR_DEVIN_2026-10-07.md` and `NOTE_FOR_DEVIN_2026-10-09.md`: order e-invoice creation split into build and save on a given connection (`ErpInvoiceFromOrderWriteService`), sale-demand capture on every tax-invoice save (`ErpSalesInvoiceWriteService`), the Syncron policy and PIM custom fields, and ported ERP pages added to the ERP top menu. Please review those before changing the same services.
- Storefront code that calls into finance data: order payment (`ajax_create_operation`) reads and writes `shop_users_accounting`; order print and Document Control print read ERP context. If you change those tables or services, run the storefront suites too (`StorefrontPhpShopTests`, `ShopOrderProtocolTests`, `StorefrontOrderPrintTests`).
- Housekeeping: the order-payment test fixture was missing the `order` column on `shop_orders_items_statuses_ref` and failed on `main` after PR #2068. Fixed on PR #2069.
- ERP gap still on your side: 166 finance PHP files (about 66k lines), the write side of the 118 mapped ERP tabs, the ERP portal half of `epc_erp_access.php`, and the deferred findings in the ERP restart note. ERP accepted processes remain 0/15.

## 6. Open questions that still need the owner

1. Syncron: no `epc_erp_syncron_policy.php` in the repo. Send the original policy or confirm the twin of `epc_erp_scm.php` plus `epc_erp_order_fulfillment.php`.
2. ERP-only staff role: PHP RBAC allows it, ASP.NET auth grants CP and ERP together. Confirm wanted.
3. Jewellery legacy schema: read-only schema or approved transaction evidence is needed before treating proposed fields as legacy columns.
4. Fit-out pack: Phase-1 scope is pending implementation and live tenant-database validation.
