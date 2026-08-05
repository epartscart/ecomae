# PHP as reference mode (confirmed architecture)

**Human-confirmed model (2026-08-05):** ASP.NET Core becomes the **live primary** runtime. The PHP project stays **available as a reference** so operators can still open previous screens/results and find gaps — **till keep** (or a separate delete approval).

Evidence lock: `docs/migration/evidence/decommission/aspnet-primary-php-reference-confirmed.json`.

This is **not** PHP source deletion and does **not** invent cutover approval / live traffic flip by itself.

## Locks (unchanged by this confirmation)

| Flag | Value |
| --- | --- |
| `cutoverAllowed` | `false` always for digest/board evidence (exact-route only; no broad trees) |
| `readyForPhpRemoval` | `false` (reference keep ≠ removal) |
| `RELEASE_OWNER_APPROVAL.md` | **Present** (`APPROVED_TO_REMOVE_PHP_FALLBACK` + `KeepPhpProjectAvailable`) — human-owned; execute via `cloudpanel_execute_aspnet_primary_cutover_operator.sh` |
| `RequirePhpFallback` | `true` until dual-sample-green per exact route (templates default true) |
| Architecture confirmation | `aspnet-primary-php-reference-confirmed.json` — destination intent + execute path |

## What “reference” means

1. Product URLs stay unchanged: `/cp` `/erp` `/bos` `/` on **www.ecomae.com** and **all named live tenants** (epartscart, electronicae, stylenlook, thejewellerytrend, taxofinca) proxy to ASP.NET (no redirect to `/cp/app`). No half-and-half.
2. Deep ASP.NET product trees (`/cp/` `/erp/` `/bos/` `/storefront/` `/marketing/`) also proxy to Kestrel. Uppercase `/CP/` `/ERP/` `/BOS/` and `/shop/` are remapped into ASP.NET apps (not served as PHP product).
3. PHP reference is **separate**: `/php-reference/home|cp|erp|bos|storefront` → `index.php?epc_php_reference=…` only (never bounce into product `/cp` trees).
4. Operators compare at `/migration/compare` (PHP reference column vs shared ASP.NET URLs).
5. Dual-sample scripts hit `/php-reference/*` while product URLs stay on ASP.NET.
6. PHP writes should be disabled or isolated after ASP.NET is primary (prefer read-only reference) so results do not diverge from conflicting writes.

## Config

Section: `EcomAE:PhpReference` (see `aspnet/.../appsettings.json` and `deploy/aspnet/platform.env.example`).

| Key | Purpose |
| --- | --- |
| `Enabled` | Expose reference board/reporter |
| `Mode` | `aspnet-primary-php-reference` |
| `ArchitectureConfirmed` | Human-confirmed architecture declaration |
| `KeepPhpProjectAvailable` | Must stay true while using reference compares |
| `WwwPhpBaseUrl` / `TenantPhpBaseUrl` | PHP reference bases |
| `AspNetPrimaryBaseUrl` | ASP.NET compare base |

## Endpoints / boards

- JSON: `GET /migration/php-reference-mode`
- Human board: `/migration/compare`
- Cutover validation: `/migration/cutover-validation` (still blocked)
- Decommission readiness: `/migration/php-decommission-readiness` (`ReadyToRemovePhp=false`)

## Operator gap workflow

1. Promote ASP.NET exact routes only after dual-sample green.
2. Keep PHP reference reachable.
3. Open PHP vs ASP.NET on `/migration/compare`.
4. Record gaps → fix ASP.NET → re-run compare / dual-sample.
5. Only a separate human approval removes PHP fallback/source.

## Rollback (keeps PHP)

```bash
bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback
# optional: --route /erp/dashboard-summary
```

## Related

- `docs/migration/TENANT_MIGRATION_SAFETY.md`
- `docs/PROJECT_SOP_SECURITY_TENANT_ISOLATION.md`
- `docs/migration/evidence/decommission/RELEASE_OWNER_APPROVAL.example.md` (example only)
