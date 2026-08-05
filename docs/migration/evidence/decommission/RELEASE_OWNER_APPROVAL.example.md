# Release-owner approval (EXAMPLE ONLY — do not treat as approval)

Replace this file with `RELEASE_OWNER_APPROVAL.md` only after:

1. Staging smoke artifacts are attached under `staging-smoke/`
2. Parity samples are attached under `parity-samples/`
3. Exact-route nginx shadows (not broad trees) are validated
4. Rollback was tested (`scripts/rollback_aspnet_foundation.sh --keep-php-fallback` / remove shadow conf)

**Architecture note (confirmed separately):** ASP.NET Core as live primary with PHP kept as **reference** for previous results / gap-finding is documented in `docs/migration/PHP_AS_REFERENCE_MODE.md`. That confirmation is **not** this approval. This file is only for removing PHP **fallback/traffic**, not for deleting the reference project.

Required marker line in the real approval file:

```text
APPROVED_TO_REMOVE_PHP_FALLBACK
```

Also record:

- Approver name/email
- Date (UTC)
- Scope (which exact routes/jobs)
- Rollback owner on-call
- Whether PHP reference host/docroot remains installed (`KeepPhpProjectAvailable=true` recommended)
