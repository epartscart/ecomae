# Release-owner approval (EXAMPLE ONLY — do not treat as approval)

Replace this file with `RELEASE_OWNER_APPROVAL.md` only after:

1. Staging smoke artifacts are attached under `staging-smoke/`
2. Parity samples are attached under `parity-samples/`
3. Exact-route nginx shadows (not broad trees) are validated
4. Rollback was tested (`scripts/rollback_aspnet_foundation.sh` / remove shadow conf)

Required marker line in the real approval file:

```text
APPROVED_TO_REMOVE_PHP_FALLBACK
```

Also record:

- Approver name/email
- Date (UTC)
- Scope (which exact routes/jobs)
- Rollback owner on-call
