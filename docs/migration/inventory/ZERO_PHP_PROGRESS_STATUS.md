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
- The next useful implementation work is Batch 1 worker replacement in ASP.NET Core dry-run mode with PHP-vs-ASP.NET parity samples and rollback commands attached before any production traffic changes.

## Final evidence workflow before any 100% claim

1. Run `python3 scripts/generate_zero_php_100_evidence_templates.py` in default dry-run mode to refresh the summary only.
2. Run `python3 scripts/generate_zero_php_100_evidence_templates.py --write` only when owners are ready to fill all generated evidence files.
3. Fill every one of the 3,049 route/job evidence templates with implementation references, PHP baseline samples, ASP.NET dry-run or shadow samples, parity comparison, exact-route cutover data, rollback approval, smoke-test status, and PHP fallback safety evidence.
4. Run `python3 scripts/verify_zero_php_100_readiness.py` and keep the release blocked until it passes with real production evidence.
5. Do not claim 100% / 0% PHP until every tracked PHP route/job is live on ASP.NET Core or approved as removed, rollback approval exists, production smoke checks pass, and PHP fallback is no longer required.
