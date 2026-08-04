# On-premises health-check scaffold

**PHP authoritative today:** `deploy/on-premises/health-check.php` and `api/v1/on-premises/health.php`.

**ASP.NET dry-run (writes=0):**
- `POST /erp/on-premises/health-dry-run`
- `POST /erp/on-premises/health-check-pack-dry-run`

Future ASP.NET installer host should expose an equivalent health endpoint under the self-hosted pack — not SaaS ERP-only mode.

`cutoverAllowed=false` until dual-sample + human approval.
