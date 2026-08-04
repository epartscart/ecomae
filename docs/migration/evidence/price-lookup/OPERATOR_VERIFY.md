# Operator verify — price lookup dual sample

Exact-route only: `/api/v1/price/lookup`. Never broad `/api`.

```bash
bash scripts/cloudpanel_run_price_lookup_dual_sample_operator.sh
```

Uses checked-in `php-baseline-sample.json` + `aspnet-output-sample.json` by default.
Expect `compare-result.json` with `cutoverAllowed=false`.

Live staging smoke remains opt-in (`RUN_PRICE_LOOKUP_SMOKE=1`). Never invent `RELEASE_OWNER_APPROVAL.md`.
