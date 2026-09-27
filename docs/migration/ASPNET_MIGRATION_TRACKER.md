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
Current: A 15/17 items (≈88 %) · B 0/21 · C 0/8 · D 0/6 · E 0/4 · F 0/2 → **overall ≈ 17.6 % done / 82.4 % pending**.
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
expanded ordinary check lines/payments with order linkage and correction-check blocks (cause document, payment and six tax sums).)

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
  - [ ] Part 3: conditional insights suite projection (CP variant, CSS only when insight HTML exists), DB industry label/icon for all
        industries incl. jewellery and fit-out, tenant ERP/storefront links through `MapCpPhpPath`
- [ ] Remaining CP PHP twins to verify/finish: `epc_tenant_features`, `epc_tenant_email_settings`, `epc_super_cp_customer_board`,
      `epc_super_cp_operator_guide`, `epc_mobile_apps`, `epc_integrations_hub` + each integration settings page (WhatsApp, payment
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

- [ ] **B0 ERP top menu** — `erp_nav_areas.php` twin (`ErpNavTree` + `IErpNavMenuService`): 36 areas in PHP order, RBAC
      `allowedTabs`, industry filter (jewellery `jw_*`), commerce filter, enabled modules, report injection, favourites, company
      picker, AP/AR/GL chain nav; replace `LegacyDesktopChromeCatalog.ErpTopnav()`; 0 invented tabs
- [ ] B1 Home & workflow (dashboard, workflow, processflow, approvals, workflow_automation, agenda, contacts, documents, knowledge base, AI assistant)
- [ ] B2 Order-to-Cash / Syncron-style OMS — sales orders, delivery notes, invoices, revenue, fulfilment, subscriptions, proposals,
      leads/opportunities/CRM; libs `epc_erp_order_fulfillment`, `epc_fulfillment_queue`, `epc_order_erp_pipeline`,
      `epc_order_supplier_fulfillment`, `epc_erp_scm`, `epc_erp_order_planning` (no `epc_erp_syncron_policy.php` exists in repo — confirm with user)
- [ ] B3 Procure-to-Pay — supplier portal, requisitions, RFQ, purchase orders, 3-way match, payables, payment batches, landed cost (+v2), barcode purchase
- [ ] B4 Inventory & warehouse — inventory, groups, reports, order planning, WMS, virtual warehouse, master planning, RFID, quality; forecast → Python
- [ ] B5 Record-to-Report / GL — GL, COA, opening balances, aging, P&L, balance sheet, trial balance, year end, period close, fiscal periods,
      dimensions, cost models, budgeting, consolidation (BU/group/IC), multi-entity, multi-currency GL, revaluation, fixed assets, expenses, projects
- [ ] B6 IFRS report pack + drill-down (summary → account → journal → voucher → source doc), external reports, scheduler, exec dashboard,
      print designer, doc formats — PHP sample report layouts preserved exactly
- [ ] B7 Cash & treasury — cash/bank, petty cash, bank recon, cash forecast, instruments, collections/dunning, credit, settlement, advances, withholding
- [ ] B8 Tax & compliance (tenant-country profiles) — VAT return boxes, CT return/filing, VAT refund, e-invoice (Peppol/XML),
      FTA legislation fetch (`epc_uae_tax_legislation_*`), elec reporting, AML, tourist refund, blockchain proofs, customs/shipping
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
- [ ] **B-F Fit-out / interior-contracting pack** (user spec `fitout.txt`; extends PHP `epc_erp_project_accounting.php`
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
