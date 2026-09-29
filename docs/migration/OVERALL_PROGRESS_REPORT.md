# Overall progress report — PHP → ASP.NET Core

**As of:** 2026-09-29 (target reframed: **100% ASP.NET / 0 PHP**; PHP-primary = parity gate only)
**Locks:** `cutoverAllowed=false` · `readyForPhpRemoval=false` · interactive ASP.NET complete **0**
**Path board:** `GET /migration/aspnet-zero-php-path` · `docs/migration/ASPNET_ZERO_PHP_PATH.md`

## Scorecard

| Area | Wired | Live on www.ecomae.com | Honest % |
| --- | ---: | ---: | ---: |
| Catalog digest-contract | **726 / 726** | Catalog APIs allowlisted | **100%** contract (cp-debug-console safe metadata-only) |
| Surface digests (CP/ERP/BOS) | **129 / 129** | **129 / 129** (`401` auth gate) | **100%** shadow live |
| Storefront digests | **7 / 7** | Live auth-gate may lag install (`401`) | **100%** wired (incl. checkout) |
| Presentation apps / shells | **145** | **142 / 145** (`200`; +`/marketing/app` pending shadow install) | **~98%** shadows live |
| Marketing ASP.NET scaffold | `/marketing/app` epm-hub | Pending nginx shadow | Replacement path started |
| Hybrid TARGETS | **134** | Sample apps + shells live | Digests/UI wired; interactive still PHP |
| Field contracts | ~153 | Probe attached | Contract floor only |
| Chrome **look** parity (fonts/color/width/motion) | Hybrid assets + ERP/BOS mega-nav + marketing hub | Improving on www hybrid | **~70–75%** look |
| Interactive module parity (menus/forms/writes) | CP 254 + ERP 321 + BOS 231 dry-run goldens (`writes=0`) | PHP-primary until field dual-sample | **~0%** interactive; **100%** dry-run catalog |
| Tenant cutover path (5 live) | Parity-gate refuse-by-default; unlock via `ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW` | PHP-primary today | **Gate open — not permanent PHP** |
| Zero-PHP end-state readiness | Phases 1–2 done; 3–4 in progress (marketing scaffolds + write dry-runs + write-dryrun dual-sample operator); 5–6 blocked | — | **~55%** honest (see path board) |

Weighted Zero-PHP meter remains **95% / 5%** (decommission residual) — **not** “95% of UX cut over.”

## Cross-surface audit checkpoint

The next audit confirms that CP and frontend are not formally complete even
though ASP.NET has a broad page inventory. The repository currently contains
331 ASP.NET presentation pages, while the PHP reference still contains 609 CP
module files and 188 files under the primary ERP module tree, plus
the wider storefront, marketing, BOS, API, and worker surfaces. File and route
counts are inventory evidence only; they do not prove that the ASP.NET page has
the PHP table, field, action, validation, workflow, permission, report, or
tenant-data behaviour.

The honest cross-surface status is:

- **CP:** broad ASP.NET twins exist, but the CP exit gate remains open for
  generated-menu reconciliation, remaining digest/missing pages, single-item
  workflows, write parity, presentation diffs, tenant/super-CP scope, and the
  combined browser round.
- **Frontend/storefront:** catalogue, account, cart, checkout, customer,
  vendor, vehicle, industry, and payment routes exist as ASP.NET shadows, but
  live product-host ownership remains PHP-primary until theme/assets,
  search latency, callbacks, guest ordering, customer workflows, SEO, and
  tenant-host dual samples pass.
- **Marketing:** the ASP.NET marketing page family is substantial, but
  `/marketing/app`, contact/demo forms, sitemap/SEO, brand-host probes, and
  human same-to-same approval remain deployment gates.
- **Tenant CP/ERP, demo, BOS, APIs, workers:** route and digest coverage is
  ahead of production-operational parity. Isolation, country profiles,
  provisioning, expiry, synchronization, retries, backups, restore, and
  rollback evidence remain required.

Therefore the current measured headline stays **20.4% weighted phase
completion / 79.6% pending** in the tracker, while the separate shadow
inventory remains approximately **98% presentation routes live** and
**0% formally accepted interactive migration**. These figures must not be
merged into a single completion percentage.

The area-by-area pending percentages, execution order, and session bands are
maintained in `docs/migration/ASP_NET_COMPLETION_ROADMAP.md`. They are planning
estimates only; the strictest unresolved parity or production gate controls
cutover.

## Owner-supplied PHP ERP screenshot audit

