# B4 Recovery and Rollback Evidence Runbook

This runbook captures the operator evidence required before any B4 tenant is
promoted from PHP fallback to an ASP.NET-owned exact route. It does not grant
cutover authority and must not be used against a live tenant without an
approved maintenance window and release-owner approval.

## Required evidence per tenant

Capture a separate evidence bundle for inventory, quality, WMS, and planning:

1. Release identity and route ownership before the rehearsal.
2. A verified production backup, including timestamp, scope, checksum, and
   storage location.
3. A restore into the approved recovery target, with row-count and application
   readback checks for the tagged B4 fixtures.
4. A controlled ownership rollback to PHP fallback, followed by:
   - `nginx -t`
   - a reload result
   - an authenticated route smoke
   - confirmation that ASP.NET is no longer the route owner.
5. Cleanup or isolation of all rehearsal markers.
6. Release-owner approval referencing the captured bundle.

Local throwaway MariaDB dump/restore proves only local recovery mechanics. It
does not satisfy production backup/restore, ownership rollback, UAT, production
smoke, or release-owner approval.

## ASP.NET release identity

```bash
readlink -f /var/www/ecomae-aspnet/current
cat /var/www/ecomae-aspnet/current/platform/RELEASE_SHA
systemctl is-active ecomae-platform.service
curl -fsS http://127.0.0.1:5100/health
```

Record the outputs without recording secrets.

Validate each completed bundle before attaching it to the acceptance record:

```bash
python3 scripts/validate_b4_recovery_bundle.py <tenant-process-bundle.json>
```

The validator rejects missing production references, throwaway-only evidence,
failed rollback checks, and any attempt to enable cutover or remove PHP.
The private bundle must explicitly include `"productionEvidence": true`;
local rehearsal evidence alone cannot satisfy the production gate.

## Database recovery rehearsal

Use the production backup mechanism approved by the database owner. Do not
replace it with an ad-hoc dump command. Record:

- source tenant and backup timestamp;
- backup object path and checksum;
- recovery database or instance;
- restore start/end timestamps;
- tagged fixture counts before backup, after restore, and after cleanup;
- the operator and change/release identifier.

The restore target must not be the live tenant database unless the approved
maintenance procedure explicitly requires it.

## Ownership rollback

Remove only the exact ASP.NET Nginx location blocks for the rehearsed route;
never remove the PHP site or PHP-FPM configuration.

```bash
sudo nginx -t
sudo systemctl reload nginx
curl -fsS -H 'Host: <tenant-host>' https://<tenant-host>/<exact-route>
bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback
```

The bundle must show that PHP served the route after rollback and that the
ASP.NET fallback lock remained enabled. Broad `/api`, `/cp`, `/erp`, `/bos`,
storefront, or `/` proxying is forbidden without separate route evidence.

## Fail-closed acceptance rule

Do not change `artifacts/erp-dummy-fixture-matrix.json` from production recovery
missing to verified based on local rehearsals. Do not set
`cutoverAllowed=true`, `readyForPhpRemoval=true`, or remove PHP fallback.
Production recovery and ownership rollback remain open until the bundle contains
direct operator evidence for the named tenant and process.
