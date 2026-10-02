# B7 Treasury recovery and rollback evidence

This runbook defines the operator evidence required before Treasury can
advance from local rehearsal to production acceptance. It does not grant
cutover permission and must not be filled with throwaway-database evidence.

## Required bundle

Create a private operator bundle outside the repository and validate it with:

```bash
python3 scripts/validate_b7_recovery_bundle.py /secure/evidence/<tenant>-b7-recovery.json
```

The bundle must contain:

```json
{
  "tenant": "epartscart",
  "process": "B7",
  "cutoverAllowed": false,
  "keepPhpFallback": true,
  "readyForPhpRemoval": false,
  "release": {
    "sha": "<active-release-sha>",
    "health": "<health-check-reference>",
    "routeOwnerBefore": "aspnet"
  },
  "productionBackup": {
    "reference": "<approved-production-backup-reference>",
    "checksum": "<backup-checksum>",
    "capturedAt": "<utc-timestamp>"
  },
  "restoreReadback": {
    "reference": "<restore-target-readback>",
    "capturedAt": "<utc-timestamp>"
  },
  "ownershipRollback": {
    "reference": "<exact-route-rollback-reference>",
    "nginxTest": "pass",
    "reload": "pass",
    "phpRouteSmoke": "pass",
    "aspnetRouteOwnerAfter": "php"
  },
  "cleanup": {
    "reference": "<tagged-fixture-cleanup-reference>"
  },
  "releaseOwnerApproval": {
    "reference": "<human-approval-reference>"
  }
}
```

## Operator sequence

1. Record the active release SHA and `/health` response before the rehearsal.
2. Capture an approved production tenant backup and checksum without modifying
   PHP/PHP-FPM ownership.
3. Restore into the approved recovery target and read back cash accounts,
   bank-entry/reconciliation state, payment-batch state, collections state,
   audit rows where PHP emits them, and relevant voucher sequences.
4. Exercise only the exact ASP.NET Treasury route under test, then remove its
   exact Nginx proxy ownership block.
5. Run `nginx -t`, reload Nginx, and prove the same route is served by PHP.
6. Remove tagged rehearsal data and record the cleanup result.
7. Attach release-owner approval only after production smoke and rollback
   results are reviewed.

The bundle is incomplete when any reference is missing, points at throwaway
evidence, or reports ASP.NET as the post-rollback owner. Keep PHP fallback
reachable and do not create `RELEASE_OWNER_APPROVAL.md` in the repository.
