# Public production probes (no secrets)

Captured from publicly reachable diagnostics. These prove migration endpoints are live and that `/api/v1/price/lookup` is already on ASP.NET (auth gate returns JSON `missing_api_key`).

They are **not** a substitute for authenticated staging smoke (`staging-smoke/*.json`) or release-owner approval.

## Files

- `www-zero-php-completion.json` — live `/migration/zero-php-completion` (95% / 5% pending)
- `www-php-decommission-readiness.json` — live `/migration/php-decommission-readiness` (`readyToRemovePhp=false`)
- `www-presentation-parity.json` — live `/migration/presentation-parity` (PHP chrome asset contract)
- `www-live-surface-stack.json` — classified live Super CP / tenant / ASP.NET stack probe
- `www-price-lookup-missing-key.json` — unauthenticated price lookup JSON 401 from ASP.NET
- `www-price-lookup.headers.txt` — response headers (cookies/CF noise stripped)

Refresh stack probe anytime:

```bash
bash scripts/probe_live_surface_stack.sh
```

## Still required for the final 5%

1. Authenticated smoke artifacts under `../staging-smoke/`
2. Parity samples under `../parity-samples/`
3. Human `../RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK`
