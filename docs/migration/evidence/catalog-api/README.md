# Catalog / API exact-route contract floor

Offline evidence for the 18 catalog routes + price lookup (`YARP routeCount=19`).

- Allowlist sync: `python3 scripts/validate_catalog_api_allowlist_sync.py`
- Operator: `bash scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh`
- Compare result: `compare-result.json` (`cutoverAllowed=false`)

Migration goldens live under `../surface-parity/samples/migration/api-catalog-*.json`.
Price samples: `../price-lookup/`.

Never invent `RELEASE_OWNER_APPROVAL.md`. Exact-route install remains operator-gated.
