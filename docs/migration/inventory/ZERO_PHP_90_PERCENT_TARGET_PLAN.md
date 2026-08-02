# Zero-PHP 90% Target Plan

Generated from `scripts/build_zero_php_90_percent_plan.py`. This is a target execution plan, not a completion claim.

## Target math

- Current foundation/planning floor: 35.0%.
- Target true zero-PHP completion: 90.0%.
- Implementation/live weight remaining after foundation: 65.0%.
- Total tracked PHP route/job assignments: 3049.
- Items that must be `live` or `removed` to honestly report 90%: 2580.
- Selected exact-route batches: 52.
- Selected items: 2600.
- Items still remaining after the 90% target: 449.

## Guardrails

- Do not report 90% until all selected items are `live` or `removed`.
- Every selected item needs ASP.NET Core implementation, parity sample, exact-route proxy, rollback command, and production smoke evidence.
- PHP fallback remains until item-level evidence is approved.
- No broad CP, ERP, BOS, API, or storefront proxy cutover is authorized.

## First 20 selected batches

| Batch | Primary slice | Items | Risk counts | Owner counts |
| ---: | --- | ---: | --- | --- |
| 1 | worker-replacement | 50 | high:50 | aspnet-core:45, aspnet-with-python-ai-helper:5 |
| 2 | worker-replacement | 50 | high:50 | aspnet-core:50 |
| 3 | worker-replacement | 50 | high:40, medium:10 | aspnet-core:50 |
| 4 | public-api-port | 50 | high:6, medium:44 | aspnet-core:44, aspnet-with-python-ai-helper:6 |
| 5 | ai-service-contract | 50 | high:50 | aspnet-with-python-ai-helper:50 |
| 6 | ai-service-contract | 50 | high:18, medium:32 | aspnet-with-python-ai-helper:50 |
| 7 | ai-service-contract | 50 | medium:50 | aspnet-with-python-ai-helper:50 |
| 8 | ai-service-contract | 50 | medium:50 | aspnet-with-python-ai-helper:50 |
| 9 | ai-service-contract | 50 | medium:50 | aspnet-with-python-ai-helper:50 |
| 10 | ai-service-contract | 50 | high:17, medium:33 | aspnet-core:17, aspnet-with-python-ai-helper:33 |
| 11 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
| 12 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
| 13 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
| 14 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
| 15 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
| 16 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
| 17 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
| 18 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
| 19 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
| 20 | cp-workflow-port | 50 | high:50 | aspnet-core:50 |
