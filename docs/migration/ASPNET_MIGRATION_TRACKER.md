# ASP.NET Core migration tracker (single source of pending work)

PHP reference (`/php-reference/*`) is the source of truth for layout, colours, controls, tables, menus,
permissions, guides and behaviour. A module is **done** only when it is a PHP twin (same tables, same
actions, same presentation) — digest/read-only shells do not count.

Update this file in every PR that moves an item. Status legend: `[x]` done (PR merged or open), `[~]` in progress,
`[ ]` pending. Gates (`MigrationGates`) stay closed until the test rounds pass:
`ReadyForPhpRemoval=false`, `PhpSourceDeletionAllowed=false`, `CutoverAllowed=false`, `AspNetInteractiveCompleteCount=0`.

Order of work (user sequence): **CP build → ERP build → storefront/others build → one combined test of all → regression → gates.**

Progress measurement (reported to the user on every completed step): `done / total` checklist items per phase and overall,
plus pending %. Weighting: Phase A 20 %, B 45 % (B-J 8 %, B-F 7 % inside), C 15 %, D 8 %, E 7 %, F 5 %.
Current: A 24/24 items (≈99 %) · B 0/21 · C 0/8 · D 0/6 · E 0/4 · F 0/2 → **overall ≈ 20.4 % done / 79.6 % pending**.
Latest ERP sub-slice: B0 navigation governance 4/4 controls landed (reconciliation, tenant deny flags,
explicit pack IDs, inspection projection); B5 manual GL journal posting, posted-journal reversal, and
closed-period guards, opened-journal line drill-down, PHP COA-backed account selectors, debit/credit balance summaries, and selectable posting dates are now live. The phase-level
percentage remains unchanged until a complete ERP capability gate is closed.
Legacy route-contract audit: CP shop/top-level maps resolve all 34/34 and 11/11 mapped destinations;
ERP PHP-tab mappings resolve 95/95 destinations and referenced `/erp/*-app` links resolve 224/224
non-AJAX pages. The `/cp/carts-app` legacy path now aliases the implemented carts twin; unresolved
scan entries are existing user/group routes or dedicated AJAX endpoints, not missing Razor pages.
The Parts Agent desk also no longer links back to legacy `/cp/shop/*` paths: price-list and catalogue
actions now target their implemented ASP.NET CP apps.

Conversation requirements audit (reconciled 2026-09-27):
- [x] CP and ERP menu sources remain PHP-authoritative; generated counts are not treated as proof of parity.
- [x] Tenant isolation, CSRF/RBAC, auditability, country-driven compliance, fit-out, jewellery, Python-sidecar,
      D365-style forms, document/report design, zero-downtime operations, on-premises/version support, and
      post-migration enterprise comparison are recorded below as standing requirements or phase work.
- [x] User sequencing is preserved: build CP → build ERP → build storefront/other areas → run combined testing.
- [~] Broad browser/E2E, functional, security, and PHP side-by-side testing remains intentionally deferred until
      the requested build phases are complete.
