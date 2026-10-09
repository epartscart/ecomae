# Cursor non-ERP progress report (2026-10-09)

Audience: Devin (ERP owner) and the project owner. Scope: everything Cursor owns, which is everything except ERP (storefront, Control Panel, BOS, CRM, marketing, tenants). ERP is Devin's.

Sources: `docs/migration/ASPNET_MIGRATION_TRACKER.md`, `docs/migration/inventory/PHP_REFERENCE_GAP_INVENTORY.md` (regenerated today by `scripts/php_reference_gap_inventory.py`), and the PR history. Numbers below are measured, not estimated.

## 1. Headline (read this first)

- Weighted migration headline: still about **20.4% done / 79.6% pending** (Phase A 24/24, B-F open). Page-level slices do not move it.
- Accepted ERP processes: **0/15**. Formal interactive acceptance for CP, ERP, BOS and storefront: **0**. Nothing in this report is production acceptance.
- PHP file gap (files that nothing in ASP.NET references): **876 on 2026-10-07 to 656 today** (271,793 lines still unreferenced). Functions not named anywhere in ASP.NET: **8,152 to 7,851 of 9,870**.
- "Mentioned" in the inventory is a lead, not parity. Parity is claimed only where a PHP 8.3 runtime golden exists, and each tracker checkpoint says what is and is not golden-covered.
- Last verified state: full ASP.NET suite **5,918 / 5,918 passing**, 0 build warnings, 0 errors. Throwaway test schemas left over: 0. Production counts unchanged (`docpart.users` 2, `ecomae.users` 2, `docpart.sessions` 73).
- Method for every slice: read the PHP, build a PHP 8.3 harness that runs the real script on a throwaway MariaDB schema, record a golden, make ASP.NET equal it, document intentional deviations (usually security hardening), run the full suite, update tracker and inventory ratchet, open a PR.

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
| 1 | Storefront: parts search and `content/shop/docpart` (part search pages, parts agent, demand intelligence, garage, fitment, crosses, multivendor/commerce price ingest) | 44 | 37,164 |
| 1 | Storefront: catalogue (`printProducts.php`, `printProducts_2.php`, `printProductBlock`, compare, bookmarks, SKU media, text search, tree lists, `modules/shop/catalogue`) | 43 | 11,400 |
| 1 | Storefront: other shop (tenant hub, usefull, POS, marketing, channels, workshop, customer mgmt, crm, geo, logistics, print docs) | 35 | 8,782 |
| 1 | Storefront: modules (login, menu, bread crumbs, slider, news, lang, cart, balance, search string, geo, ucats) | 18 | 3,156 |
| 1 | Storefront: users/login/plugins (`dp_user.php`, `epc_registration_enhanced.php` render half, `profileform.php`, session security, plugins) | 11 | 2,969 |
| 1 | Storefront: front templates `expan`, `modex`, `limo` | 3 | 2,689 |
| 1 | Storefront: `content/shop/order_process/orders_background.php`. Already ported (`StorefrontOrdersBackground`, golden-verified) but still counted as a gap on purpose: the CP helper of the same name is not ported and a path mention would falsely close both | 1 | 56 |
| 3 | CP shop core: orders (`order_card.php`, `orders_items.php` and modals, guides), catalogue `product.php`, price upload page bodies | 50 | 14,700 |
| 3 | CP shop smaller sets (logistics, crosses, data transfer, document control, channels, marketing, tenant hub, POS, payments, demand countries, synonyms, accessories, statistics) | 46 | 8,755 |
| 3 | CP other (users, lang, packs, file manager, requests, 2FA/auth plugins, CP modules) | 28 | 4,551 |
| 4 | CP control and portal (auto-price shell, social hub, auth settings, tax toolkit, industry kit, marketing broadcast, visual page editor, fleet dashboard, governance, POS tenant mgmt, BOC panels, version control) | 58 | 11,418 |
| 5 | Marketing, platform, BOS and industries (`content/general_pages`, ecomae.com pages and router, free tools, portal demo, web tracker, auth/MFA/SMTP/OAuth, API v1, Power BI, BOS unified, 28 industry templates) | 196 | 78,679 |
| 6 | Price engine (`epc_auto_price_engine.php` 6,768 lines, discovery adapters; must stay on the existing importer) | 23 | 16,789 |
| 8 | Core and root (`core/dp_*.php`, root CP includes, mailer, license manager, eparts catalogue) | 27 | 8,186 |
| 7 | **ERP (Devin): finance libraries and CP finance pages** | 166 | 66,191 |
| | **Total (measured now)** | **656** | **271,793** |

Per-area rows are the earlier grouping and no longer sum to the total. Measured current inventory: **656 gap files / 271,793 lines**; non-ERP **490 / 205,602**; ERP finance (Devin, unchanged) **166 / 66,191**. Biggest remaining non-ERP blocks: `content/general_pages` and `content/shop/docpart`.

Beyond gap files, these are open for every surface regardless of file counts:
- Same-to-same PHP vs ASP.NET dual samples per tenant host, human acceptance, and the three combined browser regression rounds.
- CP: generated menu to page to write to permission/audit to presentation, with tenant and Super-CP browser evidence. CP and ERP UI/UX must be better than the PHP reference (owner requirement, 2026-10-08), not just equal.
- BOS and CRM as experiences over shared ERP services: Customer 360, Supplier 360, approvals, KPIs, lead-to-quotation flow handing orders to ERP. Role workspaces for CEO, CFO, Sales, Purchasing, Operations, Management are not accepted yet.
- Marketing, industry and demo hosts: forms, provisioning writes, expiry/restore, SEO/sitemap parity, brand-host probes.
- Tenant CP: isolation, country profiles, rollback, host-by-host shadow approval. On-prem and hybrid registration, sync and backup recovery.
- Release evidence: nothing here is assumed live. Each merged release still needs deployment from `main`, `/health` and `/ready`, exact-route probes and rollback verification. PHP removal stays prohibited until `/migration/php-decommission-readiness` is ready and the release owner approves.

## 4. In progress right now

- Open stacked PRs: #2069 catalogue count, #2070 catalogue list/page ids, #2071 small storefront fragments plus CP eval-safe wrappers (ratchet 717 → 695 → 656).
- Next closable ≤60-line non-ERP files: 46 remain (about 1.8k lines). Skip search tabs, `printProducts*`, `side_menu`, page-builder render, and BOC console pages until their parent kernels land.
- After the small files: product block markup (`printProductBlock`), then `printProducts.php` / `printProducts_2.php` shells, then `part_search_page.php` and the parts agent.

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
