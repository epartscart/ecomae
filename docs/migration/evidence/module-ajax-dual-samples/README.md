# Module ajax dual-sample evidence

ASP.NET `POST /cp/module-ajax/...` dry-runs always return `writes=0` / `cutoverAllowed=false`.
Live PHP ajax/forms remain authoritative until paired field-level samples and human sign-off.

| Helper | Role |
| --- | --- |
| `scripts/cloudpanel_capture_module_ajax_dual_samples.sh` | Capture catalog + curated dedicated dry-runs |
| `scripts/compare_module_ajax_dual_samples.py` | Assert writes=0 / cutover false |
| `scripts/cloudpanel_run_module_ajax_dual_sample_operator.sh` | Capture + compare operator |

Catalog board: `GET /cp/module-ajax/writes/catalog`

Never invent `RELEASE_OWNER_APPROVAL.md`.
