# PHP Route and Job Cutover Batches Summary

Generated from `scripts/build_zero_php_cutover_batches.py`. This converts the owner plan into exact-route implementation batches. Nothing in this file authorizes broad proxying or PHP fallback removal.

## Batch totals

- Total assignments: 3049.
- Batch size: 50.
- Total batches: 61.
- Status: `planned-not-implemented`.

## Rules

- Implement one batch at a time; do not broad-cutover CP, ERP, BOS, API, or storefront trees.
- Keep PHP fallback enabled until a batch is parity-ready and live smoke passes.
- ASP.NET Core remains the owner of route/API/auth/database/business behavior.
- Python is invoked only by ASP.NET Core for stateless AI-service helper results.

## First 10 batches

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

## Evidence required per batch

- ASP.NET Core implementation merged
- unit/integration tests passing
- PHP-vs-ASP.NET parity sample attached
- operator rollback command documented
- exact-route proxy rule only
- live smoke passed before PHP fallback removal
