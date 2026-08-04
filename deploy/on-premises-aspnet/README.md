# On-premises ASP.NET Core installer pack (scaffold)

**Status:** scaffold only · PHP `deploy/on-premises/*` remains authoritative until dual-sample + human approval.

This directory is the ASP.NET Core replacement track for the PHP on-premises installer pack.
It is **not** the same as SaaS ERP-only tenant mode (`TenantMode.ErpOnlyTenant`).

| PHP (authoritative today) | ASP.NET scaffold |
| --- | --- |
| `deploy/on-premises/setup-wizard.php` | `POST /erp/on-premises/setup-wizard-dry-run` (writes=0) |
| `deploy/on-premises/backup.php` | `POST /erp/on-premises/backup-dry-run` (writes=0) |
| `deploy/on-premises/health-check.php` | `POST /erp/on-premises/health-dry-run` (writes=0) |
| `deploy/on-premises/activate-license.php` | `POST /erp/on-premises/license-activate-dry-run` (writes=0) |
| `docker-compose.yml` / `install.sh` | Future ASP.NET hosted pack — not cut over |

`cutoverAllowed` stays **false**. Never invent `RELEASE_OWNER_APPROVAL.md`.

Operator board: `GET /migration/on-premises-parity`
