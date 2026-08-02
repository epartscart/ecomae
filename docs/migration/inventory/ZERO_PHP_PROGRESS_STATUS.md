# Zero-PHP Progress Status

## Snapshot

| Metric | Value |
| --- | ---: |
| True zero-PHP completion | 35.0% |
| Pending to 100% | 65.0% |
| Route/job implementation complete | 0.0% |
| Route/job parity-ready | 0.0% |
| Route/job shadow-or-better | 0.0% |
| Total tracked PHP files | 3,049 |
| Job-like PHP files | 140 |
| Exact-route batches | 61 |

## Status notes

- Ownership assignment and batch planning are complete, but real implementation/parity/live cutover is not complete.
- PHP remains the production fallback and must not be broadly cut over for CP, ERP, BOS, API, or storefront surfaces.
- Batch 1 worker replacement has started at the ASP.NET Core dry-run evidence layer: the worker runner now attaches PHP baseline instructions, ASP.NET dry-run sample text, parity comparison guidance, rollback command text, smoke status, fallback safety, and approvals. A catalog-wide Batch 1 dry-run reporter now summarizes evidence readiness for all planned worker jobs while keeping blockers and PHP fallback visible, and the worker host logs this report at startup for operator review. This is still not production execution or parity-ready cutover.

## Final evidence workflow before any 100% claim

1. Run `python3 scripts/generate_zero_php_100_evidence_templates.py` in default dry-run mode to refresh the summary only.
2. Run `python3 scripts/generate_zero_php_100_evidence_templates.py --write` only when owners are ready to fill all generated evidence files.
3. Fill every one of the 3,049 route/job evidence templates with implementation references, PHP baseline samples, ASP.NET dry-run or shadow samples, parity comparison, exact-route cutover data, rollback approval, smoke-test status, and PHP fallback safety evidence.
4. Run `python3 scripts/verify_zero_php_100_readiness.py` and keep the release blocked until it passes with real production evidence.
5. Do not claim 100% / 0% PHP until every tracked PHP route/job is live on ASP.NET Core or approved as removed, rollback approval exists, production smoke checks pass, and PHP fallback is no longer required.
