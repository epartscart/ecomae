# Operator verify index (offline-honest)

All helpers keep `cutoverAllowed=false` / `readyForPhpRemoval=false`. Never invent
`RELEASE_OWNER_APPROVAL.md` or `MODULE_FUNCTION_TEST_PASS.md`. Batch 6 stays blocked.

| Family | Operator | Doc |
| --- | --- | --- |
| **Offline migration gate** | `bash scripts/cloudpanel_run_offline_migration_gate.sh` | this index |
| All dual-sample + module-function | `bash scripts/cloudpanel_run_all_dual_sample_operators.sh` | — |
| Presentation recheck | `bash scripts/cloudpanel_run_presentation_recheck_operator.sh` | `presentation/OPERATOR_VERIFY.md` |
| Hybrid UI | `bash scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh` | `hybrid-ui-dual-samples/OPERATOR_VERIFY.md` |
| Digest + surface-field | `bash scripts/cloudpanel_run_digest_dual_sample_operator.sh` / `cloudpanel_run_surface_field_parity_operator.sh` | `surface-parity/OPERATOR_VERIFY.md` |
| PHP catalog coverage board (725) | `python3 scripts/build_surface_field_catalog_coverage_board.py` | `surface-parity/php-catalog-coverage-board.json` |
| Hybrid directory full catalog (725) | `python3 scripts/validate_hybrid_directory_full_catalog_floor.py` | `hybrid-ui-dual-samples/hybrid-directory-full-catalog-floor.json` |
| Module-function ↔ coverage consistency | `python3 scripts/validate_module_function_coverage_consistency.py` | `module-function-parity/coverage-consistency.json` |
| Price lookup | `bash scripts/cloudpanel_run_price_lookup_dual_sample_operator.sh` | `price-lookup/OPERATOR_VERIFY.md` |
| Catalog/API contract floor | `bash scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh` | `catalog-api/OPERATOR_VERIFY.md` |
| Login cookie | `bash scripts/cloudpanel_run_login_cookie_dual_sample_operator.sh` | `login-session-bridge/OPERATOR_VERIFY.md` |
| Catalog miss | `bash scripts/cloudpanel_run_catalog_miss_dual_sample_operator.sh` | `catalog-miss-umapi/OPERATOR_VERIFY.md` |
| Module-function | `bash scripts/cloudpanel_run_module_function_parity_operator.sh` | `module-function-parity/OPERATOR_VERIFY.md` |
| Tenant same-to-same PHP chrome | `bash scripts/cloudpanel_run_tenant_safety_operator.sh` | `tenant-safety/OPERATOR_VERIFY.md` |
| Scaffold / allowlist guardrails | `bash scripts/validate_enterprise_bos_scaffold_guardrails.sh` | — |

After CloudPanel redeploy + presentation shadows:

```bash
bash scripts/cloudpanel_find_and_redeploy.sh
ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES \
  bash scripts/cloudpanel_install_presentation_app_shadows.sh
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh
bash scripts/cloudpanel_run_all_dual_sample_operators.sh
bash scripts/cloudpanel_run_presentation_recheck_operator.sh
```
