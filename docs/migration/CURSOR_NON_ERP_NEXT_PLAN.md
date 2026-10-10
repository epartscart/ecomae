# Cursor non-ERP next plan

Executable queue for the PHP→ASP.NET storefront / CP / BOS / marketing / tenant
migration. ERP stays Devin. Regenerated from `scripts/php_reference_gap_inventory.py`
after PlanQ1Line (`--max-gap 455` passed; current gap **454**).

Refresh the table:

```bash
python3 scripts/php_reference_gap_inventory.py --json /tmp/gap_inv.json \
  --md docs/migration/inventory/PHP_REFERENCE_GAP_INVENTORY.md --max-gap 454
python3 scripts/php_non_erp_gap_buckets.py --inventory-json /tmp/gap_inv.json
python3 scripts/php_unmentioned_functions_catalog.py --inventory-json /tmp/gap_inv.json
```

## 1. Measured remaining work

| Plan step | Area | Gap files | Lines |
|---|---|---:|---:|
| 1 | Storefront: catalogue | 30 | 8,978 |
| 1 | Storefront: modules | 17 | 3,321 |
| 1 | Storefront: other shop | 15 | 5,502 |
| 1 | Storefront: parts/docpart | 23 | 32,530 |
| 1 | Storefront: templates | 3 | 2,689 |
| 1 | Storefront: users/plugins | 2 | 2,084 |
| 3 | CP other | 10 | 3,274 |
| 3 | CP shop core (orders, catalogue, price upload) | 24 | 12,698 |
| 3 | CP shop smaller | 22 | 7,537 |
| 4 | CP control/portal | 40 | 10,296 |
| 5 | Marketing/BOS/industries | 81 | 42,704 |
| 6 | Price engine | 18 | 16,113 |
| 7 | ERP finance (Devin) | 166 | 66,191 |
| 8 | Core/root | 3 | 1,455 |
| | **Total** | **454** | **215,372** |

- Non-ERP (Cursor): **288 files / 149,181 lines**
- ERP finance (Devin): **166 / 66,191** — do not edit
- Functions unmentioned: **6,397** of 9,870. Ready non-ERP PHP on gap files ≤200 lines: **57**. Catalog: `docs/migration/inventory/PHP_UNMENTIONED_FUNCTIONS.md`

Bucket rules live in `scripts/php_non_erp_gap_buckets.py` (first path-prefix match). The table always sums to the inventory.

## 1b. Who owns UI/UX, design, testing, and tenant control

Recorded 2026-10-10 from the owner. Fleet size in scope: about **1000 tenants**.
Tenants are **site-only**, **ERP-only**, or **mixed** (site + ERP). Some tenants have
**1000+ users**. Isolation is mandatory. Scale does not relax the boundary.

| Concern | Cursor | Devin | Shared / owner |
|---|---|---|---|
| Overall structure | Tenant control plane, host routing, storefront / CP / BOS / CRM shells, industry and demo hosts | ERP document model, posting, ledgers, finance tabs | One system of record, one authorization model, one workflow, one audit trail, one API contract. Owner accepts the structure. |
| UI / UX | Storefront, marketing, CP, BOS, CRM, tenant chrome. Must be better than the PHP reference. | ERP workspaces and finance screens. Must be better than the PHP reference. | Same-tenant PHP vs ASP.NET dual samples. Human acceptance. |
| Designing (IA, flows, role workspaces) | BOS / CRM / CP / storefront / tenant flows | ERP role workspaces (CFO, Finance Manager, Accountant, Sales, Purchasing, Warehouse) | Screens may differ by role. Business logic is not copied into a second engine. |
| Presentation | Layout, colours, menus, tables, chrome on Cursor surfaces | Layout of ERP pages, reports, and finance print | PHP reference is the minimum, not the ceiling. |
| Graphics | Storefront / marketing / industry / BOS / CP visual assets and tenant branding | ERP document logos and attachments on finance docs | Branding stays tenant-scoped. Tenant A never serves Tenant B assets. |
| Functionality testing | PHP 8.3 goldens and the platform suite for non-ERP; CP / storefront / BOS browser rounds | ERP write dry-runs, 15 process acceptance, ERP browser round | Phase D combined test, dual samples, production `/health` `/ready` probes. |
| Data security / confidentiality | Tenant DB + `site_key` scoping. No cross-tenant read of users, carts, orders, tokens, or files. Super-CP may pick a tenant; tenant CP sees only its own `site_key`. | ERP rows stay on that tenant’s ledger. No cross-company leak. | One tenant = one data boundary. A 1000-user tenant does not share sessions or caches keyed only by user id. |
| Authentication | Storefront / CP / BOS / CRM login, MFA, OTP, SSO, session cookies per tenant host | ERP login uses the same tenant session and capabilities — not a second identity store | GET does not mint a guest or impersonation session. |
| Profile / user control | `users`, profiles, groups, offices, KYC, trade status scoped to `site_key` | ERP staff / payroll profiles on that tenant only | Tenant A cannot see Tenant B users even when e-mails collide. |
| Control (permissions) | CP roles, Super-CP operator vs tenant operator, capability flags (`cp`, `erp`, `bos`, `api`) | ERP document approval and period lock on ERP services | Shared authorization model. Site-only tenants do not receive another tenant’s ERP data. ERP-only tenants do not receive another tenant’s storefront users or carts. Mixed tenants share one `site_key` for site+ERP and stay isolated from every other tenant. |

