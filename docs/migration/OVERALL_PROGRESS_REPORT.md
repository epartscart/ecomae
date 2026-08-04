# Overall progress report — PHP → ASP.NET Core

**As of:** 2026-08-04 (ERP/BOS topnav/dashboard wave on main + live tenant absolute presentation lock)
**Locks:** `cutoverAllowed=false` · `readyForPhpRemoval=false` · interactive ASP.NET complete **0**

## Scorecard

| Area | Wired | Live on www.ecomae.com | Honest % |
| --- | ---: | ---: | ---: |
| Catalog digest-contract | **725 / 726** | Catalog APIs allowlisted | **~99.9%** contract (holdout: `cp-debug-console` php-only) |
| Surface digests (CP/ERP/BOS) | **127 / 127** | **127 / 127** (`401` auth gate) | **100%** shadow live |
| Storefront digests | **6 / 6** | **6 / 6** (`401`) | **100%** shadow live |
| Presentation apps / shells | **145** | **142 / 145** (`200`; +`/marketing/app` pending shadow install) | **~98%** shadows live |
| Hybrid TARGETS | **134** | Sample apps + shells live | Digests/UI wired; interactive still PHP |
| Field contracts | ~153 | Probe attached | Contract floor only |
| Chrome **look** parity (fonts/color/width/motion) | Hybrid assets + ERP/BOS mega-nav structure | Improving on www hybrid | **~70–75%** look (topnav structure closer; not pixel-identical) |
| ERP/BOS dashboard field digests | PHP erp_dashboard + CC tiles + Fleet Command counts | Awaiting redeploy | Contract expanded; dual-sample still pending |
| Interactive module parity (menus/forms/writes) | Digests read-only | PHP authoritative | **~0%** full interactive |
| Tenant same-to-same (5 live tenants) | ASP.NET hard-refused on vhosts | PHP storefront/CP/ERP; `/cp/app`+`/erp/app`+`/health` **404** | **Locked** |

Weighted Zero-PHP meter remains **95% / 5%** (decommission residual) — **not** “95% of UX cut over.”

## What is done

1. **#775** tip → `main` (waves through compare board)
2. CloudPanel ASP.NET redeploy unblocked (**#776**)
3. Digest nginx `:5080` → `:5100` (**#777**); live sed repair → **PASS=127 FAIL=0**
4. Presentation app shadows live (~142/144)
5. Human compare board: `/migration/compare`
6. Presentation look (#778): Super CP login PHP class tree; BOS particles/counters visual-only; desktop width ~1480/1400
7. ERP/BOS topnav (#779): area-column mega panels (`epc_erp_render_top_nav`); BOS explicit `epc_bos_*_items` maps + white panels; ERP/BOS dashboard digests match PHP executive + command-center / Fleet Command fields
8. This wave: **absolute presentation lock** for epartscart / electronicae / stylenlook / thejewellerytrend / taxofinca — installers hard-refuse ASP.NET shadows; probe checks PHP fingerprints + forbidden hybrid paths (`GET /migration/live-tenant-presentation-lock`)

## What is still PHP-authoritative

- Named live tenants (storefront + CP + ERP): presentation identical to PHP — no compromise
- Product chrome: `/`, `/CP/`, `/ERP/`, `/BOS/`, tenant storefronts
- All writes, full menus, OMS/ERP tabs, BOS native `$_SESSION` modules
- Checkout, cart qty, social login, rate-limit, shared-ERP picker
- `cp-debug-console` intentionally not ported (LFI risk)

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
