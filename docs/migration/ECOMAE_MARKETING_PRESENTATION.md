# ecomae.com marketing presentation (PHP ↔ ASP.NET)

**Hard law:** Live `https://www.ecomae.com/` and all marketing pages stay **PHP** until dual-sample evidence + human `RELEASE_OWNER_APPROVAL.md`. Never broad `location /` cutover.

## Authoritative PHP stack

| Piece | Path |
| --- | --- |
| Entry | `index.php` → `epc_render_ecomae_marketing_home_and_exit()` |
| Router | `content/general_pages/epc_ecomae_platform_router.php` |
| Animated hero | `epc_ecomae_platform_hub()` in `epc_ecomae_platform_layout.php` |
| Shared chrome CSS | `content/general_pages/epc_ecomae_platform_marketing.css` (via `epc_ecomae_platform_marketing_css.php`) |
| Home sections | `epc_ecomae_home_sections.php` + `epc_ecomae_home_3d.{css,js}` |
| Pages | `epc_ecomae_platform_pages.php`, `epc_ecomae_marketing_pages.php`, legal/brochure/blockchain |

Hero markers: `.epm-hub`, `.epm-hub__orbit-spin`, `.epm-hub__matrix`, `.epm-hub-section`.

## ASP.NET hybrid preview (scaffold only)

| Route | Role |
| --- | --- |
| `/marketing/app` | Blazor preview of animated `epm-hub` + hybrid directory of all marketing pages |
| `/migration/marketing-presentation-lock` | JSON lock (`cutoverAllowed=false`) |

Assets reuse PHP CSS/JS via `LegacyPresentationAssets.MarketingStylesheets` / `MarketingScripts`.

## Probe

```bash
bash scripts/cloudpanel_probe_ecomae_marketing_php_chrome.sh
```

After deploy of presentation shadows:

```bash
ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES bash scripts/cloudpanel_install_presentation_app_shadows.sh
```

Compare PHP home vs ASP.NET preview on the human board: `/migration/compare`.
