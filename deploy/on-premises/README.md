# ecomae ERP — On-Premises Deployment

Self-hosted deployment package for clients who want to run ecomae ERP on their own infrastructure.

> **New:** trying this on your own laptop instead of a server? See
> [`DESKTOP_QUICKSTART.md`](./DESKTOP_QUICKSTART.md) — same install, sized
> down for macOS/Windows Docker Desktop, no domain or certbot needed.

## How licensing works

The app's core engine (`core/dp_core.php`, `dp_content.php`, `dp_module.php`,
`dp_template.php`, and `config.php`) is proprietary and is **not included**
in this repository — you cannot `git clone` a runnable app on its own.
Instead, `install.sh` clones the rest of the codebase and then calls
`activate-license.php`, which contacts ecomae's license server, validates
your license key, and downloads those core files (plus a signed activation
certificate) directly onto your server. Nothing below Step 7 in Quick Start
will work until this activation step succeeds.

## Quick Start (Linux server, Docker)

```bash
git clone https://github.com/epartscart/ecomae.git ecomae-deploy
cd ecomae-deploy/deploy/on-premises
sudo ./install.sh --license YOUR_KEY --domain erp.yourcompany.com
```

`install.sh` handles everything: checks requirements, clones the application
code into `./app`, builds the Docker images, starts services, activates your
license, and runs the setup wizard's schema migrations.

Prefer to do it by hand? See [Manual Setup](#manual-setup) below.

## Files

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Full service stack (PHP, Nginx, MySQL, Redis, Cron) |
| `Dockerfile` | PHP image with the extensions this app requires (pdo_mysql, redis, gd, intl, zip, bcmath, opcache) + healthcheck binary |
| `.env.example` | Environment configuration template |
| `install.sh` | One-command installer script (Linux) |
| `DESKTOP_QUICKSTART.md` | macOS/Windows Docker Desktop trial guide |
| `setup-wizard.php` | Verifies core engine + license, runs the real platform schema migrations |
| `epc_license_manager.php` | License validation, online/offline activation, core-bundle install, signature verification |
| `license_public_key.pem` | Public key used to verify activation certificates (safe to ship, cannot forge licenses) |
| `activate-license.php` | CLI license activation (online + offline) |
| `backup.php` | Automated backup (DB + files, encryption, retention) |
| `health-check.php` | System health monitoring + BOS reporting |

## License Activation

### Online (server has internet)
```bash
docker compose exec app php /var/www/html/deploy/on-premises/activate-license.php LIC-2026-XXXX-XXXX
```
On success this also installs the core engine files and `config.php` — required before the setup wizard or any page will work.

### Offline (air-gapped)
```bash
# Generate request file
docker compose exec app php /var/www/html/deploy/on-premises/activate-license.php --request

# Send the generated file to your ecomae account manager.
# They will send back an activation certificate AND the core engine bundle
# (offline installs cannot fetch the bundle automatically, since there is no
# outbound call) — install both, then:
docker compose exec app php /var/www/html/deploy/on-premises/activate-license.php --offline /path/to/cert.txt
```

## Manual Setup

```bash
git clone https://github.com/epartscart/ecomae.git ecomae-deploy
cd ecomae-deploy/deploy/on-premises
cp .env.example .env
# Edit .env: set LICENSE_KEY, DB_PASSWORD, DB_ROOT_PASSWORD, APP_URL, etc.

# Fetch the application code into ./app (same repo as this package)
git clone --depth 1 https://github.com/epartscart/ecomae.git app

docker compose build
docker compose up -d

docker compose exec app php /var/www/html/deploy/on-premises/activate-license.php YOUR_KEY
docker compose exec app php /var/www/html/deploy/on-premises/setup-wizard.php
```

Then open `https://<your-domain>/cp/` to finish company profile and create your first admin user through the app's own onboarding flow.

## BOS Health Reporting

Optionally report basic health (disk, memory, DB size, backup status) to ecomae for monitoring. Authenticated by your license key — no separate token needed.

Set in `.env`:
```
BOS_SYNC_MODE=monitoring_only  # or: disabled
```

## Backups

Automated daily at 02:00 via cron. Configurable:
- Retention: `BACKUP_RETENTION_DAYS` (default: 30)
- Encryption: `BACKUP_ENCRYPT` (default: true)
- Remote upload: `BACKUP_REMOTE_URL` (S3 pre-signed URL or custom endpoint)

Manual backup:
```bash
docker compose exec cron php /var/www/html/deploy/on-premises/backup.php
```

## System Requirements

| Component | Minimum | Recommended | Enterprise |
|-----------|---------|-------------|------------|
| CPU | 4 cores | 8 cores | 16+ cores |
| RAM | 8 GB | 16 GB | 32+ GB |
| Storage | 100 GB SSD | 500 GB SSD | 1 TB+ NVMe |
| OS | Ubuntu 22.04+ / RHEL 9+ | | |
| Docker | 24+ with Compose v2 | | |

For a much smaller local trial on macOS/Windows, see `DESKTOP_QUICKSTART.md`.

## Issuing licenses (ecomae staff only)

Run on the platform server:
```bash
php epc-onprem-license-generate.php --customer "Acme Corp" --tier professional --users 50 --days 365
php epc-onprem-license-generate.php --list
php epc-onprem-license-generate.php --revoke LIC-2026-XXXX-XXXX
```

Requires a one-time RSA keypair on the platform server (private key never leaves it):
```bash
openssl genrsa -out /etc/ecomae/license_signing_key.pem 2048
openssl rsa -in /etc/ecomae/license_signing_key.pem -pubout -out deploy/on-premises/license_public_key.pem
```
Commit the resulting `license_public_key.pem` — it is public by design and is what every on-prem client uses to verify activation certificates.
