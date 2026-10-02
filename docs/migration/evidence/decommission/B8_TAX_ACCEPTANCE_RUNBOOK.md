# B8 tax and e-invoice acceptance evidence

This runbook defines the evidence required before B8 Tax/E-Invoice can advance
to formal acceptance. The rules are tenant-country-driven: the registered
country on the tenant company profile selects the compliance profile. A country
preview or dropdown must never override that registered country.

## Required bundle

Create a private operator bundle outside the repository and validate it with:

```bash
python3 scripts/validate_b8_acceptance_bundle.py /secure/evidence/<tenant>-b8-acceptance.json
```

The bundle must contain:

```json
{
  "tenant": "epartscart",
  "tenantCountry": "AE",
  "process": "B8",
  "cutoverAllowed": false,
  "keepPhpFallback": true,
  "readyForPhpRemoval": false,
  "release": {
    "sha": "<active-release-sha>",
    "routeOwnerBefore": "aspnet"
  },
  "countryProfile": {
    "reference": "<registered-company-country-and-profile-record>",
    "registeredCountry": "AE",
    "previewOverride": false
  },
  "functional": {
    "reference": "<tenant-country-functional-record>",
    "status": "pass",
    "happyPath": "<withholding-and-e-invoice-happy-path>",
    "denialPath": "<validation-and-country-denial-path>",
    "persistedReadback": "<sql-and-audit-readback>",
    "browserReadback": "<authenticated-browser-readback>"
  },
  "tenantIsolation": {
    "reference": "<physical-tenant-isolation-record>",
    "status": "pass",
    "tenantTwoReadback": "<tenant-two-only-sql-and-browser-readback>",
    "tenantOneNonVisibility": "<tenant-one-negative-readback>"
  },
  "productionRecovery": {
    "reference": "<production-backup-restore-rollback-record>",
    "status": "pass",
    "backupChecksum": "<approved-backup-checksum>",
    "restoreReadback": "<recovery-target-readback>",
    "rollbackRouteOwner": "php"
  },
  "productionSmoke": {
    "reference": "<production-tax-smoke-record>",
    "status": "pass",
    "routeOwner": "aspnet"
  },
  "releaseIdentity": {
    "reference": "<release-identity-record>",
    "expectedSha": "<intended-release-sha>",
    "observedSha": "<active-RELEASE_SHA>",
    "matches": true
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

1. Resolve the tenant registration country from the company profile and record
   the country profile used for tax, withholding, and e-invoice obligations.
2. Run tenant-country functional tests for withholding and e-invoice happy and
   denial paths, including confirmed persistence, SQL/audit readback, and
   authenticated browser readback.
3. Confirm that a preview country cannot change the registered-country
   compliance result.
4. Prove physical tenant isolation with separate tenant databases: readback must
   be visible only to the writing tenant, and the other tenant must have no
   matching SQL or browser visibility.
5. Capture production backup/checksum, restore readback, exact-route rollback
   to PHP, and cleanup evidence without removing PHP/PHP-FPM ownership.
6. Capture production smoke while the exact B8 route is ASP.NET-owned, then
   compare the intended release SHA with
   `/var/www/ecomae-aspnet/current/platform/RELEASE_SHA`.
7. Attach release-owner approval only after all country, isolation, recovery, smoke,
   identity, and cleanup results are reviewed.

The bundle is incomplete when any reference is missing, points at throwaway
evidence, the registered country differs from `tenantCountry`, a preview
country overrides it, tenant two is visible from tenant one, the release SHA
does not match, or rollback does not return ownership to PHP. Keep PHP fallback
reachable and broad cutover disabled; this contract does not assert that any
production evidence exists.
