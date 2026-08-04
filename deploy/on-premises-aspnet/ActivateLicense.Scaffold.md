# On-premises activate-license scaffold

**PHP authoritative today:** `deploy/on-premises/activate-license.php` and `api/v1/licenses/activate.php`.

**ASP.NET dry-run (writes=0):**
- `POST /erp/on-premises/license-activate-dry-run`
- `POST /erp/on-premises/activate-license-cli-dry-run`

Cert signing / license key issuance stays PHP until dual-sample + human approval.

`cutoverAllowed=false`. Never invent `RELEASE_OWNER_APPROVAL.md`.
