# PHP vs ASP.NET detailed recheck (2026-08-03)

**Verdict: NOT ready to remove PHP. Live ASP.NET product chrome does not present or function like PHP.**

This recheck covers fonts, analytics, full-page presentation, and module functionality. Weighted Zero-PHP meter (~95%) is **not** chrome/module parity.

**Batch 1 ✅ (#658):** `PhpSurfaceHead` puts PHP webfonts/CSS/GA4 into `<head>` for login/shell routes; probe uses chrome-asset markers.

**Batch 2 ✅ (#659):** Authenticated desktop chrome (`Php*DesktopChrome`) emits PHP structural selectors. Module bodies remain PHP via hybrid workspace.

**Batch 3 ✅ (#660):** Login bridge hardening + graphical login/hero parity (hub orbit, BOS particles, piston banner).

**Batch 4 ✅ (#661–#665):** OMS · Users/Groups · ERP SO · Search · Cart hybrid read UIs. Checkout/qty/guest cart remain PHP.

**Batch 5 ✅ (#666–#667):** Miss harness + outbound-blocked miss-fill dry-run. Live UMAPI fills stay PHP.

**Same-to-same law ✅ (#668):** tenants must not feel PHP→ASP.NET. Product chrome stays PHP; digests/previews never replace tenant UX.

**Continuing:** ERP cash-entries Blazor ledger UI (`/erp/cash-entries-app`) on www preview only; PHP erp_tabs_cash_bank remains authoritative. BOS/CP digest apps and prior slices shipped/tracked. Live CloudPanel cookie captures remain operator work. Batch 6 decommission remains **blocked**. Still never `readyForPhpRemoval`. Same-to-same tenant chrome = PHP until exact-route + dual-sample + human `RELEASE_OWNER_APPROVAL.md`.

## Live probe snapshot (www.ecomae.com / epartscart.com)

| Surface | PHP URL | PHP bytes | ASP.NET URL | ASP.NET bytes | Live title (ASP.NET) |
| --- | --- | ---: | --- | ---: | --- |
| CP | `/CP/` | ~30 KB | `/cp/login`, `/cp/app` | ~7–11 KB | still “EcomAE · Zero-PHP Console” on public |
| ERP | `/ERP/` | ~170 KB | `/erp/login`, `/erp/app` | ~7–10 KB | console title |
| BOS | `/BOS/` | ~32 KB | `/bos/login`, `/bos/app` | ~6–7 KB | console title |
| Storefront | epartscart.com `/` | ~507 KB | `/storefront/app` | ~7 KB | console title |

Public readiness: `readyToRemovePhp=false`, status `blocked-not-ready-for-php-removal`.

PR #654 (PhpChromeLayout + login redirects + PHP-matching login landings) was **not** on public www at probe time — live still shows MigrationConsole chrome (Sora/IBM Plex) around product shells.

## 1. Fonts / text style

| Surface | PHP authoritative | Live ASP.NET | Gap |
| --- | --- | --- | --- |
| CP login | Homer **Open Sans** + FA4 + login/hero CSS | Sora + IBM Plex Mono (console layout) + Segoe UI inline | **Fail** — wrong typeface family and hierarchy |
| CP desktop | Open Sans / system chrome; super-CP Fraunces+Sora for BOC only | N/A (no full desktop) | **Fail** — no authenticated desktop |
| ERP | Open Sans / Segoe / Fraunces+Sora (premium) | Sora console + Segoe inline | **Fail** |
| BOS | Inter/JetBrains declared; FA4; dark shell | Sora console wrap | **Fail** |
| Storefront | **PT Sans** + Lato/Open Sans/Muli/Oswald imports | Console Sora + partial PT Sans inline | **Fail** — missing storefront webfont set |

## 2. Analytics

| Surface | PHP | ASP.NET | Gap |
| --- | --- | --- | --- |
| Storefront | GA4 `G-J19D1KHXCG` + GTM + optional Clarity | none | **Fail** |
| CP / ERP / BOS login/chrome | no gtag in shells | none | OK (match) |
| Web tracker / Meta / TikTok pixels | PHP CP “Web tracker” admin | not ported | **Fail** (admin module) |

## 3. Full-page presentation

| Surface | PHP | ASP.NET hybrid | Gap |
| --- | --- | --- | --- |
| CP unauth | Full login hero (particles, badge, card, features, footer) | Tiny scaffold / wrong console chrome on live | **Fail** |
| CP auth | `desktop.php`: header + mega/top nav + sidebar + widgets + ACL menus | `/cp/app` digest KPIs + PHP deep-links only | **Fail** — no widgets/menus/content |
| ERP unauth | Dark marketing landing + stats + module cards + sign-in (~170 KB) | Tiny scaffold | **Fail** |
| ERP auth | Full ERP topnav + area/tab workspace (~160 tab UIs) | Digest KPIs + links to PHP areas | **Fail** |
| BOS unauth | Split BOS login + role toggle | Tiny scaffold | **Fail** |
| BOS auth | Topnav mega-panels + tenant switcher + module content (`$_SESSION`) | Digest KPIs only; cookie model mismatch | **Fail** |
| Storefront home | Full modex: topbar, header, search tabs, hero video, AI widget, sticky cart (~507 KB) | Thin hero preview | **Fail** |

## 4. Functionality — module inventory (PHP-only for real UX)

| Area | Approximate count | ASP.NET today |
| --- | ---: | --- |
| CP brochure features | **405** | 0 interactive modules (digests + deep-links only) |
| ERP tab UI files (`erp_tabs_*.php`) | **160** | 0 tab UIs |
| BOS module IDs | **~116** | 0 module UIs (`$_SESSION` gap) |
| Storefront commerce | search, cart, checkout, garage, Laximo, account writes | digests only (account/orders/garage/profile JSON) |
| Catalog API | UMAPI fills + PHP | 18/18 cache readers live; misses stay PHP |
| Surface digests | — | 30/30 CP/ERP/BOS + 4/4 storefront JSON |

**Conclusion:** Digests/APIs ≠ product functionality. Operators must exercise each PHP module (orders, users, finance tabs, BOS fleet, cart/checkout) against ASP.NET before any PHP removal — today ASP.NET has no replacement UI for those modules.

## 5. What must be true before PHP removal

1. Live ASP.NET chrome matches PHP fonts, layout, analytics (side-by-side evidence).
2. Authenticated CP/ERP/BOS full desktops (or intentional Enterprise BOS replacement) with module functional tests passing.
3. Storefront cart/checkout/search parity or explicit product decision to keep PHP storefront.
4. Dual-sample + staging smoke + human `RELEASE_OWNER_APPROVAL.md` (never invent).
5. `ReadyToRemovePhp=true` from `/migration/php-decommission-readiness`.

Until then: **keep PHP authoritative**. Hybrid previews are migration aids only.

## Operator recheck commands

```bash
bash scripts/cloudpanel_probe_php_presentation_parity.sh
# optional after redeploy of presentation-match branch:
ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES \
  bash scripts/cloudpanel_install_presentation_app_shadows.sh
```

Evidence output: `docs/migration/evidence/presentation/php-vs-aspnet-recheck.json`
