# Tenant migration safety (live presentation + function)

**Policy:** Migration to ASP.NET Core must not change live tenant (or industry showcase) presentation or functionality for frontend, Control Panel, or ERP. PHP remains authoritative for product chrome on those hosts until an intentional, evidence-backed, per-host exact-route promotion with release-owner approval.

## Absolute presentation lock (named live tenants)

These **live production tenants** must keep storefront, Control Panel, and ERP **identical to PHP** — no compromise on presentation:

| Tenant | Primary hosts |
| --- | --- |
| ePartsCart | `epartscart.com`, `www.epartscart.com` |
| Electronicae | `www.electronicae.com`, `electronicae.com` |
| StyleNLook | `www.stylenlook.com`, `stylenlook.com` |
| The Jewellery Trend | `www.thejewellerytrend.com`, `thejewellerytrend.com` |
| Taxofinca | `www.taxofinca.com`, `taxofinca.com` |

**Must match PHP with zero perceptible difference:**

- Theme and colouring  
- Layout / structure / menus  
- Fonts and typography  
- Hero / splash / slider / banner media  
- Every field and widget on storefront, CP, and ERP  

ASP.NET hybrid (`/cp/app`, `/erp/app`, `/bos/app`, `/storefront/app`, digests) is **www.ecomae.com only**. Installers **hard-refuse** presentation and exact-route shadows on these named live tenant vhosts — `ECOMAE_CONFIRM_TENANT_*` cannot override.

Classifier / refuse law: `scripts/ecomae_nginx_site_safety.py` (`LIVE_PRODUCTION_TENANT_*`).

## Same-to-same / invisible migration (hard law)

**Operator mandate:** None of our tenants may feel there is a change from PHP to ASP.NET Core. UI/UX for frontend, backend, Control Panel, ERP, BOS, and storefront must be **same-to-same** and work the same way — no one should be able to identify or feel any change.

| Rule | Meaning |
| --- | --- |
| Product chrome stays PHP | Live `/`, `/CP/`, `/ERP/`, `/BOS/`, cart/checkout/search on tenant hosts remain PHP until dual-sample + human `RELEASE_OWNER_APPROVAL.md`. |
| Digests ≠ UX | JSON digests and Blazor `/cp|/erp|/bos|/storefront|/marketing/*-app` previews on **www.ecomae.com** are migration scaffolding only. They must **never** replace tenant product chrome or live marketing `/`. |
| Exact-route only | Never broad `location /`, `/cp`, `/erp`, `/bos`, `/api`, `/storefront` on tenant vhosts. |
| Look & feel | Fonts, CSS/JS class trees, heroes, animations, menus, and interactive flows must match live PHP (or stay on PHP). |
| Meter ≠ cutover | Weighted Zero-PHP % does **not** mean tenants are cut over. |

## What stays PHP on every live tenant

| Surface | Paths | Stack |
| --- | --- | --- |
| Storefront / marketing | `/` and all non-allowlisted pages | PHP |
| Control Panel | `/CP/`, `/cp/` (pages, menus, plugins) | PHP |
| ERP | `/ERP/`, `/erp/` tab UIs | PHP |
| BOS (if exposed) | `/BOS/`, `/bos/` module UX | PHP |
| Cart / checkout / search | storefront commerce flows | PHP |

ASP.NET on **www.ecomae.com** only may serve allowlisted exact-routes (`location =`): health, migration diagnostics, catalog/price API shadows, surface/storefront digests, and optional Blazor **preview** paths (`/cp/app`, etc.). Those previews are **not** the live tenant UI.

## Installer hard guards

Shadow installers refuse non-www site confs by default:

| Script | Guard |
| --- | --- |
| `cloudpanel_install_exact_route_shadow.sh` | `ecomae_assert_nginx_shadow_target_allowed … exact-route` |
| `cloudpanel_install_surface_digest_shadows.sh` | same |
| `cloudpanel_install_storefront_digest_shadows.sh` | same |
| `cloudpanel_install_presentation_app_shadows.sh` | `… presentation` (tenant/industry hard-refuse) |

Classifier: `scripts/ecomae_nginx_site_safety.py`  
Bash helper: `scripts/lib/ecomae_nginx_site_safety.sh`

Overrides:

- `ECOMAE_CONFIRM_TENANT_HOST_SHADOW=YES` — allows an **exact-route** shadow on a **non-named** tenant/industry vhost after operator review. **Does not apply** to named live production tenants above.
- `ECOMAE_CONFIRM_TENANT_PRESENTATION_SHADOW=YES` — allows presentation/login shadows on a **non-named** tenant host (almost never). **Cannot override** named live production tenants.

Never enable broad `location /cp`, `/erp`, `/bos`, `/api`, `/storefront`, or `/`.

## Verify live tenants were not affected

On CloudPanel (or any host with outbound HTTPS to the tenants):

```bash
# Preferred operator entry (tenant chrome + forbidden ASP.NET paths + BOS spot-check):
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh
# -> docs/migration/evidence/tenant-safety/live-tenant-php-chrome.json
# -> docs/migration/evidence/tenant-safety/same-to-same-verify.json

# Lower-level chrome + presentation fingerprint probe only:
bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh
```

Expect `status=pass`:

1. `/`, `/CP/`, `/ERP/` on named live tenants return PHP HTML with presentation fingerprints (stylesheets, hero/splash on storefront, epc/bootstrap/font-awesome on CP/ERP).  
2. `/cp/app`, `/erp/app`, `/storefront/app`, `/health`, digest routes on those hosts are **not** ASP.NET (typically **404**).  
3. BOS spot-check on selected hosts stays non-ASP.NET.

Optional industry showcase hosts: `ECOMAE_INCLUDE_INDUSTRY_PROBE=1 bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh`.

Optional: scan a site conf for broad cutovers:

```bash
python3 scripts/ecomae_nginx_site_safety.py /etc/nginx/sites-enabled/epartscart.com.conf --scan-broad
```

## Operator rules

1. Default `ECOMAE_NGINX_SITE_CONF=/etc/nginx/sites-enabled/www.ecomae.com.conf`.
2. Do not point shadow installers at epartscart / electronicae / stylenlook / taxofinca / thejewellerytrend / industry `*.ecomae.com` confs.
3. Do not remove PHP-FPM, PHP cron, or PHP rewrites for tenant sites.
4. Weighted Zero-PHP meter ≠ “tenants cut over.”
5. `ReadyToRemovePhp` stays false without human `RELEASE_OWNER_APPROVAL.md` — do not invent approval.
6. If a tenant ever looks different from PHP — restore PHP vhost immediately; do not “fix” with ASP.NET chrome.

## Related

- `docs/migration/LIVE_SURFACE_LINKS.md`
- `docs/migration/CHROME_PARITY_GAP_MATRIX.md`
- `docs/migration/PHP_VS_ASPNET_DETAILED_RECHECK.md`
- `docs/migration/OVERALL_PROGRESS_REPORT.md`
- `GET /migration/live-surface-links`
