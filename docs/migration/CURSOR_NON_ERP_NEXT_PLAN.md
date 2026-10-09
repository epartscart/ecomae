# Cursor non-ERP next plan

Executable queue for the PHP→ASP.NET storefront / CP / BOS / marketing / tenant
migration. ERP stays Devin. Regenerated from `scripts/php_reference_gap_inventory.py`
after PlanQ1Faq (`--max-gap 522` passed; current gap **521**).

Refresh the table:

```bash
python3 scripts/php_reference_gap_inventory.py --json /tmp/gap_inv.json \
  --md docs/migration/inventory/PHP_REFERENCE_GAP_INVENTORY.md --max-gap 521
python3 scripts/php_non_erp_gap_buckets.py --inventory-json /tmp/gap_inv.json
python3 scripts/php_unmentioned_functions_catalog.py --inventory-json /tmp/gap_inv.json
```

## 1. Measured remaining work

| Plan step | Area | Gap files | Lines |
|---|---|---:|---:|
| 1 | Storefront: catalogue | 35 | 10,764 |
| 1 | Storefront: modules | 18 | 3,446 |
| 1 | Storefront: other shop | 19 | 6,669 |
| 1 | Storefront: parts/docpart | 30 | 34,919 |
| 1 | Storefront: templates | 3 | 2,689 |
| 1 | Storefront: users/plugins | 3 | 2,192 |
| 3 | CP other | 12 | 3,578 |
| 3 | CP shop core (orders, catalogue, price upload) | 30 | 13,605 |
| 3 | CP shop smaller | 23 | 7,804 |
| 4 | CP control/portal | 40 | 10,296 |
| 5 | Marketing/BOS/industries | 119 | 63,363 |
| 6 | Price engine | 18 | 16,113 |
| 7 | ERP finance (Devin) | 166 | 66,191 |
| 8 | Core/root | 5 | 2,983 |
| | **Total** | **521** | **244,612** |

- Non-ERP (Cursor): **355 files / 178,421 lines**
- ERP finance (Devin): **166 / 66,191** — do not edit
- Functions unmentioned: **7,146** of 9,870. Ready non-ERP PHP on gap files ≤200 lines: **74**. Catalog: `docs/migration/inventory/PHP_UNMENTIONED_FUNCTIONS.md`

Bucket rules live in `scripts/php_non_erp_gap_buckets.py` (first path-prefix match). The table always sums to the inventory.

## 2. Definition of done (honest close)

A gap file leaves the inventory only as **PORT**, **MAP**, **RETIRE**, or **SKIP**.

- **PORT**: PHP 8.3 runtime golden on a throwaway `ecomae_cpw_*` MariaDB schema; ASP.NET twin equals that golden (intentional security deviations documented); full path constant plus every PHP function identifier appear in `aspnet/src` (not tests, not catalogues). GET does not mint a guest / impersonation session.
- **MAP**: routed or retired-equivalent with a reason, not a path-string mention.
- **RETIRE**: `docs/migration/inventory/PHP_RETIRED.tsv` with a reason. Never retire a live include.
- **SKIP**: parent kernel missing, same-basename collision, or mentioning the identifier would close a page that is not ported.

Do not close both sides of a basename collision (`orders_background.php`, `show_details.php`). Do not write the full path of a large unported sibling (`epc_marketing_broadcast` + `"_panel.php"`). Files whose names contain Catalog/Inventory/Matrix/DryRun/Reporter/Dashboard/Readiness/Contract/Probe/Evidence are skipped from the corpus.

## 3. Next queue

### Q1 — done (PlanQ1 / `PhpPlanQ1`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1`, 35 cases): bootstrap-light, deploy-auth, BOS security lists, PHP-reference router, CP common-parity, office/storage meta, perf-cache, tenant-brand stub, genuine-manufacturer index.

### Q1 next — done (PlanQ1Next / `PhpPlanQ1Next`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Next`, 12 cases): storefront logo markup/hub flags (stub site profile; hub and animated-logo parents stay their own gaps), industry-pack builtin catalog plus seed/assign/tenant/fleet, promotions create/list/apply/usage/fleet. Schema helpers are in-memory twins of the PHP PDO functions (throwaway MariaDB goldens).

Skipped on purpose:

