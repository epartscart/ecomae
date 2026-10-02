# B7 Treasury UAT and production acceptance evidence

This runbook defines the evidence required before B7 Treasury can advance from
rehearsal and recovery preparation to formal UAT and production acceptance. It
does not grant cutover permission and must not be filled with throwaway evidence.

## Required bundle

Create a private operator bundle outside the repository and validate it with:

```bash
python3 scripts/validate_b7_uat_acceptance_bundle.py /secure/evidence/<tenant>-b7-uat.json
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
    "routeOwnerBefore": "aspnet"
  },
  "uat": {
    "reference": "<signed-uat-record>",
    "status": "pass",
    "happyPath": "<cash-bank-payment-collections-uat>",
    "denialPath": "<validation-and-denial-uAT>",
    "browserReadback": "<authenticated-browser-readback>",
    "persistedReadback": "<sql-and-audit-readback>",
    "tenantIsolation": "<tenant-specific-isolation-readback>"
  },
  "productionSmoke": {
    "reference": "<production-smoke-record>",
    "status": "pass",
    "routeOwner": "aspnet"
  },
  "releaseIdentity": {
    "reference": "<release-identity-record>",
    "expectedSha": "<intended-release-sha>",
    "observedSha": "<active-RELEASE_SHA>",
    "matches": true
  },
  "recoveryRollback": {
    "reference": "<validated-b7-recovery-bundle>",
    "status": "pass",
    "validatedBundle": "pass"
  },
  "cleanup": {
    "reference": "<tagged-fixture-cleanup-record>"
  },
  "releaseOwnerApproval": {
    "reference": "<human-approval-record>"
  }
}
```

## Operator sequence

1. Confirm the intended production release SHA and compare it with
   `/var/www/ecomae-aspnet/current/platform/RELEASE_SHA`; health alone is not
   release identity evidence.
2. Run tenant-specific UAT for Treasury happy paths and denial paths without
   changing PHP/PHP-FPM ownership.
3. Read back the authenticated browser result, SQL persistence, PHP-emitted
   audit rows where applicable, and physical tenant-isolation result.
4. Capture production smoke while the exact Treasury route is ASP.NET-owned.
5. Validate the B7 recovery/rollback bundle separately, then reference its
   validated result.
6. Confirm tagged fixture cleanup and attach release-owner approval only after
   UAT, smoke, identity, recovery, and cleanup are reviewed together.

The bundle is incomplete when any reference is missing, points at throwaway
evidence, the observed release SHA differs from the intended SHA, production
smoke is not ASP.NET-owned, or the recovery bundle was not independently
validated. Keep PHP fallback reachable, keep broad cutover disabled, and do not
create approval-marker files in the repository.
