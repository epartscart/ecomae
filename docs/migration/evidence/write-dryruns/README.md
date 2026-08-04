# Write dry-run dual-sample evidence

Wave B ASP.NET write dry-runs always return `writes=0` / `cutoverAllowed=false`.
Live PHP ajax endpoints remain authoritative until paired samples are captured and a human signs off.

## Offline contract floor

```bash
bash scripts/cloudpanel_run_write_dryrun_dual_sample_operator.sh
```

Expected: **183/183** unique probe paths with `php-*` + `aspnet-*` migration-contract-golden pairs
(`docs/migration/evidence/write-dryruns/compare-result.json`).

Includes storefront quote submit (`ajax_quote_submit.php`) and quote accept (`ajax_quote_accept.php`; cart INSERTs stay PHP).

## Companions

- Module ajax: `scripts/cloudpanel_run_module_ajax_dual_sample_operator.sh` (CP **254/254**)
- ERP ajax: `scripts/compare_erp_ajax_dual_samples.py --contract-only` (**321/321**)
- BOS ajax: `scripts/compare_bos_ajax_dual_samples.py --contract-only` (**231/231**)

Never invent `RELEASE_OWNER_APPROVAL.md`. Never delete PHP from this floor.
