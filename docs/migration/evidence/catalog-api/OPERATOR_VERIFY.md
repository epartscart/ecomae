# Operator verify — catalog/API dual-sample contract floor

```bash
python3 scripts/validate_catalog_api_allowlist_sync.py
bash scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh
```

Expect `compare-result.json` with `ok=true`, `catalogGoldensChecked=18`, `cutoverAllowed=false`.

Wave-1 catalog list goldens must keep non-empty `data[]` item-field sentinels
(`list-item-field-floor.json`: manufacturers/models/modifications/brands/suppliers/brand-parts).
Price-lookup contract-only requires non-empty `offers[]`.

Live API-key captures remain CloudPanel/staging work. Never invent `RELEASE_OWNER_APPROVAL.md`.
