# Surface field / function / presentation parity

Goal: ASP.NET Core CP, ERP, BOS, and frontend must match existing PHP **field-by-field, function-by-function, presentation-by-presentation** before any cutover.

## Hard rule

- PHP remains authoritative.
- `CutoverAllowed=false` until dual live samples match.
- Do **not** enable broad `/cp`, `/erp`, `/bos`, `/`, or `/api` nginx cutover from harness success alone.
- Keep `AdminAspNetEnabled=false`, `StorefrontAspNetEnabled=false`, `RequirePhpFallback=true`.

## Operator board

After deploy: `GET /migration/surface-field-parity`

## Harness

```bash
bash scripts/run_surface_parity_harness.sh
```

Optional authenticated digest capture (CloudPanel / loopback):

```bash
export ECOMAE_ASPNET_BASE_URL=http://127.0.0.1:5100
export ECOMAE_ADMIN_COOKIE_HEADER='admin_session=...; admin_u_id=...'
bash scripts/run_surface_parity_harness.sh
```

Field compare two samples:

```bash
python3 scripts/compare_surface_payload_parity.py \
  --left docs/migration/evidence/surface-parity/samples/cp-dashboard-php.json \
  --right docs/migration/evidence/surface-parity/samples/cp-dashboard-aspnet.json \
  --path summary \
  --require users,adminSessions,portalTenants,activePortalTenants,source,message
```

## Evidence

- Contracts: `/migration/surface-field-parity`
- Harness report: `docs/migration/evidence/surface-parity/harness-report.json`
- Presentation asset check: `docs/migration/evidence/surface-parity/presentation-asset-check.json`
- Dual samples: `docs/migration/evidence/surface-parity/samples/`

## Promotion gate (exact-route only)

1. Harness pass with presentation assets OK
2. Dual samples `match=true` for contracted digests
3. Exact-route shadow one `location =` at a time
4. No PHP removal until final decommission gate