The owner supplied a 31-screenshot PHP reference pack on 2026-09-29 covering
VAT/CT/e-invoice, external audit, document control, insurance, accounting and
tenant setup, print design, automation, document formats, HR/payroll, order
planning, PIM, customs/shipping, fixed assets, CRM, fulfilment, AR/AP, landed
cost, and process flow. The pack confirms that the PHP ERP is a mature
module-body and workflow system, not just a menu and shared shell.

The detailed classification and remediation gates are in
`docs/migration/PHP_ASPNET_ERP_VISUAL_PARITY_AUDIT_2026-09-29.md`. The honest
interpretation is unchanged but now evidence-backed:

- route/catalog and shell coverage are materially ahead of interactive module
  parity;
- module-specific fields, dense tables, report layouts, action panes,
  validation/error states, workflow diagrams, and source-detail links remain
  incomplete across the supplied areas;
- production ERP ownership is still mixed: `/erp/login` is ASP.NET-primary,
  while `/erp/` remains PHP-backed;
- the legacy CP finance ERP URL redirects from PHP compatibility handling to
  the ASP.NET CP, but that redirect does not prove the original ERP module is
  ASP.NET-native;
- no score or decommission meter should increase from route or screenshot
  shell coverage alone.

## What is done

1. **#775** tip → `main` (waves through compare board)
2. CloudPanel ASP.NET redeploy unblocked (**#776**)
3. Digest nginx `:5080` → `:5100` (**#777**); live sed repair → **PASS=127 FAIL=0**
4. Presentation app shadows live (~142/144)
5. Human compare board: `/migration/compare`
6. Presentation look (#778): Super CP login PHP class tree; BOS particles/counters visual-only; desktop width ~1480/1400
7. ERP/BOS topnav (#779): area-column mega panels; dashboard digests expanded
8. Live-tenant safety (#780): same-to-same probes — now reframed as **parity gate** (not forever PHP)
9. This wave: marketing `/marketing/app` + unlock path `ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW` + `GET /migration/aspnet-zero-php-path`

## What is still PHP-primary (until ASP.NET parity cutover)

- Named live tenants (storefront + CP + ERP) — same-to-same required during migration
- Product chrome: `/`, `/CP/`, `/ERP/`, `/BOS/`, tenant storefronts
- All writes, full menus, OMS/ERP tabs, BOS native `$_SESSION` modules
- Checkout, cart qty, social login, rate-limit, shared-ERP picker
- Security headers + response compression (UX-neutral speed/security)

## Look / presentation status by area

| Area | Fonts/CSS | Width fitness | Graphics / animation | Verdict |
| --- | --- | --- | --- | --- |
| CP login (`/cp/login`) | PHP login + hero CSS | Super CP centered 440px (matches PHP `--super`) | Particles + hub orbit | Strong hybrid |
| ERP login (`/erp/login`) | PHP login CSS + hub | Wide hero panel | Hub orbit | Good; not full PHP ERP router page |
| BOS login (`/bos/login`) | `epc_bos_shell.css` | Full-bleed PHP shell | Particles + rings + counters (visual JS) | Strong hybrid |
| CP/ERP/BOS `*-app` chrome | PHP admin/BOS CSS | Widened ~1480/1400 fluid | ERP area columns + BOS white mega + Open first | Shell structure closer to PHP; module body still PHP iframe/deeplink |
| Storefront app | Modex + spareparts CSS | ~1280 container | Piston banner | Partial vs live epartscart.com |
| Tenant ePartsCart | PHP only | PHP | PHP 3D/parts | Must stay PHP |

## Operator compare links

| | PHP | ASP.NET hybrid (www) |
| --- | --- | --- |
| CP | https://www.ecomae.com/CP/ | https://www.ecomae.com/cp/login · `/cp/app` |
| ERP | https://www.ecomae.com/ERP/ | https://www.ecomae.com/erp/login · `/erp/app` |
| BOS | https://www.ecomae.com/BOS/ | https://www.ecomae.com/bos/login · `/bos/app` |
| Tenant | https://epartscart.com/ | Compare ASP.NET only on www |
| Board | — | https://www.ecomae.com/migration/compare |

## Path to “complete” (honest)

1. **Look** — continue login/desktop/storefront chrome toward PHP class trees (this PR + next waves)  
2. **Function** — dual-sample + per-module interactive ports (large; interactive stays 0 until human MODULE_FUNCTION_TEST_PASS)  
3. **Gate** — human `RELEASE_OWNER_APPROVAL.md` before any PHP removal  
4. Never broad `/cp|/erp|/bos|/storefront` cutover; never tenant ASP.NET cutover without explicit confirm

## Related

- `docs/migration/LIVE_SURFACE_LINKS.md`
- `docs/migration/CHROME_PARITY_GAP_MATRIX.md`
- `docs/migration/evidence/presentation/HUMAN_COMPARE_BOARD.md`