Commerce isolation, MFA, and Power BI API keys already lock work to one `site_key`.
The tenant onboard kernel (`PhpPlanQ1Dock`) now registers all three types on one save path:
site-only (own hostname + shared or dedicated DB), ERP-only shared (hostname forced to
`www.ecomae.com`, dedicated MySQL), and mixed (own hostname + dedicated MySQL). Platform
hostnames cannot be stolen. Dedicated DBs cannot reuse `docpart` / `ecomae` / `epartscart`
or another tenant. Super-CP still picks the tenant; a tenant operator never sees another
`site_key`. Remaining HTML (intro form, tenant-hub panel) and leftover portal PDO / intro
parents still block the click-through UI; those stay skipped until the parent lands.

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

### Q1 lead — done (`PhpPlanQ1Lead`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Lead`, 5 cases): AI classification rules / classify / store / batch / HS seed+lookup / stats / review, marketing page meta / JSON-LD / crumb / docs-compare-bos-solution render (content catalogs already ported; home helpers stubbed), CP top-alerts HTTPS / email / SMS state and styles. HTTPS reads the global config (the parameter is overwritten). `empty()` treats `"0"` as empty. Professional header is always on, so the header items stay blank.

### Q1 note — done (`PhpPlanQ1Note`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Note`, 2 cases): storefront seed locale defaults (portal/ERP locale parents omitted), FX rates / convert-price (`round` 0), generic + electronics/fashion/consulting/jewellery catalogs, idempotent category/product upserts. Product catalog non-AED prices divide the AED list by 3.67 then convert.

### Q1 safe — done (`PhpPlanQ1Safe`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Safe`, 6 cases): warehouse sitemap shard size / stale / meta / url xml / write / regenerate / serve / estimate (SEO price-clause and part-loc injected; storage-flag and article-match parents stay gaps), tenant data-protection access / redact / classify / retention / audit / isolation (portal tenant row/connect injected; `enforce_access` mentioned but not golden-run because it exits), customer-management h / money / dashboard / list / display / initials / orders / advances / documents / save / tab-url (finance save/VAT injected). `empty('0')` is empty. Same-second customer orders follow MySQL time DESC then id ASC; recent orders time DESC then id DESC.

### Q1 talk — done (`PhpPlanQ1Talk`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Talk`, 4 cases): order communication-test definitions / last-json / load / save / notify-row / answer-summary / record-test / ensure-customer / create-order (notify status and trade injected), storefront anti-crawl client-ip / bot / tech-key / session-user / rate-limit / enforce / deny / redact / resolve-identity (prices-visible and session user injected; do not write the leftover user-include basename). `deny` is mentioned but not golden-run because it exits. Rate-limit `count > max` blocks; `empty('0')` tech_key is empty.

### Q1 walk — done (`PhpPlanQ1Walk`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Walk`, 2 cases): multivendor min-price ACL ensure / defaults / get / save / admin-viewer / may-see / min-max row / display-storage / typed list / hide-row / customer groups. Session user stays injected. `empty('0')` restrict is open. Save keeps first-seen positive ids.