(CP dashboard twin split into 3 parts: parts 1–3 done — KPIs/chart, persisted `.eds-*` shortcuts, conditional insights + portal industry catalogue.
CP re-audit in progress: prices-edit rebuilt as the `prices_edit` twin — filter/search, profile site-price preview, paged table, inline edit, delete, search-delete;
print-docs rebuilt as the `print_doc_tuning` twin — document list, JSON `parameters_description` widgets (text/textarea/checkbox/image/profile), wholesaler office scope, live save;
SAO rebuilt as the `states_statuses_link` twin — state↔item-status grid with live save, robot queue view, SAO action palette;
data-transfer rebuilt as the `data_transfer` twin — catalogue XML/JSON export (PHP request fields, office/group scope, generated-file download)
and XML/JSON import (own-warehouse `interface_type=1` + per-user `users` JSON authorisation, clear modes 0/1/2 with explicit catalogue-clear confirmation);
CSV import completed as the `catalogue_csv_import` twin — end-category gate, 1-based column mapping for name/description/image/price/quantity plus every
category property with URL flags, skip-rows, Windows-1251/UTF-8 decoding, own-warehouse authorisation, delete-warehouse-rows / delete-category-products
(second confirmation) and per-row warnings;
KKT rebuilt as the online-cashier twin — `kkt_root_page` default settings for manual (type 2) and post-online-payment (type 1) checks with live save,
`devices.php` register list with wired-interface description, and the `checks.php` register: all 17 PHP filters, whitelisted sorting, paging,
expanded ordinary check lines/payments with order linkage and correction-check blocks (cause document, payment and six tax sums);
Tenant control center rebuilt as the `epc_tenant_control_center` twin — PHP tenant typing (commerce / demo / ERP-only demo / ERP-only) with the
PHP label and badge maps, PHP registry ordering, Listed/In registry/Accessible/Platform DB KPIs with demo-expiry access blocking, PHP admin-email
precedence (demo contact → `intro_json.admin_cp_email` → `operator_login_email` → `from_email`), PHP commerce host normalisation with storefront/CP
links and the ERP-only login URL, ERP pack column, and a masked password-state column; secrets (`db_password`, `operator_temp_password`) never leave
the service, and reveal/reset plus demo-access credential edits stay on the Classic twin;
API clients rebuilt as the `epc_api_clients_manage` twin — native `epc_api_clients` schema ensure, PHP hero/guide/curl block, create form
(product catalog / price_pro / both, label, contact email, clamped daily limit 1…1 000 000, catalog action scopes), PHP active-first listing with
key prefix / quota / scopes / status columns, and the full lifecycle: create, rotate, update, reset quota, revoke, activate. Keys are generated with
the PHP prefixes (`epc_catalog_`, `epc_pricepro_`), stored only as SHA-256 hash plus safe prefix, never selected back, and the plain key is handed to
the browser exactly once through a one-time in-memory token — never in the DB, the list payload or a URL;
Parts Agent rebuilt as the `parts_agent_chats` twin — PHP hero/toolbar, configuration card (enabled, domain, agent name, logo, subtitle, greeting,
system prompt, teaser, placeholder), stats strip (`epc_agent_cp_stats`), session filters (search over session/message/IP + date range),
`epc_parts_agent_session` list ordered by `updated_at DESC` with PHP 1–200 limit clamp and paging, `users`/`users_profiles` customer enrichment,
server-rendered transcript from `epc_parts_agent_message`, CSV export of the filtered set, and native `epc_agent_ensure_db_schema` /
`epc_agent_config_ensure_schema` DDL. Temp-file chat sync stays Classic — that folder belongs to the PHP host;
SMS/WhatsApp rebuilt as the `sms_turning` twin — `sms_api` catalogue with PHP `control_available` filter and `epc_%`-first ordering,
UAE/GCC/Pakistan guide block, current-operator/sender panel, grouped operator selector with description, and the dynamic parameter editor
(descriptor arrays and associative maps, text/number/select/checkbox/password widgets, sender fields prefilled with `+971567607011`),
saving field-by-field through the existing CSRF-gated activate endpoint — stored credentials are never echoed back and a blank secret keeps
the saved value. Actual sending stays under Communications;
Communications rebuilt as the `communications.php` + `ajax_test_notification.php` twin — SMTP readiness from all eight `config.php` fields,
From/host:port, active `sms_api` operator/handler/sender (PHP sender precedence), `debug_results` e-mail/SMS pills with stale (1 day) / old
(1 week) marking, admin-profile/session/From e-mail prefill, and a live test send: admin CP gate, confirm-write gate, `email`/`phone` type check,
`reg_fields` full-match contact validation, `test_email`/`test_phone` templates, SMTP or native SMS handler send (Unifonic/Etisalat/du/Pakistan
with UAE/PK MSISDN normalisation), `debug_results` persistence; passwords and SMS credentials never reach the browser.
Super CP info blocks rebuilt as the `epc_super_cp_info_blocks.php` twin — PHP hero/workspace intro/empty state, placement filter, PHP list
order, registry tenant dropdown, full-content editor (`?edit=` / legacy `?block_id=`), inline delete, native `epc_platform_info_blocks` schema ensure. Super CP communication rebuilt as the
`epc_super_cp_communication.php` twin — SMTP diagnostics, notification policy, task editor with `task_status` filter,
PHP task order, platform-user/tenant dropdowns, date due field, native comm/task schema ensure. Demo tenants rebuilt as the
`epc_demo_tenants_manage.php` twin — PHP hero/KPIs, demo table, +3d extend, convert to live, force delete with protected-DB guard.)

