# On-premises install host scaffold

Target end-state: ASP.NET Core self-hosted installer replaces PHP `deploy/on-premises/install.sh` + compose pack.

The first control-plane contract is documented in
`docs/tenant-installation-control-plane.md`. A generated manifest follows
`Install.Manifest.example.json`; it contains an expiring enrollment request id,
not database credentials or a reusable cloud secret.
The step-by-step operator procedure is in `docs/tenant-installation-guide.md`.

| Concern | Today (PHP) | ASP.NET track |
| --- | --- | --- |
| Setup wizard | `setup-wizard.php` | dry-run + this scaffold |
| Backup | `backup.php` | dry-run + `Backup.Scaffold.md` |
| Health | `health-check.php` | dry-run + `HealthCheck.Scaffold.md` |
| License activate | `activate-license.php` | dry-run + `ActivateLicense.Scaffold.md` |
| Compose / systemd | PHP pack | future — not cut over |

The cloud control plane must create the tenant first, classify it as `on-premises`,
and generate the manifest before the local setup package is run. The local package
must enroll over outbound HTTPS; it must not require an inbound management port.

Distinct from SaaS `TenantMode.ErpOnlyTenant`. Both tracks must reach 0 PHP.

Operator board: `GET /migration/on-premises-parity`
