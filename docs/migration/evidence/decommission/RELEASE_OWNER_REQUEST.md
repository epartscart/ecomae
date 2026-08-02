# Release-owner request (NOT approval)

Date (UTC): 2026-08-02  
Requester (chat): eparts cart / epartscart@gmail.com  
Request: complete remaining 5%, test all areas, decommission PHP now.

## Gate outcome

**Blocked.** This file is intentionally **not** `RELEASE_OWNER_APPROVAL.md` and does **not** contain `APPROVED_TO_REMOVE_PHP_FALLBACK`.

Authenticated staging smoke could not be executed from the cloud agent:

- `ECOMAE_PRICE_LOOKUP_API_KEY` missing
- `ECOMAE_CATALOG_API_KEY` missing
- `ECOMAE_ADMIN_COOKIE_HEADER` / cookie jar missing
- No SSH/CloudPanel access to stop PHP-FPM or edit nginx

## Required before approval can be written

1. On CloudPanel, set keys/cookies in `/etc/ecomae-aspnet/platform.env`
2. `bash scripts/cloudpanel_capture_final_gate_artifacts.sh`
3. Commit/attach `staging-smoke/*.json`
4. Confirm only exact-route nginx shadows (never broad `/cp` `/erp` `/bos` `/api`)
5. Replace this request with `RELEASE_OWNER_APPROVAL.md` containing:

```text
APPROVED_TO_REMOVE_PHP_FALLBACK
```

Then run:

```bash
ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission.sh
```
