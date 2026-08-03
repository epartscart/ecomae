# Release-owner request (NOT approval)

Date (UTC): 2026-08-03  
Requester (chat): eparts cart / epartscart@gmail.com  
Request: complete remaining 5%, test all areas, decommission PHP now.

## Gate outcome

**Blocked on human approval only.** This file is intentionally **not** `RELEASE_OWNER_APPROVAL.md` and does **not** contain `APPROVED_TO_REMOVE_PHP_FALLBACK`.

Authenticated CloudPanel staging smoke is attached on `main` (PR #612):

- `staging-smoke/price-lookup-aspnet.json` — authenticated price lookup
- `staging-smoke/catalog-status-aspnet.json` — catalog status (`connected=true`)
- `staging-smoke/surface-digests-aspnet.json` — `ok=true`, 30 non-migration digest HTTP 200s
- Final-gate checklist on CloudPanel: **40 pass / 4 skip / 0 fail** (skips = approval + optional live smoke flags)

PHP remains authoritative. Broad `/api` `/cp` `/erp` `/bos` /storefront cutover remains forbidden.

## Required before approval can be written

1. Redeploy `main` so ContentRoot packs smoke evidence: `bash scripts/cloudpanel_redeploy_final_gate_branch.sh`
2. Confirm `/migration/php-decommission-readiness` shows smoke checklist items **present** (approval still missing)
3. Confirm only exact-route nginx shadows (never broad `/cp` `/erp` `/bos` `/api`)
4. A human release owner replaces this request with `RELEASE_OWNER_APPROVAL.md` containing:

```text
APPROVED_TO_REMOVE_PHP_FALLBACK
```

Then run:

```bash
ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission.sh
```

Do **not** invent approval. Do **not** remove PHP from automation without that marker.
