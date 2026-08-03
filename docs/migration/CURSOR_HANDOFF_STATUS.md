# Cursor Handoff Status: ASP.NET Core Migration Foundation

## Canonical law

All work must follow Enterprise BOS architecture:

- `docs/migration/PROJECT_ARCHITECTURE_INSTRUCTIONS.md` (project law)
- `docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md` (live vs target)
- `docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md` (Zero-PHP % only)

Do not use this handoff table as the source of truth for completion percentages.

## Handoff summary

ASP.NET Core migration foundation and diagnostics-only deploy tooling are in place. Remaining work is exact-route Zero-PHP parity, Enterprise BOS stack scaffolding (EF Core/PG17/YARP/OTel/Redis/Kafka), CloudPanel operations, and staging smoke — without broad PHP cutover.

## Completion percentages (pointers)

| Area | Where to read |
| --- | --- |
| Zero-PHP true completion | `docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md` |
| Enterprise BOS stack readiness | `docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md` |
| Repository foundation / deploy guardrails | Present under `aspnet/`, `deploy/aspnet/`, `scripts/` |

## Cursor's standing tasks

1. Keep ASP.NET Core as sole enterprise backend; Python AI-only; no Java/Node/Go/PHP backends.
2. Continue exact-route Zero-PHP digests/parity with PHP fallback required.
3. Advance EF Core / observability / YARP scaffolding without claiming infra live.
4. Redeploy smoke-issuer via `bash scripts/cloudpanel_redeploy_final_gate_branch.sh` (or `origin/main` after merge).
5. Ensure table → issue smoke creds → validate → capture/commit before any `location =` nginx shadows.

## Do not do yet

- Do not remove PHP fallback.
- Do not proxy broad `/api`, `/cp`, `/erp`, `/bos`, or storefront locations.
- Do not enable worker write jobs beyond dry-run until job parity is approved.
- Do not claim PostgreSQL 17, Redis, Kafka, YARP, or Vault are production-live.
- Do not expand `pyapi` business APIs.
- Do not commit production credentials or paste secrets into PR comments.
