# Zero-PHP Progress Status

This status is generated from the tracked inventory, ownership plan, and exact-route cutover batches. It reports true production progress separately from planning progress so we do not overstate 0% PHP readiness.

## Current percentage

- True zero-PHP completion: 35.0%.
- Pending to 100%: 65.0%.
- Foundation/planning floor: 35.0%.
- Route/job implementation complete: 0.0%.
- Route/job parity-ready: 0.0%.
- Route/job shadow-or-better: 0.0%.

## Inventory

- Total PHP files: 3049.
- Job-like PHP files: 140.

| Surface | PHP files |
| --- | ---: |
| api | 53 |
| bos | 9 |
| cp | 638 |
| erp | 431 |
| platform | 812 |
| storefront | 1106 |

## Planning progress

- Ownership assigned: 3049 (100.0%).
- Batch assignments: 3049 (100.0%).
- Total exact-route batches: 61.

## Next execution order

- Implement batch 1 worker replacements in ASP.NET Core dry-run mode. Batch 1 now has a generated worker replacement catalog scaffold, dry-run planner, parity reporter, and dry-run evidence manifest in `aspnet/src/EcomAE.Workers/ZeroPhpBatchOneWorkerReplacement.cs`, `aspnet/src/EcomAE.Workers/ZeroPhpBatchOneWorkerReplacementRunner.cs`, `aspnet/src/EcomAE.Workers/ZeroPhpBatchOneWorkerParityReporter.cs`, and `aspnet/src/EcomAE.Workers/ZeroPhpBatchOneWorkerDryRunEvidence.cs`.
- Attach PHP-vs-ASP.NET parity samples for each route/job in the batch.
- Move passing batch items to aspnet-shadow, then parity-ready, then live.
- Repeat batches without broad proxy cutover.
- Remove PHP only after every item is live or removed and rollback evidence is approved.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass.
