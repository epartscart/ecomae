# Tenant migration safety (ASP.NET-primary all tenants → PHP reference only)

**Target end-state:** 100% ASP.NET Core live product traffic on **every** named tenant. PHP project remains as a **reference** for previous results / gap-finding (`docs/migration/PHP_AS_REFERENCE_MODE.md`) until a separate deletion approval. **No half-and-half** (some tenants PHP, some ASP.NET).

**Master SOP (security, confidentiality, tenant isolation, cutover protocols):**  
[`docs/PROJECT_SOP_SECURITY_TENANT_ISOLATION.md`](../PROJECT_SOP_SECURITY_TENANT_ISOLATION.md)

**Policy now:** All named live product tenants use ASP.NET for `/` `/cp` `/erp` (and deep product trees). **Product `/bos` is Super-CP only** — tenant hosts must 404. PHP opens only via `/php-reference/*` (except `/php-reference/bos`, also Super-CP-only). Chrome must stay same-to-same with the PHP look while the stack is ASP.NET.

## Named live tenants (all ASP.NET-primary)

| Tenant | Primary hosts | Product stack |
| --- | --- | --- |
| ePartsCart | `epartscart.com`, `www.epartscart.com` | ASP.NET |
| Electronicae | `www.electronicae.com`, `electronicae.com` | ASP.NET |
| StyleNLook | `www.stylenlook.com`, `stylenlook.com` | ASP.NET |
| The Jewellery Trend | `www.thejewellerytrend.com`, `thejewellerytrend.com` | ASP.NET |
| Taxofinca | `www.taxofinca.com`, `taxofinca.com` | ASP.NET |

**Install classic-entry on all of them:**

```bash
ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
  bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts
```

Classifier: `scripts/ecomae_nginx_site_safety.py`  
Catalog: `GET /migration/live-tenant-presentation-lock`  
Path board: `GET /migration/aspnet-zero-php-path` · `docs/migration/ASPNET_ZERO_PHP_PATH.md`

## Same-to-same / invisible migration

**Same-to-same / invisible migration:** tenants must not feel PHP→ASP.NET. Digests/Blazor previews on www **never** replace tenant product chrome until dual-sample + staged exact-route cutover.

## Same-to-same / invisible cutover (hard law)

| Rule | Meaning |
| --- | --- |
| PHP-primary until parity | Live `/`, `/CP/`, `/ERP/`, `/BOS/`, cart/checkout/search stay PHP until dual-sample + staged exact-route cutover. |
| Digests ≠ premature UX | www Blazor `*-app` / digests are scaffolding until promoted — they **never** replace tenant product chrome. |
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
