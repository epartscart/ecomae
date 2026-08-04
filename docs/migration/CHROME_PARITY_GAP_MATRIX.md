# Chrome parity gap matrix (PHP ↔ ASP.NET)

Authoritative product chrome remains **PHP** until intentional exact-route cutover + dual-sample evidence + human `RELEASE_OWNER_APPROVAL.md`. This matrix tracks hybrid strengthen work so CP (platform + tenants), ERP, BOS, and login present/work without removing PHP.

**Same-to-same / absolute presentation lock:** epartscart, electronicae, stylenlook, thejewellerytrend, taxofinca must keep storefront + CP + ERP **identical to PHP** (theme, colour, structure, fonts, hero/splash, fields). Digests/Blazor previews on www never replace live tenant UX. Verify: `bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh`. Catalog: `GET /migration/live-tenant-presentation-lock`.

**Full parity plan:** `docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md` — Batch 0 ships complete PHP-sourced module directories on `/cp|/erp|/bos|/storefront/app` (`GET /migration/php-module-catalog`).

## Status legend

| Status | Meaning |
| --- | --- |
| PHP-live | Public HTML/UX still PHP |
| ASP.NET exact-route | nginx `location =` → Kestrel |
| Hybrid | ASP.NET shell/nav + PHP modules for unported work |
| Gap | Known missing parity — do not cut over |

## Surfaces

| Surface | PHP URL | ASP.NET preview / bridge | Presentation | Functionality | Gap notes |
| --- | --- | --- | --- | --- | --- |
| Platform CP chrome | `/CP/` (login when unauth) | `/cp/login` → `/cp/app` · `/cp/orders` · `/cp/users-app` · `/cp/groups-app` | Hybrid Batch 2/4: `PhpCpDesktopChrome` + OMS/users/groups read UIs | Digests ASP.NET; writes PHP | Full user/group managers + OMS console still PHP; not cut over |
| Tenant CP chrome | `tenant/CP/` | same `/cp/app` when shadowed on tenant vhost | Hybrid | Tenant DB via registry | Per-tenant shadow install still operator-driven; keep PHP chrome |
| CP login | `/CP/` auth plugin | `/cp/login` + `POST /auth/login/admin` | PHP class tree (`epc-cp-login-shell` / `--super` centered) + hub orbit + particles + login hero CSS | Batch 3: PHP-compatible `sessions` + `admin_*` when `SecretSuccession` set; cookie dual-sample harness | Rate-limit / social / demo / shared-ERP picker still PHP |
| Platform ERP | `/ERP/` | `/erp/app` · SO/PO/PUR/INV/CASH/COA/GL/WH/SUP `*-app` | Hybrid: `PhpErpDesktopChrome` area-column mega panels (`epc_erp_render_top_nav`) + procure/finance read UIs | Digests ASP.NET (erp_dashboard + command-center field set); writes PHP | Full tab UIs / ajax_erp still PHP; tenant chrome PHP |
| Platform CP | `/CP/` | `/cp/app` · orders · users/groups/modules/pages/menus/tenants/currencies/storages/admin-sessions/api-clients/config-items `*-app` | Hybrid: `PhpCpDesktopChrome` + registry/OMS/CMS/portal/finance/logistics/session/API/config read UIs | Digests ASP.NET; writes PHP | Content/menu/tenant/currency/storage/user/API/config managers still PHP; tenant chrome PHP |
| Client / platform ERP routers | `/cp/client-erp/…`, `/cp/platform-erp/` | link via PHP | PHP-live | PHP | Not cut over |
| ERP login | ERP routers / CP plugin | `/erp/login` | Bridge UI | Same admin session mint (Batch 3 dual-sample) | Shared-tenant picker still PHP |
| Super BOS | `/BOS/` | `/bos/app` · `/bos/audit-log-app` | Hybrid: `PhpBosDesktopChrome` white mega panels + explicit `epc_bos_*_items` section maps + Open first | Digests ASP.NET (Total/Commerce/ERP Only/Demo); audit append PHP | **DECISION:** `/BOS/` PHP-authoritative — admin cookies ≠ `$_SESSION` |
| BOS login | `POST /bos/?action=login` | `/bos/login` (admin bridge) | PHP `.bos-login` shell + visual-only particles/counters (no PHP form hijack) | Admin cookies for digests/`/bos/app` only | Native BOS PHP session login remains required for modules |
| Storefront home | `epartscart.com/` | `/storefront/app` · search/cart/orders/garage/profile/account-summary `*-app` | Hybrid: modex chrome + piston + account-family read UIs | Digests ASP.NET; search/cart/orders/garage/profile/account-summary read | Live slider/media + qty/guest cart/checkout + order detail + garage/profile edits + part_search tabs stay PHP |
| Storefront login | PHP customer login | `/storefront/login` | Bridge UI | Batch 3: customer token `md5(contact+userId+time+secret)` + `last_activiti_time` | Full account UX PHP |
| Digests CP/ERP/BOS | n/a (JSON) | **127/127** exact-routes live (`401`) | n/a | Live auth gate | Contract dual-sample floor attached |
| Storefront digests | n/a | **6/6** live (`401`) | n/a | Live auth gate | Customer cookie needed for 200 payloads |
| Presentation apps | n/a | **142/144** live (`200`) | Hybrid shells | Read UIs | 2 residual non-200 (auth POST / edge) |
| Catalog API | PHP UMAPI fill | 18/18 exact-routes | n/a | Live cache readers | Miss → ASP.NET 404 `cache_miss`; live fill PHP (Batch 5 harness) |
| Hybrid UI dual-sample | PHP product paths | `*-app` + `/cp/orders` www previews | Blazor SSR markers + PHP deeplinks | Contract stubs + compare harness (`hybrid-ui-dual-samples/`) | Live cookie captures pending CloudPanel; cutoverAllowed=false |

## Strengthen rules (do not violate)

1. Never invent `RELEASE_OWNER_APPROVAL.md`.
2. Never broad-cutover `/`, `/cp`, `/erp`, `/bos`, `/storefront`.
3. Hybrid nav must link PHP for unported modules — presentation must not orphan functionality.
4. Login bridge requires `EcomAE__SecretSuccession` (= PHP `secret_succession`) in `/etc/ecomae-aspnet/platform.env`; without it, UI points to PHP login.
5. Password hash upgrade stays PHP-authoritative (ASP.NET verifies only).

## Operator install (presentation + login shadows)

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
cd /opt/ecomae-aspnet-source
git fetch origin main && git checkout -f main && git reset --hard origin/main
bash scripts/cloudpanel_find_and_redeploy.sh
# Optional for login writes:
#   echo 'EcomAE__SecretSuccession=<php secret_succession>' >> /etc/ecomae-aspnet/platform.env
ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES \
  bash scripts/cloudpanel_install_presentation_app_shadows.sh
```

## Side-by-side review

| Compare | PHP | ASP.NET |
| --- | --- | --- |
| CP | `/CP/` | `/cp/app` + `/cp/login` |
| ERP | `/ERP/` | `/erp/app` + `/erp/login` |
| BOS | `/BOS/` | `/bos/app` + `/bos/login` |
| Storefront | `https://epartscart.com/` | `/storefront/app` + `/storefront/login` |
| Parity board | — | `/migration/presentation-parity` |
