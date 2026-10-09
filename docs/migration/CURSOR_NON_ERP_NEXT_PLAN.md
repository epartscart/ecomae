# Cursor non-ERP next plan

Executable queue for the PHP→ASP.NET storefront / CP / BOS / marketing / tenant
migration. ERP stays Devin. Regenerated from `scripts/php_reference_gap_inventory.py`
after ReadyNamed (`--max-gap 595` passed; current gap **590**).

Refresh the table:

```bash
python3 scripts/php_reference_gap_inventory.py --json /tmp/gap_inv.json \
  --md docs/migration/inventory/PHP_REFERENCE_GAP_INVENTORY.md --max-gap 590
python3 scripts/php_non_erp_gap_buckets.py --inventory-json /tmp/gap_inv.json
python3 scripts/php_unmentioned_functions_catalog.py --inventory-json /tmp/gap_inv.json
```

## 1. Measured remaining work

| Plan step | Area | Gap files | Lines |
|---|---|---:|---:|
| 1 | Storefront: catalogue | 36 | 11,180 |
| 1 | Storefront: modules | 18 | 3,446 |
| 1 | Storefront: other shop | 23 | 7,989 |
| 1 | Storefront: parts/docpart | 34 | 36,349 |
| 1 | Storefront: templates | 3 | 2,689 |
| 1 | Storefront: users/plugins | 3 | 2,192 |
| 3 | CP other | 13 | 3,686 |
| 3 | CP shop core (orders, catalogue, price upload) | 31 | 13,857 |
| 3 | CP shop smaller | 24 | 8,039 |
| 4 | CP control/portal | 41 | 10,505 |
| 5 | Marketing/BOS/industries | 173 | 80,616 |
| 6 | Price engine | 19 | 16,422 |
| 7 | ERP finance (Devin) | 166 | 66,191 |
| 8 | Core/root | 6 | 3,111 |
| | **Total** | **590** | **266,272** |

- Non-ERP (Cursor): **424 files / 200,081 lines**
- ERP finance (Devin): **166 / 66,191** — do not edit
- Functions unmentioned: **7,726** of 9,870. Ready non-ERP PHP on gap files ≤200 lines: **162** (50 files). Catalog: `docs/migration/inventory/PHP_UNMENTIONED_FUNCTIONS.md`

Bucket rules live in `scripts/php_non_erp_gap_buckets.py` (first path-prefix match). The table always sums to the inventory.

## 2. Definition of done (honest close)

A gap file leaves the inventory only as **PORT**, **MAP**, **RETIRE**, or **SKIP**.

- **PORT**: PHP 8.3 runtime golden on a throwaway `ecomae_cpw_*` MariaDB schema; ASP.NET twin equals that golden (intentional security deviations documented); full path constant plus every PHP function identifier appear in `aspnet/src` (not tests, not catalogues). GET does not mint a guest / impersonation session.
- **MAP**: routed or retired-equivalent with a reason, not a path-string mention.
- **RETIRE**: `docs/migration/inventory/PHP_RETIRED.tsv` with a reason. Never retire a live include.
- **SKIP**: parent kernel missing, same-basename collision, or mentioning the identifier would close a page that is not ported.

Do not close both sides of a basename collision (`orders_background.php`, `show_details.php`). Do not write the full path of a large unported sibling (`epc_marketing_broadcast` + `"_panel.php"`). Files whose names contain Catalog/Inventory/Matrix/DryRun/Reporter/Dashboard/Readiness/Contract/Probe/Evidence are skipped from the corpus.

## 3. Next queue

### Q1 — port next (self-contained named helpers)

Smallest honest files first. Each row is a real PHP function file, not a page-body `*_h` wrapper.

