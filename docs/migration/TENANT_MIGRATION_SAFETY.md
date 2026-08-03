# Tenant migration safety (live presentation + function)

**Policy:** Migration to ASP.NET Core must not change live tenant (or industry showcase) presentation or functionality for frontend, Control Panel, or ERP. PHP remains authoritative for product chrome on those hosts until an intentional, evidence-backed, per-host exact-route promotion with release-owner approval.

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

Overrides (not recommended for live tenants):

- `ECOMAE_CONFIRM_TENANT_HOST_SHADOW=YES` — allows an **exact-route** shadow on a tenant/industry vhost after operator review.
- `ECOMAE_CONFIRM_TENANT_PRESENTATION_SHADOW=YES` — allows presentation/login shadows on a tenant host (almost never; changes UI).

Never enable broad `location /cp`, `/erp`, `/bos`, `/api`, `/storefront`, or `/`.

## Verify live tenants were not affected

On CloudPanel (or any host with outbound HTTPS to the tenants):

```bash
bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh
# writes docs/migration/evidence/tenant-safety/live-tenant-php-chrome.json
```

Expect `status=pass`: `/`, `/CP/`, `/ERP/` on ePartsCart + dedicated tenants + platform www return PHP HTML (or PHP runtime plain text), not Blazor scaffolds or digest JSON.

Optional industry showcase hosts: `ECOMAE_INCLUDE_INDUSTRY_PROBE=1 bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh` (some industry CP pages return PHP license/`domain_path` text — not an ASP.NET cutover).

Optional: scan a site conf for broad cutovers:

```bash
python3 scripts/ecomae_nginx_site_safety.py /etc/nginx/sites-enabled/epartscart.com.conf --scan-broad
```

## Operator rules

1. Default `ECOMAE_NGINX_SITE_CONF=/etc/nginx/sites-enabled/www.ecomae.com.conf`.
2. Do not point shadow installers at epartscart / electronicae / stylenlook / taxofinca / industry `*.ecomae.com` confs.
3. Do not remove PHP-FPM, PHP cron, or PHP rewrites for tenant sites.
4. Weighted Zero-PHP meter ≠ “tenants cut over.”
5. `ReadyToRemovePhp` stays false without human `RELEASE_OWNER_APPROVAL.md` — do not invent approval.

## Related

- `docs/migration/LIVE_SURFACE_LINKS.md`
- `docs/migration/CHROME_PARITY_GAP_MATRIX.md`
- `docs/migration/PHP_VS_ASPNET_DETAILED_RECHECK.md`
- `GET /migration/live-surface-links`