### Q1 hold — done (`PhpPlanQ1Hold`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Hold`, 4 cases): SSO SAML schema / SP metadata / provider CRUD / AuthnRequest / response / sessions / logout / expire / fleet (AuthnRequest id/instant normalized; `SUM(active)` is a MariaDB string), BOS tenant health / all / summary (tenant connect injected so the unified parent stays a gap). Connectivity fail detail is the hardcoded `Connection failed`. `round` half-up: 62.5 → 63.

### Q1 keep — done (`PhpPlanQ1Keep`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Keep`, 4 cases): industry SEO slug / presentation / host-map / primary-host / site-url / template parse / sitemap / match-request / request-host (crc32 presentation; live-bridge and groups injected so those parents stay gaps), BOC tenant-scope catalogs / session / module-url / switcher / nav-filter / href / switcher HTML (unified tenant-list and Super-CP host injected). Empty `cp_url` on a demo tenant becomes `https://www.ecomae.com/` plus the path. `htmlspecialchars` ENT_QUOTES uses `&#039;`. Live-bridge categories apply only when a template row parses empty, not on a regex miss.

### Q1 lock — done (`PhpPlanQ1Lock`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Lock`, 4 cases): industry consolidation groups / resolve / get / template-key / tenant sub-areas / save / schema / stats (portal group-map injected so that parent stays a gap; keyword order is first-match), template router path / hero / category filter / ERP modules / CP sections / savings. Last fallback is `/` + `retail` + `.php` because `$mainDir` is undefined. `empty('0')` theme primary does not override. `round` half-up: 97.573… → 97.6.

### Q1 open — done (`PhpPlanQ1Open`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Open`, 4 cases): storefront storage-flag schema / SQL fragments / disabled maps / CP rows / toggle sync / line and bunch filters (`empty('0')` price_id is empty), SKU media catalogs / normalize / profile upsert / spec groups / photos / public lookup / library search (CHPU builder injected so the article-match leftover stays a gap). Upload `add_photo` is mentioned; CLI has no uploaded file so the golden is `No upload`. Attach file names are normalized.

### Q1 view — done (`PhpPlanQ1View`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1View`, 4 cases): SKU-media CP install (lang upsert / content route / ACL copy; menu apply injected so the mainstream-menu parent stays a gap) and storefront load / spec HTML / CSS emit / gallery render. `empty('0')` hides photos. Rich spec cells stay unescaped. CSS `?v=` stamps are normalized to `MTIME`. Manager basename is concatenated.

### Q1 form — done (`PhpPlanQ1Form`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Form`, 4 cases): SKU-media manager page (session/csrf injected so the user kernel stays a gap; page-frame register stubbed) and the storefront warehouse/price toggle panel (rows from the Open twin; `empty()` disabled; `stripos` probe labels). CSS `?v=` digits are normalized to `MTIME`. Config basename is concatenated.

### Q1 text — done (`PhpPlanQ1Text`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Text`, 4 cases): stock-brand price IDs / in-stock fallback / counts+letters / match params / manufacturer parts / genuine-aftermarket tags (offices, genuine index, and synonym map injected so those parents stay their own mentions), tree-list items dump (`json_encode` flags=`JSON_HEX_TAG`; `"level"` / `"parent"` / `"count"` become `$` keys; `(bool)` on native INT `0` is false), catalogue text-search include (caption / description / article / alias / discovery-queue; parent lang fragment already mentioned). File cache TTL is 600s.

### Q1 list — done (`PhpPlanQ1List`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1List`, 4 cases): agent catalog cache path / norm key / match pattern / index build / section hint+label / stock+vehicle+model match / inquiry+list detect / resolve query / name-in-text. Stock-brand counts reuse the Text twin. File cache TTL is 21600s; in-process memo is 300s.

### Q1 scan — done (`PhpPlanQ1Scan`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Scan`, 4 cases): CloudPanel CLI bin/available fallback, provision empty/unavailable, vhost scrub/strip/audit/orphan, tenant snippets, 8080 alias add/remove, failover splash, SSL path patch. RunCmd / IsDir / IsFile / Http stay injected so goldens never hit `https://127.0.0.1:8443` or rewrite nginx. `part_search_page.php` and the auto-price engine stay open (parents).

