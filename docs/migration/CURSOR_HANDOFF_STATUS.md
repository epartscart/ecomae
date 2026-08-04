# Cursor Handoff Status: ASP.NET Core Migration Foundation

## Canonical law

All work must follow Enterprise BOS architecture:

- `docs/migration/PROJECT_ARCHITECTURE_INSTRUCTIONS.md` (project law)
- `docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md` (live vs target)
- `docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md` (Zero-PHP % only)

Do not use this handoff table as the source of truth for completion percentages.

## Handoff summary

ASP.NET Core migration foundation and diagnostics-only deploy tooling are in place. Authenticated final-gate staging smoke is attached on `main` (PR #612). Remaining Zero-PHP 5% is human `RELEASE_OWNER_APPROVAL.md` plus exact-route shadow promotion / Enterprise BOS scaffolding — without broad PHP cutover.

## Completion percentages (pointers)

| Area | Where to read |
| --- | --- |
| Zero-PHP true completion | `docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md` |
| Enterprise BOS stack readiness | `docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md` |
| Repository foundation / deploy guardrails | Present under `aspnet/`, `deploy/aspnet/`, `scripts/` |

## Cursor's standing tasks

1. Keep ASP.NET Core as sole enterprise backend; Python AI-only; no Java/Node/Go/PHP backends.
2. Continue exact-route Zero-PHP digests/parity with PHP fallback required (hybrid digest `*-app` wave complete on tip). Offline floor: `bash scripts/cloudpanel_run_all_dual_sample_operators.sh`. Live cookie/API captures remain CloudPanel operator work.
3. Advance Enterprise BOS scaffolding without claiming infra live; still no production clients or Replace* flags. Run `bash scripts/validate_enterprise_bos_scaffold_guardrails.sh` (includes evidence cutover locks + presentation/hybrid allowlist sync) before wiring anything. GitOps/Helm examples are design-only. YARP regenerate: `generate_all_yarp_design_examples.sh`.
4. Redeploy main via `bash scripts/cloudpanel_redeploy_final_gate_branch.sh` so ContentRoot packs smoke evidence.
5. Confirm readiness smoke items present; promote only approved `location =` shadows; never invent `RELEASE_OWNER_APPROVAL.md`.

## Do not do yet

- Do not remove PHP fallback.
- Do not proxy broad `/api`, `/cp`, `/erp`, `/bos`, or storefront locations.
- Do not enable worker write jobs beyond dry-run until job parity is approved.
- Do not claim PostgreSQL 17, Redis, Kafka, YARP, or Vault are production-live.
- Do not expand `pyapi` business APIs.
- Do not commit production credentials or paste secrets into PR comments.
