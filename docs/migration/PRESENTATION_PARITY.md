# Presentation parity contract (PHP → ASP.NET Core)

Frontend and backend presentation must stay the same during conversion. ASP.NET Core surface shells reuse the live PHP chrome assets; digests stay JSON for tooling.

Full plan: `docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md`. Batch 0 lists every PHP CP/ERP/BOS/storefront module on hybrid shells (`GET /migration/php-module-catalog`) so nothing is omitted from navigation while interactive ports are completed family-by-family.

## Contract

1. **Same assets** — HTML shells link PHP CSS/JS paths (`/epc-static.php?f=...`, `/content/general_pages/epc_cp_*_css.php`, `bos/epc_bos_shell.css`, `templates/modex/...`).
2. **JSON preserved** — Default response for `/cp`, `/erp`, `/bos`, `/storefront/account` remains JSON. Use `?format=json` to force JSON.
3. **HTML for browsers** — `Accept: text/html` (without `application/json`) or `?format=html` returns the presentation-preserving chrome shell.
4. **Auth errors stay JSON** — Unauthorized shell probes remain `401` JSON so API clients are not surprised.
5. **PHP remains authoritative** — Full interactive UX (menus, widgets, cart/checkout, BOS `$_SESSION` modules) stays on PHP until staging smoke + release-owner approval.
6. **Hybrid chrome** — Blazor `/…/app` shells link PHP module URLs so functionality is not orphaned (see `CHROME_PARITY_GAP_MATRIX.md`).
7. **Login bridge (opt-in)** — `/cp|/erp|/bos|/storefront/login` + `POST /auth/login/admin` mint PHP-compatible cookies when `EcomAE__SecretSuccession` is set; otherwise UI points to PHP login.

## Routes

| Surface | Shell / preview | Login bridge | Presentation reporter |
| --- | --- | --- | --- |
| CP | `/cp/app` (+ `/cp?format=html`) | `/cp/login` | `/migration/presentation-parity` |
| ERP | `/erp/app` | `/erp/login` | same |
| BOS | `/bos/app` | `/bos/login` | same |
| Storefront | `/storefront/app` | `/storefront/login` | same |

## Evidence

Capture HTML samples under `docs/migration/evidence/presentation/` after CloudPanel redeploy:

- stylesheet `href` list matches PHP desktop chrome
- brand mark resolves to `/content/general_pages/epc_ecomae_logo_svg.php`
- JSON `?format=json` shape still includes `shell` + `session`

## Guardrail

Do not enable broad `/cp`, `/erp`, `/bos`, or storefront cutover from this scaffold alone. Presentation scaffolding does not change Zero-PHP completion (still 95% / remaining 5% = PHP decommission gate).
