# Write dry-run dual-sample evidence

Wave B ASP.NET write dry-runs always return `writes=0` / `cutoverAllowed=false`.
Live PHP ajax endpoints remain authoritative until paired samples are captured and a human signs off.

Operator: `scripts/cloudpanel_run_write_dryrun_dual_sample_operator.sh`

Never invent `RELEASE_OWNER_APPROVAL.md`.