### Q1 grow — done (`PhpPlanQ1Grow`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Grow`, 4 cases): web tracker h / country / UA / clip / uuid / duration / csv / client-ip / geo (CF header; lookup HTTP injected), site-key / beacon, range+filters, ingest (bad ids + upsert), dashboard summary + session detail.

### Q1 rise — done (`PhpPlanQ1Rise`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Rise`, 4 cases): CP sidebar lang / shop-group / ensure group+item, mainstream apply, payments / marketing / procurement / POS / customers / documents / hub / operator / integrations / portal packs, ERP+OMS cleanup, system hidden URL/label cleanup, parity registry+apply (cache bust stays unmentioned).

### Q1 peak — done (`PhpPlanQ1Peak`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Peak`, 4 cases): tenant price-id resolve (site key unused in SQL; `empty('0')` skips), assert + violation log, full audit (check2/3 WARN do not set overall WARN), client/ERP isolation, ownership, orphans, query scoping, registry credentials (Connect injected), find-php-files (broken `content/files` skip), scoped-query rewrite, get-scoped-pdo prepare (placeholders only), enforcement scan. `epc_ci_recent_violations` LIMIT bind fatals — twin takes an int; goldens SELECT instead. Admin leftover basename is concatenated.

### Q1 wave — done (`PhpPlanQ1Wave`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Wave`, 4 cases): TOTP base32 / RFC 6238, enroll / confirm / verify, backup codes, policy get/save/update (invalid JSON falls back to defaults), role/group/dept required-for-user, path guard, CP auth-gate `(string)array` → `Array` quirk, ERP finance tab gate, ajax handler. `epc_mfa_recent_activity` LIMIT bind fatals — twin takes an int; goldens SELECT instead. Clock / random stay injected. GET does not mint a session.

### Q1 tide — done (`PhpPlanQ1Tide`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Tide`, 4 cases): valid modes / health / env / splash, default+read/write config (`empty('0')` badge; poll clamp 30–300), mode file + JSON mirror, current-status TTL (stale rebuild vs cached; autoProbe with a valid mirror returns the mirror), local probe host short-circuit (HTTP injected, never a live primary), probe-authorized (token `hash_equals`; leftover deploy-auth / portal paths concatenated). GET does not mint a session.

### Q1 reef — done (`PhpPlanQ1Reef`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Reef`, 4 cases): platform/large-host gates (`isset` cleaner flag), listing rows with denormalized QTY then live COUNT fallback + persist, index helper (static once; invalid ident no-op), pyprices health (`empty('0')` status; HTTP injected). Leftover platform-hostname parent stays injected/unmentioned. GET does not mint a session.

### Q1 surf — done (`PhpPlanQ1Surf`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Surf`, 4 cases): compact money / vendor-warehouse-channel rollups (`empty('0')` ok/web), defensive collectors (missing table → zeros), injected fleet walk, tile/RAG/YN/hero + three control-room renderers. Leftover registry list/connect and marketplace channel ids stay injected/unmentioned. Kernel escape/classify/type-label mentioned; kernel file stays open. GET does not mint a session.

### Q1 drift — done (`PhpPlanQ1Drift`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Drift`, 4 cases): enroll/verify/settings HTML (`empty` backup and methods; ENT_QUOTES `&#039;`) plus the four JS helpers. QR generation is injected from the already-closed MFA helpers. GET does not mint a session.

### Q1 bay — done (`PhpPlanQ1Bay`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Bay`, 4 cases): catalog / guide / capabilities, config+report storage (upsert consumes AUTO_INCREMENT), embed resolve (platform fallback / `empty('0')` / Azure never mints a token), CSV BOM, and dataset collectors. Leftover finance export and phase-8 paths stay injected or concatenated. GET does not mint a session.

### Q1 cove — done (`PhpPlanQ1Cove`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Cove`, 4 cases): graph version / caption / URL helpers, tenant-scoped credentials and public meta, live tests (Facebook / Instagram / TikTok / LinkedIn vault-only), and publish draft/now. HTTP stays injected. Portal parents stay stubbed. GET does not mint a session.

