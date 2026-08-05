# PHP as reference mode (confirmed architecture)

**Human-confirmed model:** ASP.NET Core becomes the **live primary** runtime. The PHP project stays **available as a reference** so operators can still open previous screens/results and find gaps.

This is **not** PHP source deletion and does **not** invent cutover approval.

## Locks (unchanged)

| Flag | Value |
| --- | --- |
| `cutoverAllowed` | `false` until dual-sample + human `RELEASE_OWNER_APPROVAL.md` |
| `readyForPhpRemoval` | `false` (reference keep ≠ removal) |
| `RequirePhpFallback` | `true` in templates until final gate |
| `RELEASE_OWNER_APPROVAL.md` | Never invented by agents |

## What “reference” means

1. Live customer traffic eventually served by ASP.NET (exact-route, staged).
2. PHP docroot/project remains installed (same host internal vhost, staging clone, or read-only replica).
3. Operators open PHP URLs next to ASP.NET URLs on `/migration/compare`.
4. Dual-sample scripts can still hit `ECOMAE_PHP_BASE_URL` / `EcomAE:PhpReference:WwwPhpBaseUrl`.
5. PHP writes should be disabled or isolated after ASP.NET is primary (prefer read-only reference) so results do not diverge from conflicting writes.

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