- `epc_sku_media_storefront.php` / `epc_sku_media_cp_install.php` until `epc_sku_media.php`.
- `cp/content/content/get_content_records.php` (`addContentToDump`): the walker is a pure tree walk, but mentioning it closes the include-time dump page, which needs `DP_ContentRecord` / `translate_str_by_id`. Leave unmentioned until that class is ported.

### Q1 after — done (PlanQ1After / `PhpPlanQ1After`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1After`, 4 cases): industry live-bridge defs / storefront URL / inject-merge / audit. URL and audit stub `epc_industry_seo` / `epc_portal_industries` like tenant-brand (do not mention the SEO parent path or all 12 SEO functions).

### Q1 more — done (PlanQ1More / `PhpPlanQ1More`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1More`, 23 cases): industry catalog profile/title/unsplash/categories/render (electronics-retail image helpers optional; parent stays a gap), storefront layout registry/default/meta/sections/active/js, industry subdomain detect/resolve/bootstrap, dealer portal tiers/register/list/order/auto-tier/fleet/get/update/suspend/activate/report (in-memory twin; `epc_dealer_orders` LIMIT bind fatals on this MariaDB — twin takes an int limit), social-hub CSS body (do not mention the CP page-assets parent). OEM `Functions.Common.php` stays skipped.

### Q1 leftover — done (PlanQ1Left / `PhpPlanQ1Left`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Left`, 11 cases): page-cache enabled/key/dir/serve/flush/purge (inject cache dir; lock/exit goldens skipped), isolation-anomaly scan/record/resolve/fleet (missing audit tables stubbed; `epc_anomaly_list` LIMIT bind fatals — twin takes an int), integrations/industry/broadcast CSS bodies, brochure inventory, capability guides. Do not mention the marketing-broadcast panel or CP page-assets parents.

### Q1 done — done (PlanQ1Done / `PhpPlanQ1Done`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Done`, 9 cases): PHP serving pause flags (CLI maybe-exit stays false), CP phase tracer, Prime Invest landing data (stamp SKU / AED format), ECOM AE marketing catalogs, SOC2 control/evidence/policy twin (in-memory; MariaDB goldens).

### Q1 gov — done (PlanQ1Gov / `PhpPlanQ1Gov`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Gov`, 4 cases): platform governance categories/defaults/seed/list/update/applies/active/branding-block, tenant config groups/get/set/bulk/export/import/fleet (`epc_tenant_config_history` LIMIT bind skipped — twin takes an int).

### Q1 twin — done (PlanQ1Twin / `PhpPlanQ1Twin`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Twin`, 8 cases): import orchestrator create/validate/chunk/dry-run/cancel/retry, document vault folder/version/GDPR/search, on-prem license generate/activate/revoke/health (signing key absent → `signing_unavailable`; list LIMIT bind skipped), BI builtin metrics/snapshot/dashboard/fleet/compare (`epc_bi_metric_trend` LIMIT and `epc_bi_cleanup` INTERVAL binds skipped), notification center send/list/prefs/digest (events include stays a gap — harness does not copy it). Do not write `core/dp_` + engine basenames as one path string.

### Q1 data / mig / hook / sec — done (`PhpPlanQ1Data`, `PhpPlanQ1Mig`, `PhpPlanQ1Hook`, `PhpPlanQ1Sec`)

Closed against PHP 8.3.6 goldens: jewellery / fashion / electronics `*_data.php` catalogs (footer uses portal-missing store names only; do not mention helper / portal / `*_header_href` paths), `epc_db_migrations.php` (MariaDB DDL implicit-commit makes apply/rollback return `ok=false` after the row is written), `epc_webhooks.php` + `epc_events.php` (no live HTTP; emit with no matching hooks so dispatch returns 0; `process_retries` / `dlq_list` LIMIT binds skipped), `epc_security_kernel.php` (headers / lockdown / risk / allowlist / backend group / BOS role; CSRF `session_start` skipped; `require_ops_access` mentioned but not golden-run).

### Q1 mark / pack — done (`PhpPlanQ1Mark`, `PhpPlanQ1Pack`)