| Order | File | C# twins | Why now |
|---|---|---|---|
| 1 | `cp/epc_cp_bootstrap_light.php` (108) | `EpcCpBootstrapLightActive`, `EpcCpBootstrapLightInit`, `EpcCpHasAdminCookies`, `EpcCpIsLoginRequest`, `EpcCpRequestRoute` | Cookie/route predicates only. Do not `session_start` on GET. |
| 2 | `epc_deploy_auth.php` (128) | `EpcDeployAllowedIps`, `EpcDeployClientIp`, `EpcDeployForbidden`, `EpcDeployLockdownEnabled`, `EpcDeployRequireToken` | Token/IP gate. Root ops helper, no ERP. |
| 3 | `content/general_pages/epc_bos_security.php` (138) | `EpcBosPublicActions`, `EpcBosProviderOnlyActions`, `EpcBosAjaxActionName`, `EpcBosCsrfMeta`, `EpcBosAjaxEntryGuard` | Action lists + CSRF meta. No live BOS login. |
| 4 | `content/general_pages/epc_php_reference_router.php` (144) | `EpcPhpReferenceApplyDeepUri`, `EpcPhpReferenceIsSuperCpHost`, `EpcPhpReferenceSurface`, `EpcPhpReferenceTryRoute` | URI/surface helpers. |
| 5 | `content/general_pages/epc_cp_common_parity.php` (151) | `EpcCpCommonParityHostMap`, `EpcCpCommonParityPacks`, `EpcCpCommonParityTargets`, `EpcCpCommonParityPackApplies` | Static host/pack catalogs. |
| 6 | `content/shop/catalogue/epc_sku_media_cp_install.php` (118) | `EpcSkuMediaCpInstall`, `EpcSkuMediaCpLang` | Install + lang table. Keep storefront renderer as its own file. |
| 7 | `content/shop/docpart/epc_prices_office_storage_meta.php` (170) | `EpcPricesBuildOfficeStorageDataInfo` | Structured office/storage meta. |
| 8 | `content/general_pages/epc_perf_cache.php` (171) | `EpcPerfCacheDir` / `KeySafe` / `Get` / `Set` / `Delete` / `Remember` / `BustPrefix`, `EpcCpMenuCache`, `EpcCpMenuCacheBust` | File cache, same pattern as `epc_crossbase_cache`. |
| 9 | `content/general_pages/epc_portal_tenant_brand.php` (174) | catalog / config / css / markup / hero | Stub site profile; do not load `epc_portal.php`. |
| 10 | `content/shop/docpart/docpart_genuine_manufacturers.php` (191) | cache path/read/write, names, section counts | Umapi sync stays stub-safe if the live client is absent. |

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
| `epc_logistics_helpers.php` | Channel helpers / schema |
| `epc_auto_price_*`, `epc_apai_*`, `epc_auto_price_cp_shell.php` | Auto-price engine (6,768 lines) |
| `epc_custom_shipping_guide.php`, `epc_erp_only_onboard_guide.php` | ERP include — Devin |
| `epc_ai_copilot.php`, `epc_bos_ajax_login.php`, `epc_bos_health_check.php` | Tenant PDO / BOS session |
| `epc_cp_breadcrumb.php` | CP content-folder DB |
| `epc_industry_packs.php`, `epc_promotions_engine.php` | Schema + fleet stats |
| `epc_tenant_pdo.php` | Live tenant connections |
| `get_alternative_bread_crumbs` | Breadcrumb module |
| `clear_dir` (`del_tmp_folder.php`) | pyprices upload parent |
| `epc_sku_media_storefront.php` | After CP install + product-page media slot |

### Q3 — do not mention-only (false close)

These files expose one `epc_*_h` / `header_href` htmlspecialchars helper and a full page body. Naming the helper in C# would mark the page mentioned while the page is still PHP.

- `epc_ecomae_h` on `epc_ecomae_platform_home.php`
- `epc_cpi_header_href`, `epc_er_header_href`, `epc_frn_header_href`, `epc_jrk_header_href`
- CP portal guides: `epc_adg_h`, `epc_awg_h`, `epc_csg_portal_h`, `epc_eog_h`, `epc_ffg_h`, `epc_pg_h`, `epc_pos_manage_h`, `epc_pbig_h`, `epc_emod_cp_h`, `epc_wa_guide_h`

Port the page, or leave the identifier unmentioned.

## 4. After the small helpers

1. Product-block markup (`printProductBlock`), then `printProducts.php` / `printProducts_2.php` shells.
2. `part_search_page.php` and the parts agent.
3. CP order card / order lines / price-upload page bodies (step 3 core).
4. Marketing / industry page kernels in `content/general_pages` (largest remaining non-ERP block: 173 files / 80,616 lines).
5. Price engine last among Cursor-owned libraries, on the existing importer.

## 5. Invariants every slice

- Throwaway DBs only (`ecomae_cpw_*`). Never commit `App_Data/` or `content/` under the platform project.
- Production counts stay `docpart.users` 2, `ecomae.users` 2 unless a test is supposed to change them.
- Platform build: 0 warnings / 0 errors (`TreatWarningsAsErrors`).
- Weighted headline stays a migration-gate number (~20.4%). It is not acceptance. ERP accepted processes stay 0/15.
