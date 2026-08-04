# On-premises dual-sample evidence

Contract floor for PHP `deploy/on-premises/*` + license/health APIs vs ASP.NET dry-runs.

## Hard locks

- `cutoverAllowed=false` / `readyForPhpRemoval=false` always
- Dry-runs are `writes=0` (`confirm_writes` refused)
- PHP pack remains authoritative until live field dual-sample **and** human `RELEASE_OWNER_APPROVAL.md`
- Never invent approval or delete PHP from this floor

## Offline floor

```bash
bash scripts/cloudpanel_run_on_premises_dual_sample_operator.sh
# or
bash scripts/cloudpanel_run_all_digest_hybrid_onprem_floors.sh
```

Expected: **6/6** contract pairs (`health`, `license-activate`, `setup-wizard`, `backup`, `activate-license-cli`, `health-check-pack`).

Licenses digest (`GET /erp/on-premises/licenses`) is a separate read surface — still awaiting live dual-sample.
