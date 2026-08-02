# Public production probes (no secrets)

Captured from publicly reachable diagnostics. These prove migration endpoints are live and that `/api/v1/price/lookup` is already on ASP.NET (auth gate returns JSON `missing_api_key`).

They are **not** a substitute for authenticated staging smoke (`staging-smoke/*.json`) or release-owner approval.

## Files

- `www-zero-php-completion.json` — live `/migration/zero-php-completion` (95% / 5% pending)
- `www-php-decommission-readiness.json` — live `/migration/php-decommission-readiness` (`readyToRemovePhp=false`)
- `www-presentation-parity.json` — live `/migration/presentation-parity` (PHP chrome asset contract)
- `www-live-surface-links.json` — live operator/tenant URL catalog
- `www-surface-field-parity.json` — live field/function/presentation contract report
- `www-surface-parity.json` — live surface parity scaffold report
- `www-live-surface-stack.json` — classified live Super CP / tenant / ASP.NET stack probe
- `www-final-gate-area-tests.json` — unit/live/chrome/smoke-area results (`readyToRemovePhp=false`)
- `www-price-lookup-missing-key.json` — unauthenticated price lookup JSON 401 from ASP.NET
- `www-price-lookup.headers.txt` — response headers (cookies/CF noise stripped)
- `www-catalog-status-still-php.json` — public catalog status still PHP HTML (pending exact-route shadow)
- `www-cp-dashboard-summary-still-php.json` — public CP digest still PHP HTML (pending shadow after smoke)

Refresh stack probe anytime:

```bash
bash scripts/probe_live_surface_stack.sh
```

On CloudPanel, also run env preflight (prints PRESENT/MISSING only):

```bash
bash scripts/cloudpanel_validate_final_gate_env.sh
```

## Still required for the final 5%

1. Authenticated smoke artifacts under `../staging-smoke/`
2. Parity samples under `../parity-samples/` (public still-PHP proofs help; authenticated dual samples still required)
3. Human `../RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK`
