# Operator verify — surface-field + digest dual samples

## Surface-field offline floor

```bash
bash scripts/cloudpanel_run_surface_field_parity_operator.sh
```

Validates `www-surface-field-parity.json` (`cutoverAllowed=false`, `readyForPhpRemoval=false`, ≥53 contracts)
and re-runs digest + catalog-api migration contract floors.

Optional live harness (needs network / cookies for full capture):

```bash
ECOMAE_SURFACE_FIELD_LIVE=1 bash scripts/cloudpanel_run_surface_field_parity_operator.sh
```

## Digest dual samples

```bash
bash scripts/cloudpanel_run_digest_dual_sample_operator.sh
python3 scripts/validate_surface_digest_allowlist_sync.py
```

Checked-in digest exact-route inventory (35 = surface 30 + storefront 4 + orders-digest):
`docs/migration/evidence/surface-parity/surface-digest-exact-routes.json`

Expect `cutoverAllowed=false`. Never invent `RELEASE_OWNER_APPROVAL.md`.