Closed against PHP 8.3.6 goldens: `printProductBlock` markup from `content/shop/catalogue/helper.php` (CRLF tile/list/bookmarks/compare/admin/warehouse/cart-suggestion; 11 cases), `epc_ecomae_faq_data.php` (105 items / status counts), `epc_ecomae_legal_content.php` (effective date / catalog / top-level aliases), `epc_ded_activity_mapping.php` (divisions / registries / group filter / coverage audit / portal bridge). Audit and bridge take injectable maps; do not mention the industry-consolidation or portal parent paths.

### Q1 rest — done (`PhpPlanQ1Rest`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Rest`, 9 cases): demand-country ISO maps / parse / preview / import (article-normalize and schema injected; CHAR(2) migrate INSERT fatals on this MariaDB STRICT — no-op ISO3 path only), industry theme registry / default / ERP kit / CP alignment, brochure topic catalog / resolve / unique photos / svg URL, theme-template slots / palettes / quartet / normalize (industry maps injected), storefront package registry / resolve / preset / apply. Package dump omits header/home/footer sibling paths. Do not mention the portal parent path.

### Q1 plus — done (`PhpPlanQ1Plus`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Plus`, 8 cases): warehouse extra-field catalog / header map / extract / merge / encode / index search / lookup / json_params (in-memory store; lookup cache matches PHP), CP script/style relocate / main-pane splice / footer inject / BOC first-paint, POS terminal markup (`epc_pos_h` stubbed; helpers parent stays a gap), role-home catalogs / can / render / detect / assign / tile (action URLs concatenated so leftover unique basenames are not written as path strings), channel schema + helpers (catalogs, demo rate, seed/sync/import/dashboard/report). Shipment `random_int`/`date` is implemented with injectable clock/rng and not golden-run.

### Q1 ship — done (`PhpPlanQ1Ship`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Ship`, 6 cases): logistics helpers (h / money / seed / dashboard / report / urls / snapshot; channel schema+helpers already ported; demo/setup URLs concatenated), electronics taxonomy tree / seed / list / slug / breadcrumb / category link (optional industry-taxonomy migrate not copied), social pack catalogs / brand adapt / thread (helpers parent stubbed to adapt-text only), storefront worldclass JSON-LD / social links / newsletter / trust / cookie / js (portal parent stubbed). `substr` of the thread starter is bytes.

### Q1 site — done (`PhpPlanQ1Site`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Site`, 5 cases): site-context cache key / reset / context / domain / url / host / trade / from / admin / phone / apply / document defaults (portal host/profile/industry/guess/auto/home and branding system/hub stubbed; default-contact is private so the portal parent stays a gap), leftover supplier `epc_supplier_h` / storage-id / LPO HTML, leftover CP ACL preload / expand-groups / content-url. Cache-key regex is lowercase-only (`Demo-Site!` → `emoite`). Apply uses `empty()` so blank head-office fields keep the config value. Expand-groups unique+sorts when the nested-groups helper is absent.

### Q1 ask — done (`PhpPlanQ1Ask`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Ask`, 4 cases): copilot intents / parse / generate-sql / execute / fleet (history `LIMIT ?` bind skipped — twin takes an int), AI PII strip / detect / classify / anomaly / NL report / route / service-query / stats (recent `LIMIT ?` bind skipped). PII patterns apply in PHP order (15-digit TRN before IBAN). Execute logs SQL and does not run it.

### Q1 work — done (`PhpPlanQ1Work`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Work`, 5 cases): orders workspace h / storage-label / usd-rate / aed-usd / badges / kpi / status ids / cookie tab / filter / count (currency records injected), marketing helpers h / snapshot / progress / completion / kpi / review / resolve-link / demo-report (playbook catalog injected so that parent stays a gap), CP breadcrumb humanize / caption / ensure-folder / repair. `empty('0')` is empty. Badge class uses a request static cache.

### Q1 faq — done (`PhpPlanQ1Faq`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Faq`, 2 cases): FAQ format-answer (longest-label-first replace, so Auto Price AI nests inside Auto Price AI page), status class, schema JSON (105 questions), styles body (3334 chars), render-page counts/contains. Home helpers are harness stubs only and stay unmentioned. `showModule` / `filterFaq` are the page-local JS identifiers.

### Q1 next leftovers — honest schema/data twins still open

The 74 leftover “ready” ≤200-line rows are still Q2/Q3. Next honest ports (do not port industry templates — each `require`s `_base_template.php` and renders a full HTML page):