### Q1 dock — done (`PhpPlanQ1Dock`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Dock`, 4 cases): templates / statuses / host gates / DNS, save of site-only + ERP-only shared + mixed dedicated, reserved-DB and cross-tenant DB collision, host load (inactive and platform/eParts skipped), registry by `site_key`, and runtime vs dedicated credentials. Leftover portal / intro / demo / PDO parents stay stubbed. GET does not mint a session.

### Q1 pier — done (`PhpPlanQ1Pier`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Pier`, 4 cases): enqueue (dedupe / empty payload `[]`), claim by priority, complete keeps `locked_by`, fail retry vs dead, dispatch (noop / custom / unknown / tenant health+warmup), batch. Leftover intro / tenant-PDO / blockchain / ERP-tick parents stay stubbed or unmentioned. GET does not mint a session.

### Q1 quay — done (`PhpPlanQ1Quay`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Quay`, 4 cases): missing fields, host resolve (row / DP_Config / config.php / 127.0.0.1), dedicated-db flags (`empty('0')` erp-only), from-row aliases + shared-docpart creds hook, live connect reuse / dead reconnect / user-case miss, pool eviction at max 2. Open / ping stay injectable in the twin. GET does not mint a session.

### Q1 slip — done (`PhpPlanQ1Slip`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Slip`, 4 cases): missing fields / unavailable DB, bcrypt + md5-secret + plain-md5 + `pass` alias, role deny / tenant / admin-table / allowlist / backend-group, csrf + context. Unified / upgrade / session-file / BOC audit stay injected or absent. GET does not mint a session.

### Q1 hull — done (`PhpPlanQ1Hull`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Hull`, 4 cases): Google providers (empty / client-id-only / full), HMAC state pack/unpack (tamper + 900s expiry + tenant_key isolation), Google start/verify/exchange (audience, unverified `empty('0')`, issuer, expired, HTTP inject), complete login CP vs storefront, modern HTML + second-call empty. Leftover auth-common / OTP-modal / oauth-buttons stay injected. GET does not mint a session.

### Q1 keel — done (`PhpPlanQ1Keel`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Keel`, 4 cases): admin `COUNT===1` (no-config, user-type, foreign uid, duplicate token, dropped table), ERP-only landing (empty `backend_dir` → `//shop/...`, custom `panel`, injected shell URL), run (guest roots + operator prefixes, POST `authentication` / `empty('0')`, `backend_dir` trim, demo ERP-shell then post-login, platform/client ERP, ERP-only landing skipped on platform hostname), MFA route (`?qs` null vs empty path) and ajax (slash-escaped JSON). Portal / demo / MFA parents stay injected. GET does not mint a session.

### Q1 mast — done (`PhpPlanQ1Mast`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Mast`, 4 cases): schema, key generate/validate/revoke/list (SHA-256, validate last_used is pre-UPDATE), hourly rate (`strtotime(window + ' +1 hour')`), handle (`Bearer ` case-sensitive, `site_key` from the key, empty arrays `[]`), OpenAPI / fleet COUNT int / SUM string / AVG `"0.0000"`. Clock / RNG / microtime injected. GET does not mint a session.

### Q1 helm — done (`PhpPlanQ1Helm`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Helm`, 4 cases): presets/validate (`empty('0')` mode, Gmail warnings), write (`var_export`, blank password keeps existing, chmod 0640), effective (local / file `"0"` host applies / tenant overlay / Super-CP skip), diagnose/classify/send (mailer leftover injected), demo fallback `demo_` prefix, operator store/lookup (`empty('0')` code skip). GET does not mint a session.

### Q1 yard — done (`PhpPlanQ1Yard`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Yard`, 4 cases): tiers/checks (isolation `pass`/`ok`, MFA `'1'`, backup age, einvoice modes, homepage 2000/5000, compliance `!= ''`, ERP json then `erp_enabled`, branding + injected token catalog, webhooks 0=warn), fleet live ORDER BY trade_name, `round` half-up. Token catalog leftover stays injected. GET does not mint a session.

