# Operator verify — module-function parity

Contract inventory only. Hybrid TARGETS are preview/deeplink coverage; full PHP catalog
scope is tracked via `phpCatalogCounts` (CP≥405 / ERP tabs≥154 / BOS≥99 / storefront≥12).
**`aspnetCompleteCount` must stay 0** until a human attaches
`docs/migration/evidence/presentation/MODULE_FUNCTION_TEST_PASS.md` containing
`MODULE_FUNCTION_PARITY_PASS`.

```bash
bash scripts/cloudpanel_run_module_function_parity_operator.sh
python3 scripts/compare_module_function_parity.py
```

Expect `cutoverAllowed=false`, `readyForPhpRemoval=false`. Never invent the human pass file or `RELEASE_OWNER_APPROVAL.md`.
