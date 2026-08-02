# Zero-PHP 100% Evidence Template Summary

- Mode: **dry-run**
- Selected batches: **61**
- Selected items: **3049**
- Existing templates: **0**
- Created templates: **0**
- Missing templates: **3049**
- True zero-PHP completion: **35.0%**
- Pending to 100%: **65.0%**

## Required evidence schema fields

- `implementation_reference`
- `php_baseline_sample`
- `aspnet_dry_run_or_shadow_sample`
- `response_or_data_parity_comparison`
- `exact_route_cutover_data`
- `auth_tenant_permission_parity_result`
- `rollback_command`
- `rollback_approval`
- `production_smoke_status`
- `php_fallback_safety`
- `owner_approval`

## Guardrail

Do not report 100% until every tracked PHP route/job is live or removed, rollback approval exists, production smoke checks pass, and PHP fallback is no longer required for the item.
