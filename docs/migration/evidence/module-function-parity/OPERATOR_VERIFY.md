# Operator verify — module-function parity

Contract inventory only. **`aspnetCompleteCount` must stay 0** until a human attaches
`docs/migration/evidence/presentation/MODULE_FUNCTION_TEST_PASS.md` containing
`MODULE_FUNCTION_PARITY_PASS`.

```bash
bash scripts/cloudpanel_run_module_function_parity_operator.sh
```

Expect `cutoverAllowed=false`, `readyForPhpRemoval=false`. Never invent the human pass file or `RELEASE_OWNER_APPROVAL.md`.
