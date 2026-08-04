# ecomae.com marketing → ASP.NET (parity path)

**Target:** ASP.NET Core serves all marketing pages (including animated `epm-hub`); PHP removed.

**Interim:** Live `https://www.ecomae.com/` stays PHP-primary until `/marketing/app` dual-samples same-to-same. That is a **parity gate**, not a permanent ban. Never broad `location /` without approval.

## PHP sources (until cutover)

| Piece | Path |
| --- | --- |
| Entry | `index.php` → `epc_render_ecomae_marketing_home_and_exit()` |
| Router | `content/general_pages/epc_ecomae_platform_router.php` |
| Animated hero | `epc_ecomae_platform_hub()` |
| Shared CSS | `epc_ecomae_platform_marketing.css` |
| Home sections | `epc_ecomae_home_sections.php` + `home_3d.{css,js}` |

## ASP.NET replacement scaffold

| Route | Role |
| --- | --- |
| `/marketing/app` | Blazor `epm-hub` + hybrid directory of all marketing pages |
| `/migration/marketing-presentation-lock` | Parity-gate JSON (`cutoverAllowed=false`, target `100%-aspnet-core-0-php`) |
| `/migration/aspnet-zero-php-path` | Overall zero-PHP phase board |

## Probe / promote

```bash
bash scripts/cloudpanel_probe_ecomae_marketing_php_chrome.sh
# After dual-sample green + approval: exact-route promote / (never invent cutoverAllowed=true)
```