| Order | File | Why now |
|---|---|---|
| 1 | `epc_ai_classification.php` | Self-contained HS/category rules + schema. |
| 2 | `epc_ecomae_marketing_pages.php` | Content parent already mentioned; home helpers stay stubbed. |
| 3 | `epc_cp_top_alerts.php` | Header SSL/email/SMS helpers, no missing kernel. |
| 4 | OEM `Functions.Common.php` (124) | Third-party catalog API client — **skip**. |
| 5 | `epc_platform_jobs.php` | Skip until `epc_portal_tenant.php`. |
| 6 | `printProducts.php` / `printProducts_2.php` | Catalogue list parent still open — skip until that kernel. |

After each file: regenerate inventory with `--max-gap` = previous gap count; leftover `ecomae_cpw_%` must be 0.

### Q2 — skip until the named parent lands

| File / function | Parent still open |
|---|---|
| `printProducts.php`, `printProducts_2.php`, `printCatalogueNode`, `getHtmlOfTopMenuCatalogue` | Catalogue list kernel |
| Search tabs, `side_menu`, page-builder render, BOC consoles | Those kernels |
| Storefront `orders_background.php` | Already ported; CP helper of the same basename is not. Path mention would close both. |
| `epc_marketing_broadcast_panel.php` | Panel body. Wrapper/config already ported. Concatenate the path in C#. |
| `epc_el_pl_href`, `epc_ep_pl_href`, industry `*_header.php` hrefs | Product-line / header pages `require` a parent and return. |
| `epc_epartscart_storefront.php` | Storefront kernel + APAI aliases |
| `epc_build_initial_price_bunch.php` | `prices_enclosure` |
| `epc_auto_price_*`, `epc_apai_*`, `epc_auto_price_cp_shell.php` | Auto-price engine (6,768 lines) |
| `epc_custom_shipping_guide.php`, `epc_erp_only_onboard_guide.php` | ERP include — Devin |
| `epc_ai_copilot.php`, `epc_bos_ajax_login.php`, `epc_bos_health_check.php` | Tenant PDO / BOS session |
| `epc_cp_breadcrumb.php` | CP content-folder DB |
| `epc_tenant_pdo.php` | Live tenant connections |
| `get_alternative_bread_crumbs` | Breadcrumb module |
| `clear_dir` (`del_tmp_folder.php`) | pyprices upload parent |
| `epc_sku_media_cp_install.php`, `epc_sku_media_storefront.php` | `epc_sku_media.php` (1,311 lines) |
| `addContentToDump` (`get_content_records.php`) | `DP_ContentRecord` dump page |

### Q3 — do not mention-only (false close)

These files expose one `epc_*_h` / `header_href` htmlspecialchars helper and a full page body. Naming the helper in C# would mark the page mentioned while the page is still PHP.

- `epc_ecomae_h` on `epc_ecomae_platform_home.php`
- `epc_cpi_header_href`, `epc_er_header_href`, `epc_frn_header_href`, `epc_jrk_header_href`
- CP portal guides: `epc_adg_h`, `epc_awg_h`, `epc_csg_portal_h`, `epc_eog_h`, `epc_ffg_h`, `epc_pg_h`, `epc_pos_manage_h`, `epc_pbig_h`, `epc_emod_cp_h`, `epc_wa_guide_h`

Port the page, or leave the identifier unmentioned.

## 4. After the small helpers

1. `printProducts.php` / `printProducts_2.php` shells after the catalogue list kernel.
2. `part_search_page.php` and the parts agent.
3. CP order card / order lines / price-upload page bodies (step 3 core).
4. Marketing / industry page kernels in `content/general_pages` (largest remaining non-ERP block: 103 files / 58,788 lines).
5. Price engine last among Cursor-owned libraries, on the existing importer.

## 5. Invariants every slice

- Throwaway DBs only (`ecomae_cpw_*`). Never commit `App_Data/` or `content/` under the platform project.
- Production counts stay `docpart.users` 2, `ecomae.users` 2 unless a test is supposed to change them.
- Platform build: 0 warnings / 0 errors (`TreatWarningsAsErrors`).
- Weighted headline stays a migration-gate number (~20.4%). It is not acceptance. ERP accepted processes stay 0/15.