### Q1 spar — done (`PhpPlanQ1Spar`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Spar`, 4 cases): file / method-key overlay (`empty('0')` skip), shop URL overrides, host gate, error classes, maps, injected HTTP call and umapi fallback. Leftover catalog/portal parents stay injected. GET does not mint a session.

### Q1 boom — done (`PhpPlanQ1Boom`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Boom`, 4 cases): canonical JSON, SHA-256 proofs, Merkle even/odd last-leaf duplicate, tenant-scoped record/list/fleet, mode `off` skip, `empty($opts['enqueue_anchor'])`, maybe-record enqueue default true. Leftover ERP/shared/intro parents stay injected. GET does not mint a session.

### Q1 stay — done (`PhpPlanQ1Stay`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Stay`, 4 cases): asset version inject, content-URL CSS/JS map (empty `backend_dir` → `//…`, orders GET append), ERP-url resolve + injected nav JS, head `htmlspecialchars` ENT_QUOTES, APAI tab aliases / imports filter / `discoverInlined`, empty-backend shell=`cp` / inline=`""`, site-key lower-then-strip, host fallback, VPE extras + `empty()` skip. Leftover version / ajax / host / ERP-nav parents stay injected. GET does not mint a session.

### Q1 jib — done (`PhpPlanQ1Jib`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Jib`, 4 cases): five industry themes, industry sanitize `[a-z0-9_]`, screenshot key `[a-z0-9]`, live asset URL split, PHP `?>` showcase HTML, ENT_QUOTES `&#039;`. Leftover marketing-data / home-h parents stay injected. GET does not mint a session.

### Q1 gaff — done (`PhpPlanQ1Gaff`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Gaff`, 4 cases): digits / bilingual / wa.me `rawurlencode`, sales digits from injected agent href, display `empty('0')`, site-name brand then `from_name`, order/cart/LPO/product messages, notify skip empty id/status, button ENT_QUOTES, frontend script slash-escaped JSON. Leftover agent / branding / supplier-notify parents stay injected. GET does not mint a session.

### Q1 sprit — done (`PhpPlanQ1Sprit`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Sprit`, 4 cases): brand sanitize `[a-z0-9_]`, epartscart vs ecomae profiles / sections / stats / journey, interpolating CSS heredoc, printable HTML, `empty('0')` skips auto-print, ENT_QUOTES `&#039;`. Leftover live-deck parents stay injected. GET does not mint a session.

### Q1 luff — done (`PhpPlanQ1Luff`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Luff`, 4 cases): preset path / empty / invalid JSON / custom tables (`shop_offices\`x` kept), verify missing-table catch + geo id=3 + office city `stripos` + hours LIKE `%`/`_` escape, apply incremental vs force clone + home `modules_array` + verify fail, missing source PDO + clone errors. Leftover clone parents stay injected. GET does not mint a session.

### Q1 clew — done (`PhpPlanQ1Clew`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Clew`, 4 cases): en/ar/ru copy, unknown lang falls back to en, no PDO → AE, ENT_QUOTES `&#039;`. Leftover SEO parents stay injected. GET does not mint a session.

### Q1 tack — done (`PhpPlanQ1Tack`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Tack`, 4 cases): reset, below-limit + storage sum, category-mismatch skip, empty `IN ()` skipped (PHP would emit invalid SQL). GET does not mint a session.

### Q1 vang — done (`PhpPlanQ1Vang`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Vang`, 4 cases): lang upsert, content register + group walk, install + super access copy, setup-connect empty/fallback/fail. Leftover POS helper parents stay injected. Leftover tenant-manage page basename is concatenated. GET does not mint a session.

### Q1 sheet — done (`PhpPlanQ1Sheet`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Sheet`, 4 cases): article/brand normalize, pair empty/same, count + missing-column catch, annotate/enrich, add/import + source filter + search. Leftover docpart parents stay injected. GET does not mint a session.

### Q1 halyard — done (`PhpPlanQ1Halyard`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Halyard`, 4 cases): illegal name, missing dir, delete keeps `index.html` (rmdir may fail), no DB. Shared PHP helper name is not repeated. GET does not mint a session.

### Q1 cringle — done (`PhpPlanQ1Cringle`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Cringle`, 4 cases): no rule, text caption + href args, GET/url caption + injected fetch, empty caption. Live HTTP stays injected. GET does not mint a session.