Standing rules that apply to every item below:
- Tenant isolation, per-tenant DB confidentiality, CSRF on every write, RBAC/ACL, audit log, degraded-shared-DB guard.
- Statutory logic (VAT/CT/e-invoice/payroll/labour law) is tenant-country-driven and effective-dated — UAE/FTA is one profile.
- ERP entry forms: Dynamics 365 F&O style — header/lines, action pane, field + cross-field validation, pre-validate → post,
  number sequences per document type, draft → confirmed → posted → reversed workflow, audit trail.
- Python sidecar (`pyapi`/`pyprices`) where technically better (forecasting, analytics, price-file ingest), behind a typed ASP.NET client.
- Zero downtime for tenants: rolling/blue-green releases, health-checked cutover, additive (expand/contract) migrations, persisted sessions.

---

## Phase A — Control Panel (CP)

### A1 CP module twins (PHP `cp/content/**`)
- [x] Currencies (#1486) · Order statuses, groups, config editor, languages, statistics (#1487)
- [x] Search tabs, manufacturer synonyms, crosses (#1489) · Warehouses/storages (#1490)
- [x] Offices, obtaining modes, geo tree, slider, additional texts, sitemap, menus (#1491/#1492)
- [x] Content pages manager/editor/tree (#1493) · Users manager/editor (#1494)
- [x] Product filters, quote requests, carts (#1495) · Notification settings/templates (#1496) · Orders list (#1497) — re-land #1498
- [x] Price configs (#1499) · Accessories marketplace (#1500) · Templates/plugins/modules (#1501)
- [x] VIN requests + VIN fields (#1502) · File manager (#1503) · Packs (#1504) · Bulk upload hub (#1505)
- [x] POS terminal (#1506) · Workshop desk (#1507) · Procurement desk (#1508) · Returns manager (#1509)
- [x] Price management (#1510) · CRM enterprise (#1511) · Prices-send (#1512)
- [x] Payment gateways hub — `payments_main.php` twin: `epc_payment_seed_all_gateways` catalogue (24 modern handlers, PHP names/
      descriptions/regions/parameters/demo values), dashboard/accounts/configure/legacy tabs, region sections, dynamic credential
      widgets from `shop_payment_systems.parameters`, individual `epc_payment_accounts` (platform/office/vendor, direct/connected/
      payout modes), `epc_payment_settlements` register, and all seven `ajax_payments.php` actions live behind admin+`confirmWrites`
- [x] Tenant feature registry + tenant SMTP — `epc_tenant_features.php` twin (Super-CP gate, `epc_portal_tenants` selector ordered by
      hostname with first-tenant default, catalogue checkbox matrix with PHP icons/labels, `tenant_registry` unsaveable, how-it-works
      panel, native `epc_integrations_ensure_schema` before `save_feature_flags`) and `epc_tenant_email_settings.php` presentation
      parity (platform-SMTP notice, activate-in-3-steps guide)
- [x] Mobile apps — `epc_mobile_apps.php` twin (admin-login warning, Super/Tenant CP badge, Integrations-hub action, Capacitor/PWA
      copy, full Android/iOS/PWA form with PHP placeholders and `api_base_url ?: storefrontUrl` / `deep_link_scheme ?: epartscart://`
      defaults, Android/iOS/connect publish guide and doc links); `save_mobile` merges `integrations_json.mobile` on the host-scoped
      `epc_portal_site_settings` row (host aliases first, lowest-id fallback) and adds the `integrations_json` column when missing
- [x] Super CP customer board — `epc_super_cp_customer_board.php` twin (`epc_scp_customer_board_search`): platform-registry search plus
      per-tenant scan of every live `epc_portal_tenants` database, `q` / `tenant` filters with 50-row pagination, PHP hero + workspace
      intro, Results / Platform / Tenants-scanned / Live-tenant KPIs, CRM + ERP + CP quick actions per row, PHP empty state; no password
      or credential column is ever selected and the board stays read/search only, as in PHP
- [x] Integrations Hub — `epc_integrations_hub.php` twin (`CpIntegrationsHubService` + `CpIntegrationsHubCatalog.BuildHubCards`):
      Super/Tenant brand and hero actions, Active / In catalog / Guides ready counts, tenant market label from
      `epc_price_settings.company_country_code` with the PHP UAE fallback and market notice, the six PHP categories with coloured
      cards, status and Super-CP pills, Configure or “Configured on ecomae.com”, PHP guide resolution, search + category chips
      served from the PHP `epc_integrations_hub_ui.js`, PHP empty state and three-step playbook; tenant rows follow
      `epc_tenant_feature_flags` and no integration secret is ever read or rendered
- [x] Integrations Guide — `epc_integrations_guide.php` twin (`CpIntegrationsGuideCatalog`, own `/cp/control/portal/epc_integrations_guide`
      route taken back from the generic guides hub): PHP operator handbook per catalog entry (summary, Activate steps, Tips, extra
      links) rendered in category order with the sticky contents list, Configure button on the Super/Tenant URL, Dedicated-guide button
      only when the resolved guide is not this page, tenant view drops super-only entries without a tenant URL, hides their Configure
      and the Super-only API documentation link
- [x] Super CP operator guide — `epc_super_cp_operator_guide.php` twin (`SuperCpOperatorGuideCatalog` + `/cp/super-cp-operator-guide-app`,
      route taken back from the generic guides hub): `epc_scp_guard_super_admin` behaviour (Super-CP-host-only notice, then Super CP login
      notice), PHP hero with Customer board / Tenant hub actions, both callouts (Operator role, “Tenant CP — no Operator sidebar group”),
      the six PHP module cards with summary / “Who should use it” / numbered workflow / Open link, the five-step typical-operator-day strip
      and the menu-location note
- [x] Industry settings — `industry_settings.php` twin (`/cp/industry-settings-app`, route taken back from the industry-packs digest):
      PHP brand banner and stats, ecosystem-grouped industry select, per-industry style templates with palette swatches, storefront
      layouts, branding/domain fields, the four access modes, the 27-language CP registry, contact block, ERP module matrix with
      preset labels, CP pack matrix (forced `core`, `super_platform` hidden on tenant hosts) and sidebar group/item visibility;
      `POST /cp/industry-settings/save` twins `ajax_portal.php?action=save_settings` — admin+`cp` gate, `confirmWrites`, visible→hidden
      inversion against the tenant's own `control_groups`/`control_items`, PHP normalisation (packs, ERP-module defaults per access
      mode, theme aliases) and a host-alias-scoped `epc_portal_site_settings` upsert
- [x] Social media hub — `epc_social_media_hub_panel.php` twin (`SocialMediaPackCatalog`, `CpSocialHubService`, `CpSocialCrypto`):
      the seven PHP tabs (Marketing pack, TikTok, Instagram, Connected accounts, AI advisor, Drafts, Guide), PHP brand/site-key
      resolution with the Super CP tenant selector, brand-adapted captions and hashtags, trending formats and industry hooks,
      caption generator, TikTok specs/privacy levels, Reel and guide video cards with “Use as media URL”, AES-256-CBC per-tenant
      credential vault (PHP `epc_social_crypto_key` derivation, tokens never rendered back, blank fields keep stored secrets),
      `save_account`/`test_account`/`delete_account`/`save_draft` on `POST /cp/social-hub/write` and the PHP hub stylesheet;
      live sending to Meta/TikTok still runs on the Classic twin
- [x] Marketing broadcast — `epc_marketing_broadcast.php` / `_panel.php` twin (`MarketingBroadcastCatalog`,
      `CpMarketingBroadcastService`, `CpMarketingBroadcastRecipients`, `CpMarketingBroadcastWriteService`): PHP tab order
      (Email, WhatsApp, History, Guide), KPI strip (customers with email/phone, emails sent, WhatsApp sent, campaigns),
      the four PHP audience modes (`all`, `with_orders`, `group`, `manual`) with group picker and manual list parsing,
      four email + five WhatsApp templates with `{{customer_name}}`/`{{shop_name}}`/`{{shop_url}}` merge tags, live email
      preview and chat-style WhatsApp bubble, SMTP readiness diagnostics, 1–100 batch clamp, PHP-compatible
      `epc_marketing_broadcast_campaigns`/`_log` schema with per-recipient logging, `send_email` via the tenant SMTP
      service and `send_whatsapp` as PHP Phase 1 `wa.me` link preparation on `POST /cp/marketing-broadcast/write`
      (admin + `cp` gate, `confirmWrites`), plus the PHP broadcast stylesheet; automatic WhatsApp Cloud API delivery
      stays on the Classic twin until a verified provider integration exists
- [x] Power BI — `epc_power_bi.php` + `content/general_pages/epc_power_bi.php` twin (`PowerBiCatalog`, `CpPowerBiService`,
      `CpPowerBiWriteService.EnsureSchemaAsync`): PHP hero and guide link, capability matrix (available now vs needs customer
      Azure/Power BI Pro vs Phase-A exclusions), the five-step connect guide and `X-API-Key` curl example, the seven-dataset
      Web-connector catalogue (`catalog`, `kpis`, `orders`, `sales`, `stock`, `gl`, `metrics`) with paths/formats/scope/params,
      Super CP tenant selector with the PHP host-match then first-tenant fallback, workspace config form (workspace/Azure tenant/
      default report/dataset IDs, embed mode, embed URL, notes), registered-report table and add-report form, native
      `epc_power_bi_config`/`epc_power_bi_reports` schema ensure before reads and writes, PHP embed resolution
      (`config_missing` / `needs_azure` / `url_missing` / `url_invalid` / `ready`) with the HTTPS `*.powerbi.com|.us` allowlist
      and iframe preview, and the tenant-scope guard so a non-operator CP cannot post another tenant's `site_key`;
      Azure embed-token minting is deliberately not implemented until the tenant supplies its own AAD app and capacity
- [x] CP top menu — DB-driven `control_groups`/`control_items` twin (#1513) + `/cp/...` row mapping through `MapCpPhpPath`
- [~] **CP dashboard twin** (`epc_tenant_cp_dashboard.php`) — built in 3 parts:
  - [x] Part 1: `CpTenantDashboardService` — PHP-exact `epc_tcp_dash_stats` SQL (orders today/7d/prev-7d, catalogue, warehouse/goods qty,
        SKU, vendors, clients, pending tasks, returns, VIN), per-KPI failure isolation, `epc_tcp_dash_stats:v7:<db>` 120 s cache,
        `TenantDataGuard` containment → zero-safe values, dynamic 7-day labels/counts, PHP `epc_tcp_dash_change` formatter,
        `epc_co_profile` company name + base currency, Finance pulse only when finance data exists
  - [x] Part 2: persistent per-user shortcuts — `CpShortcutCatalog` (PHP catalogue, auto_parts + generic default strips, CP tone map),
        `CpTenantDashboardService.LoadShortcutsAsync` (reads `epc_user_shortcuts` for surface `cp`/`both` on the per-tenant connection,
        seeds industry defaults only on first visit, zero-safe when the table/DB is unavailable), `POST /cp/dashboard/shortcut`
        (`shortcut_add` / `shortcut_delete` / `shortcut_delete_key` / `shortcut_reset` / `shortcut_reorder`, admin session + `cp`
        capability + CSRF, catalogue keys resolved server-side so posted URLs can't be trusted, `javascript:`/`data:` rejected),
        `.eds-*` grid/editor markup at PHP geometry (172 px min tile, 12 px gap, 118 px min height, 40×40 icon, hover lift)
  - [x] Part 3: conditional insights suite projection (CP variant, CSS only when insight HTML exists), DB industry label/icon for all
        industries incl. jewellery and fit-out, tenant ERP/storefront links through `MapCpPhpPath`
- [ ] Remaining CP PHP twins to verify/finish: `epc_super_cp_customer_board`,
      `epc_super_cp_operator_guide`, `epc_integrations_hub` + each integration settings page (WhatsApp, payment
      gateways, trackers, social media, Power BI, APIs, parts agent), `industry_settings`, `tenant_hub`, `sms-operatory`,
      `communications`, `epc_pos_tenant_manage`, `usergroups`, `content_tree` write actions, customers/documents/logistics single-item pages
- [ ] Auto-generated matrix: every DB menu item → PHP file → ASP.NET route → page type (twin / digest / missing); fix every digest/missing
- [ ] Presentation diff per CP page vs PHP (layout blocks, table columns, buttons, colours, icons, guide/help panels, breadcrumb)
- [ ] Guides: `cp-guideline`, module guide pages, operator guide, OMS menu guide (`epc_oms_menu_guide_lib.php`)
- [ ] ASP.NET-only CP routes PHP does not have → redirect/park (no invented menu entries)
- [ ] 53 CP routes whose PHP lives under ERP tabs (jewellery-*, AML/SOC2, blockchain proofs, landed cost, budgets, tickets/SLA,
      promotions/marketing, approvals/workflow) → alias to the ERP twins built in Phase B

### A2 CP exit
- [ ] Matrix shows 0 digest / 0 missing for tenant and super menus; parity tests + full suite green

---

## Phase B — ERP (PHP `cp/content/shop/finance/erp/**` + `content/shop/finance/**`)

- [~] **B0 ERP top menu** — `erp_nav_areas.php` twin (`ErpNavTree` + `IErpNavMenuService`): 35 areas and 154 placements in PHP order, RBAC
      `allowedTabs`, industry filter (jewellery `jw_*`), commerce filter, enabled modules, report injection, favourites, company
      picker, AP/AR/GL chain nav; replace `LegacyDesktopChromeCatalog.ErpTopnav()`; 0 invented tabs.
      Added a versioned `ErpNavAudience` policy and duplicate-placement audit so tenant version, industry,
      country and Super ERP deny rules can be evaluated without silently hiding PHP modules. The runtime
      now carries the active legal entity country and supports explicit tenant module-pack allow sets.
      A source-to-artifact reconciliation command now compares all PHP category/area/tab order,
      labels, icons, descriptions, groups, jewellery/raw flags, links, duplicates, and empty hrefs.
      Runtime visibility decisions now expose stable reason codes for industry filtering, explicit
      deny rules, module-pack restrictions, and PHP-default visibility. Persisted entitlement
      storage now reads explicit `erp.nav.disabled.*` tenant feature flags and recognizes only
      exact PHP catalog IDs/keys/areas from assigned industry-pack `modules` JSON. Generic pack
      labels remain non-authoritative, so they cannot accidentally hide PHP placements. Version
      persistence and Super ERP administration remain pending. The policy now also exposes a
      complete 154-placement inspection projection with visible/hidden reason codes for future
      Super ERP review screens.
- [ ] B1 Home & workflow (dashboard, workflow, processflow, approvals, workflow_automation, agenda, contacts, documents, knowledge base, AI assistant)
- [~] B2 Order-to-Cash / Syncron-style OMS — sales orders, delivery notes, invoices, revenue, fulfilment, subscriptions, proposals,
      ASP.NET sales-order save/status/invoice/delete/cancel lifecycle is live; cancellation now uses the
      dedicated reversal-safe lifecycle endpoint with a required operator reason. Remaining fulfilment
      dashboard parity (payment/stock/delivery/returns funnel) is still pending.
      leads/opportunities/CRM; libs `epc_erp_order_fulfillment`, `epc_fulfillment_queue`, `epc_order_erp_pipeline`,
      `epc_order_supplier_fulfillment`, `epc_erp_scm`, `epc_erp_order_planning` (no `epc_erp_syncron_policy.php` exists in repo — confirm with user)
- [~] B3 Procure-to-Pay — supplier settlement and PHP-compatible draft payment-batch creation now have ASP.NET form/JSON writes; draft creation best-effort starts the existing PHP payment-lifecycle process-flow case when provisioned; supplier portal, requisitions, RFQ, purchase orders, 3-way match execution, payment approval/export, landed cost (+v2), and barcode purchase remain pending
- [ ] B4 Inventory & warehouse — inventory, groups, reports, order planning, WMS, virtual warehouse, master planning, RFID, quality; forecast → Python
- [~] B5 Record-to-Report / GL — manual journal and opening-balance lines now use PHP COA/inventory-backed selectors, opening-batch line drill-down, pre-post validation, and transactional live posting for COA/cash-bank-only batches; inventory and fixed-asset batches remain explicitly gated until their shared transaction services are available; selectable posting date, list/detail debit-credit balance summaries, and posted-journal reversal now use the
      live validated GL endpoints with balanced-line validation, reversal safeguards, and audit logging; GL, COA, opening balances, aging, P&L, balance sheet, trial balance, year end, period close, fiscal periods,
      dimensions, cost models, budgeting, consolidation (BU/group/IC), multi-entity, multi-currency GL, revaluation, fixed assets, expenses, projects; project transaction form now preserves the PHP category field
 - [~] B6 IFRS report pack + drill-down — external reporting now renders a PHP-shaped 43-section/page-aware annual IFRS/IFRS 18 pack with cover, contents, primary statements, 30+ notes/disclosures, applicability index, VAT/Corporate Tax/e-invoice bridge, deterministic sample fallback, and expandable invoice/bill source schedules; annual live invoice and purchase rows now take precedence when available, expose clickable source-document actions, and show source kind plus net/VAT/gross metadata, while missing source populations remain clearly labelled sample schedules; account → journal → voucher drill-down, filing/scheduler, executive dashboard, print designer, and exact PHP report-layout parity remain pending
 - [~] B7 Cash & treasury — cash-entry posting, bank-statement line matching, bank-instrument create/status lifecycle, generic petty-cash float creation, payment-batch draft validation/write, and PHP-compatible payment-batch dimension-link writes now have ASP.NET coverage with PHP-style cash-account and dimension inputs; the cash-entry workspace now presents aligned D365 F&O-style New/Create, receipt-journal, payment-journal, Options, amend, and audit-safe Void actions while preserving PHP RV-/PV- voucher flows; cash forecast, collections/dunning, credit, settlement, advances, withholding, payment approval/export, and broader bank import/report parity remain pending
- [~] B8 Tax & compliance (tenant-country profiles) — VAT return/refund and tourist VAT writes, UAE FTA legislation fetch/checklist/regen, and CT adjustment writes now have ASP.NET handlers; full return filing, e-invoice (Peppol/XML), electronic reporting, AML, blockchain proofs, customs/shipping, and non-UAE country profiles remain pending
- [ ] B9 HR & payroll — staff, HR ops, recruitment, performance, labour-law profiles, payroll, WPS (UAE profile)
- [ ] B10 Service & after-sales — contracts, tickets, SLA, warranty/RMA, insurance, doc expiry, plant maintenance
- [ ] B11 Production — manufacturing, MFG planning, quality, product structure, PLM, costing
- [ ] B12 Retail & commerce — retail, POS, card reader, e-commerce/CRM integration, marketing
- [ ] B13 Setup & administration — ERP setup, security roles (RBAC), platform, data import/migration, integration, tenant config,
      org admin, business units, shortcut icons, on-premises, audit, DB integrity, governance
- [ ] B14 Guides — ERP guide, full/advanced/operator guides, process flows
- [ ] **B-J Jewellery pack** (39 `jw_*` tabs + INDUS study): masters (karat, rate type, metal stock, design, diamond, pearl, colour stone,
      currency, gold rate, tags, barcode, divisions, prefixes, cost/price types, price lists, daily rates), purchasing (metal, diamond,
      fixing), manufacturing/stock (verification, balance, weight ledger, valuation, transfers), sales/POS (retail, metal, fixing, return,
      advance, old-gold exchange, gold scheme, multi-currency tender), repairs/workshop chain, finance (weight + value TB, JV, petty cash,
      tourist VAT), field injection into inventory/PO/SO for jewellery tenants; all 26 `jw_*_save` ajax actions
- [~] **B-F Fit-out / interior-contracting pack** (user spec `fitout.txt`; ASP.NET now exposes live PHP-compatible project budget, project transaction, and project-recognition writes over the shared project-accounting foundation; recognition preserves PHP POC/completed/straight-line calculation, WIP semantics, pre-post validation, and selectable as-of dates; the full pack extends PHP `epc_erp_project_accounting.php`
      `epc_prja_budget/txn/recognition` + `erp_tabs_projects.php`, industry codes `construction_contracting`/`building_materials`):
      Lead → Estimate/BOQ (sections, items, material/labour/subcontract/equipment/overhead rates, markup, revisions, Excel import)
      → Quotation → Contract (advance/retention/warranty) → Project + hierarchical cost codes → budget/committed/actual/forecast
      ledger (`project_transactions` per project + cost code) → PR/RFQ/PO/GRN (3-way match, tolerance) → material issue → subcontract
      orders/measurements/certifications → site daily reports (photos, mobile) → timesheets at cost rate → equipment usage →
      variations (approved only revise contract) → RFI/drawings revisions → QA/QC inspections/snags → weighted BOQ progress
      (versioned/approved) → progress claims/invoices (gross, retention, advance recovery, VAT) → project P&L/forecast profit;
      configurable approval engine (thresholds, levels), numbering (`QT-2026-00001` …), RBAC roles, dashboards (CEO/PM/finance/procurement),
      reports (sales/estimation/projects/procurement/inventory/subcontract/finance); shares ERP masters (customer, supplier, item,
      COA, tax, warehouse, currency); D365-style forms; phased P1 core → P2 operations → P3 finance → P4 advanced; MVP = 32-step scenario
- [ ] All 321 `ajax_erp.php` actions have an ASP.NET dispatcher case (CSRF + RBAC) — tracked by a parity test
- [ ] **ERP document/report designer** — per-tenant/company (optional branch) templates for vouchers, invoices, orders, statements,
      reports: logo, header/footer, fonts, colours, columns, layout, number/date formats, print/PDF/email variants, preview,
      versioning + effective dating, audit, rollback, tenant isolation, safe template content
- [ ] Statutory profile tests for ≥ 2 countries (UAE + one non-UAE)

---

## Phase C — Storefront & remaining areas
- [ ] C1 Storefront themes (`expan`, `limo`, `modex`, `nero`) CSS/asset parity; fix `/php-reference/home` 404
- [ ] C2 Catalogue & search (search tabs, spare-parts search, ucats/umapi/laximo, vehicle catalog) — search latency (~6 s)
- [ ] C3 Cart / checkout / payments (gateway callbacks, obtaining modes, guest order)
- [ ] C4 Customer area (account, requests, balance, quotes, returns, garage, wishlist, print docs)
- [ ] C5 Vendor / B2B (vendor portal, bulk upload, parts agent, demand intelligence, channels, marketing)
- [ ] C6 BOS / Super-CP / tenant hub (provisioning writes, operator guides)
- [ ] C7 Marketing / LifeOS / IP / API v1 (forms, SEO/sitemaps)
- [ ] C8 Cron & workers — every PHP cron has a Worker twin (currency rates, e-invoice poll, legislation fetch, demo expiry, cache warm, webhooks)

---

## Phase D — Platform hardening & operations (during and after migration)
- [ ] Tenant isolation review per module (no cross-tenant reads, degraded-shared guard, credentials never leak)
- [ ] Security: CSRF on all writes, RBAC/ACL parity, audit on all mutations, rate limits, secure headers
- [ ] Zero-downtime releases: rolling/blue-green Kestrel, readiness/liveness probes, graceful drain, persisted session state,
      backward-compatible app/DB versions, expand-then-contract migrations, rollback runbook
- [ ] On-premises installation package + multi-version compatibility (1000 tenants, multi-industry, multi-country)
- [ ] Versioning, licensing/rights, compliance & policy documentation
- [ ] VPS: prune 15 parallel PHP-FPM versions (after test round 1)

---

## Phase E — Testing rounds & gates (user sequence)
- [ ] Round 1: CP + ERP combined browser test (both industries, tenant + super hosts) via `testing-erp-aspnet-local`, side-by-side with `/php-reference`
- [ ] Round 2: storefront + BOS + marketing + workers
- [ ] Round 3: full regression → `AspNetInteractiveCompleteCount = N`
- [ ] Gates: `CutoverAllowed` → PHP reference read-only one release → `PhpSourceDeletionAllowed` (with user approval)

---

## Phase F — Post-migration: enterprise ERP feature-parity matrix
- [ ] Area-by-area matrix vs Dynamics 365 F&O, SAP, Oracle, Odoo, QuickBooks, Peachtree/Sage 50, Zoho, 1C Enterprise (GL, AR/AP,
      fixed assets, budgeting, cash, tax, consolidation/intercompany, procurement, inventory/WMS, MRP, projects, HR/payroll, CRM, POS,
      e-commerce, BI, workflow, audit, multi-company/currency/language, API, DMS, dimensions, period close, bank recon, expenses,
      service, quality, transport, leasing, revenue recognition, credit, rebates, subscription billing)
- [ ] Build every missing capability found by the matrix

---

## Open questions for the user
1. Syncron: no `epc_erp_syncron_policy.php` in repo — send original policy/docs or confirm twinning `epc_erp_scm.php` + `epc_erp_order_fulfillment.php`.
2. ERP-only staff role (PHP RBAC allows it; ASP.NET auth currently grants CP+ERP together) — confirm wanted.
3. Jewellery legacy schema open items (INDUS study) — verified against PHP tables + sample transactions; nothing invented.
4. Fit-out pack: PHP only has generic project accounting (budget/txn/recognition); the BOQ/variation/progress-claim/subcontract chain is
   new DB-backed design per the user's `fitout.txt` spec — user to confirm the Phase-1 scope (customers, suppliers, items, cost codes,
   projects, estimation, BOQ, quotation, contracts, budget) before B-F build starts.
