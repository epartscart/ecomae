# On-premises install host scaffold

Target end-state: ASP.NET Core self-hosted installer replaces PHP `deploy/on-premises/install.sh` + compose pack.

| Concern | Today (PHP) | ASP.NET track |
| --- | --- | --- |
| Setup wizard | `setup-wizard.php` | dry-run + this scaffold |
| Backup | `backup.php` | dry-run + `Backup.Scaffold.md` |
| Health | `health-check.php` | dry-run + `HealthCheck.Scaffold.md` |
| License activate | `activate-license.php` | dry-run + `ActivateLicense.Scaffold.md` |
| Compose / systemd | PHP pack | future — not cut over |

Distinct from SaaS `TenantMode.ErpOnlyTenant`. Both tracks must reach 0 PHP.

Operator board: `GET /migration/on-premises-parity`
