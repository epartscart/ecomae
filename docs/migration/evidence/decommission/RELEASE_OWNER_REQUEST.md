# Release-owner request (NOT approval)

Date (UTC): 2026-08-03  
Requester (chat): eparts cart / epartscart@gmail.com  
Request: complete remaining 5%, test all areas, decommission PHP now.

## Gate outcome

**Blocked — do not remove PHP.** This file is intentionally **not** `RELEASE_OWNER_APPROVAL.md` and does **not** contain `APPROVED_TO_REMOVE_PHP_FALLBACK`.

Authenticated **loopback** CloudPanel staging smoke is attached on `main` (PR #612):

- `staging-smoke/price-lookup-aspnet.json` — authenticated price lookup
- `staging-smoke/catalog-status-aspnet.json` — catalog status (`connected=true`)
- `staging-smoke/surface-digests-aspnet.json` — `ok=true`, 30 non-migration digest HTTP 200s
- Readiness: **8/9** (`readyToRemovePhp=false`; only approval marker missing)

Public frontend/backend authority is **still PHP** for product chrome:

- `https://www.ecomae.com/`, `/CP/`, `/ERP/`, `/BOS/` → PHP HTML
- Public `/cp/dashboard-summary` is **not** cut over yet
- Approved exact-route API shadows on www: `/health`, `/migration/*`, `/api/v1/price/lookup`, `/api/v1/catalog/status`, `/api/v1/catalog/manufacturers`, `/api/v1/catalog/models` (401/200 ASP.NET JSON)
- Live `/migration/surface-parity` → `parity-not-yet-reached`
- Live `/migration/presentation-parity` → `presentation-shell-scaffolded` only

Approval must **not** be written until remaining surfaces have exact-route shadows + dual-sample PHP↔ASP.NET parity where required. Loopback smoke + two API shadows are insufficient for PHP removal.

## Required before approval can be written

1. Redeploy `main` and confirm readiness smoke items **present** (approval still missing)
2. Run `bash scripts/verify_pre_php_removal_parity.sh` (must stay fail-closed / PHP remains)
3. Promote only approved `location =` nginx shadows one path at a time; attach dual PHP↔ASP.NET samples
4. Confirm only exact-route nginx shadows (never broad `/cp` `/erp` `/bos` `/api`)
5. A human release owner replaces this request with `RELEASE_OWNER_APPROVAL.md` containing:

```text
APPROVED_TO_REMOVE_PHP_FALLBACK
```

Then run:

```bash
ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission.sh
```

Do **not** invent approval. Do **not** remove PHP from automation without that marker.