### Q1 throat — done (`PhpPlanQ1Throat`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Throat`, 4 cases): empty order/id, no CRM, CRM id, `number_format` thousands + VAT. Leftover notify/currency parents stay injected. GET does not mint a session.

### Q1 leech — done (`PhpPlanQ1Leech`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Leech`, 4 cases): probe+redirect digits concatenate to `11`/`12`/`21`/`22` (ok+redir / ok+nored / fail+redir / fail+nored). Leftover top-alert parents stay injected. Reef already owns `epc_prices_manager_perf.php` — this file is not a Reef overwrite. GET does not mint a session.

### Q1 knot — done (`PhpPlanQ1Knot`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Knot`, 4 cases): no rule keeps title, `url` title + `like_title` description, `complex` translate `%0` replace, `text_for_url` override wins. Leftover page-url / translate / HTTP parents stay injected. GET does not mint a session.

### Q1 bend — done (`PhpPlanQ1Bend`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Bend`, 4 cases): empty order/id, no customer phone, profile phone wins, LPO groups + `phone_not_auth`. Leftover WhatsApp helper parents stay injected. PHP `?>` eats the following newline. GET does not mint a session.

### Q1 wake — done (`PhpPlanQ1Wake`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Wake`, 4 cases): normalize URL, longest-prefix area resolve, Super-CP gate (deny control/login/already-open), open/close. Leftover console / portal / tenant-scope parents stay injected. GET does not mint a session.

### Q1 wind — done (`PhpPlanQ1Wind`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Wind`, 4 cases): pyprices URL, 11 channels, `cron_wget` concatenates domain+backend with no extra slash, snapshot missing cron tables = -1, health `empty()` all_ok. Leftover history-schema + HTTP stay injected. The leftover `epc_multivendor_price` + `_ingest.php` basename is concatenated so that 1,408-line sibling stays a gap. GET does not mint a session.

### Q1 sail — done (`PhpPlanQ1Sail`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Sail`, 4 cases): 6 guide titles, portal codes + healthcare→medical map, catalogue usort Auto then Jewellery, apply invalid / not found / ok + curly-quote extra. Leftover portal / theme / finance-pack parents stay injected. GET does not mint a session.

### Q1 line — done (`PhpPlanQ1Line`)

Closed against PHP 8.3.6 goldens (`Fixtures/PlanQ1Line`, 4 cases): empty article, add/dedupe + UMAPI key tail, Crossbase HTML brand parse, collect warehouse + CP crosses + synonym merge. Leftover article-match / synonym / cache parents stay injected. HTTP is stubbed. GET does not mint a session. Next unused class after Line: Stem.

Detailed area functionality: `NonErpAreaFunctionalityTests` plus `scripts/run_non_erp_area_functionality.sh` (auth, storefront commerce, CP/BOS, tenants/jobs/social, all PlanQ1). This is not human acceptance and does not close the remaining 288 files.

### Q1 next leftovers — honest schema/data twins still open

The leftover “ready” ≤200-line rows are still mostly Q2/Q3. Next honest ports (do not port industry templates — each `require`s `_base_template.php` and renders a full HTML page):

