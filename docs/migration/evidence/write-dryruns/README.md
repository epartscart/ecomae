# Write dry-run dual-sample evidence

Wave B ASP.NET write dry-runs always return `writes=0` / `cutoverAllowed=false`.
Live PHP ajax endpoints remain authoritative until paired samples are captured and a human signs off.

Includes storefront quote submit (`ajax_quote_submit.php`) and quote accept (`ajax_quote_accept.php`; cart INSERTs stay PHP).

Operator: `scripts/cloudpanel_run_write_dryrun_dual_sample_operator.sh`

Never invent `RELEASE_OWNER_APPROVAL.md`.
