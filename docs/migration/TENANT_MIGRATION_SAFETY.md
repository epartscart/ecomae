# Tenant migration safety (parity gate → 100% ASP.NET)

**Target end-state:** 100% ASP.NET Core / 0 PHP.

**Policy during migration:** Live tenant (and industry showcase) presentation/functionality must stay **same-to-same** with today’s PHP UX — tenants must not feel a stack change. PHP is **primary until ASP.NET parity**, not forever.

## Named live tenants (parity gate)

| Tenant | Primary hosts |
| --- | --- |
| ePartsCart | `epartscart.com`, `www.epartscart.com` |
| Electronicae | `www.electronicae.com`, `electronicae.com` |
| StyleNLook | `www.stylenlook.com`, `stylenlook.com` |
| The Jewellery Trend | `www.thejewellerytrend.com`, `thejewellerytrend.com` |
| Taxofinca | `www.taxofinca.com`, `taxofinca.com` |

**Same-to-same requirements while PHP-primary:**

- Theme and colouring  
- Layout / structure / menus  
- Fonts and typography  
- Hero / splash / slider / banner media  
- Every field and widget on storefront, CP, and ERP  

Default: ASP.NET digests/hybrid stay on **www.ecomae.com**. Named live tenant vhosts **refuse** shadows unless unlocked for parity work.

Classifier: `scripts/ecomae_nginx_site_safety.py`  
Catalog: `GET /migration/live-tenant-presentation-lock`  
Path board: `GET /migration/aspnet-zero-php-path` · `docs/migration/ASPNET_ZERO_PHP_PATH.md`

## Same-to-same / invisible cutover (hard law)

| Rule | Meaning |
| --- | --- |
| PHP-primary until parity | Live `/`, `/CP/`, `/ERP/`, `/BOS/`, cart/checkout/search stay PHP until dual-sample + staged exact-route cutover. |
| Digests ≠ premature UX | www Blazor `*-app` / digests are scaffolding until promoted. |
| Exact-route only | Never broad `location /`, `/cp`, `/erp`, `/bos`, `/api`, `/storefront` on tenant vhosts. |
| Look & feel | ASP.NET must match PHP fonts/CSS/heroes/menus before traffic moves. |
| Meter ≠ cutover | Weighted Zero-PHP % does **not** mean tenants are cut over. |

## Installer guards (refuse-by-default, unlockable)

| Script | Guard |
| --- | --- |
| `cloudpanel_install_exact_route_shadow.sh` | `ecomae_assert_nginx_shadow_target_allowed … exact-route` |
| `cloudpanel_install_surface_digest_shadows.sh` | same |
| `cloudpanel_install_storefront_digest_shadows.sh` | same |
| `cloudpanel_install_presentation_app_shadows.sh` | `… presentation` |

Overrides:

- `ECOMAE_CONFIRM_TENANT_HOST_SHADOW=YES` — exact-route on **non-named** tenant/industry vhost.  
- `ECOMAE_CONFIRM_TENANT_PRESENTATION_SHADOW=YES` — presentation on **non-named** tenant (rare).  
- **`ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES`** — unlocks exact-route **or** presentation parity shadows on **named** live tenants (path to 100% ASP.NET). Still exact-route only; never broad cutover. Require dual-sample evidence before traffic promotion.

`cutoverAllowed=false` / `readyForPhpRemoval=false` until dual-sample + human `RELEASE_OWNER_APPROVAL.md` (never invent that file).

## Verify PHP-primary until cutover

```bash
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh
bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh
```

Expect PHP fingerprints on `/` `/CP/` `/ERP/` and no unintended ASP.NET product chrome — until a staged parity shadow is intentionally installed.
