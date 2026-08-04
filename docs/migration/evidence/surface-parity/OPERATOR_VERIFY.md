# Operator verify — surface-field + digest dual samples

## Surface-field offline floor

```bash
bash scripts/cloudpanel_run_surface_field_parity_operator.sh
```

Validates `www-surface-field-parity.json` (`cutoverAllowed=false`, `readyForPhpRemoval=false`, ≥54 contracts),
rebuilds `php-catalog-coverage-board.json` (all 725 PHP catalog rows → `digest-contract` /
`php-only-deeplink` / `hybrid-directory-only` / `missing`, `missingCount=0`,
`aspNetInteractiveComplete=0`), and re-runs digest + catalog-api migration contract floors.

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

## CP menus item-field floor

`cp-menus` migration golden must keep a non-empty structure-summary sentinel
(`nodeCount` / link-mode counts; raw `structure` omitted). Enforced by
`scripts/compare_digest_dual_samples.py` (`LIST_ITEM_FIELDS` + `LIST_NONEMPTY_MIGRATION`)
and documented in `cp-menus-item-field-floor.json`.

## List digest item-field floor (25 stems)

All list digests under `LIST_CONTRACTS` require non-empty migration item-field
sentinels (`list-digest-item-field-floor.json`). Regenerate with:

```bash
python3 scripts/generate_migration_digest_contract_samples.py
bash scripts/cloudpanel_run_digest_dual_sample_operator.sh
```