| Order | File | Why now |
|---|---|---|
| 1 | `content/social_media/epc_social_publish.php` | Closed in Cove. |
| 2 | OEM `Functions.Common.php` (124) | Third-party catalog API client — **skip**. |
| 3 | `epc_platform_jobs.php` | Closed in Pier. |
| 4 | `epc_tenant_pdo.php` | Closed in Quay. |
| 5 | `epc_bos_ajax_login.php` | Closed in Slip. |
| 6 | `epc_auth_social.php` | Closed in Hull. |
| 6b | `cp/epc_cp_auth_gate.php` | Closed in Keel. |
| 6c | `epc_rest_api_v2.php` | Closed in Mast. |
| 6d | `epc_auth_smtp.php` | Closed in Helm. |
| 6e | `epc_readiness_score.php` | Closed in Yard. |
| 6f | `epc_partsapi_config.php` | Closed in Spar. |
| 6g | `epc_blockchain_bos.php` | Closed in Boom. |
| 6h | `epc_cp_page_assets.php` | Closed in Stay. |
| 6i | `epc_ecomae_platform_tenant_showcase.php` | Closed in Jib. |
| 6j | `epc_whatsapp_share.php` | Closed in Gaff. |
| 6k | `epc_marketing_brochure.php` | Closed in Sprit. |
| 6l | `epc_demo_autoparts_bootstrap.php` | Closed in Luff. |
| 6m | `epc_seo_shipping_export.php` | Closed in Clew. |
| 6n | `product_exist_limit.php` | Closed in Tack. |
| 6o | `epc_pos_cp_install.php` | Closed in Vang. |
| 6p | `epc_cp_cross_helpers.php` | Closed in Sheet. |
| 6q | `del_tmp_folder.php` | Closed in Halyard. |
| 6r | `modules/bread_crumbs/helper.php` | Closed in Cringle. |
| 6s | `epc_order_staff_summary.php` | Closed in Throat. |
| 6t | `cp/modules/check_ssl/check_ssl.php` | Closed in Leech. |
| 6u | `plugins/metadata_handler/metadata_handler.php` | Closed in Knot. |
| 6v | `epc_order_whatsapp_share.php` | Closed in Bend. |
| 6w | `epc_boc_page_shell.php` | Closed in Wake. |
| 6x | `epc_price_upload_diagnostics.php` | Closed in Wind. |
| 6y | `epc_tenant_templates_catalog.php` | Closed in Sail. |
| 6z | `docpart_epc_article_brands.php` | Closed in Line. |
| 7 | `epc_bos_health_check.php` | Already mentioned. |
| 8 | `printProducts.php` / `printProducts_2.php` | Catalogue list parent still open — skip until that kernel. |

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
| `epc_ai_copilot.php` | BOS unified / AI parent |
| `epc_cp_breadcrumb.php` | CP content-folder DB |
| `get_alternative_bread_crumbs` | Closed in Cringle |
| `clear_dir` (`del_tmp_folder.php`) | pyprices upload parent |
| `epc_sku_media_cp_install.php`, `epc_sku_media_storefront.php` | Closed in View |
| `epc_sku_media_manager.php`, `epc_storefront_storage_panel.php` | Closed in Form |
| `epc_stock_brands_helpers.php`, `get_tree_list_items.php`, `text_search_algorithm.php` | Closed in Text |
| `epc_agent_catalog_knowledge.php` | Closed in List |
| `epc_cloudpanel_helpers.php` | Closed in Scan |
| `epc_web_tracker.php` | Closed in Grow |
| `epc_cp_mainstream_menu.php` | Closed in Rise |
| `epc_commerce_isolation.php` | Closed in Peak |
| `epc_auth_mfa.php` | Closed in Wave |
| `epc_platform_failover.php` | Closed in Tide |
| `epc_prices_manager_perf.php` | Closed in Reef |
| `epc_boc_advanced.php` | Closed in Surf |
| `epc_mfa_ui.php` | Closed in Drift |
| `epc_power_bi.php` (general_pages) | Closed in Bay |
| `epc_social_publish.php` | Closed in Cove |
| `epc_portal_tenant.php` | Closed in Dock |
| `epc_platform_jobs.php` | Closed in Pier |
| `epc_tenant_pdo.php` | Closed in Quay |
| `epc_bos_ajax_login.php` | Closed in Slip |
| `epc_auth_social.php` | Closed in Hull |
| `epc_cp_auth_gate.php` | Closed in Keel |
| `epc_rest_api_v2.php` | Closed in Mast |
| `epc_auth_smtp.php` | Closed in Helm |
| `epc_readiness_score.php` | Closed in Yard |
| `epc_partsapi_config.php` | Closed in Spar |
| `epc_blockchain_bos.php` | Closed in Boom |
| `epc_cp_page_assets.php` | Closed in Stay |
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
- Production update paste (from `main` only, PHP stays): `docs/migration/PRODUCTION_UPDATE_PASTE.md`. This branch is not production until it merges.
- Tenant isolation: no cross-tenant read or write of users, profiles, sessions, credentials, or documents. Site-only / ERP-only / mixed tenants stay classified and scoped. High-user tenants use the same boundary.
