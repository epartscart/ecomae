# Operator verify — module-function parity

Full PHP catalog inventory (CP brochure features, ERP areas/tabs/categories, BOS modules,
storefront surfaces). Hybrid TARGETS upgrade matching rows; all other entries stay `php-only`.
**`aspnetCompleteCount` must stay 0** until a human attaches
`docs/migration/evidence/presentation/MODULE_FUNCTION_TEST_PASS.md` containing
`MODULE_FUNCTION_PARITY_PASS`.

```bash
bash scripts/cloudpanel_run_module_function_parity_operator.sh
python3 scripts/compare_module_function_parity.py
```

Expect `moduleCount >= 714`, `cutoverAllowed=false`, `readyForPhpRemoval=false`.

Consistency with the surface-field coverage board (same 714 unique catalog ids):

```bash
python3 scripts/build_surface_field_catalog_coverage_board.py
python3 scripts/validate_module_function_coverage_consistency.py
```

Never invent the human pass file or `RELEASE_OWNER_APPROVAL.md`.
