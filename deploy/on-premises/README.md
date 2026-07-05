# ecomae ERP — On-Premises Deployment

Self-hosted deployment package for clients who want to run ecomae ERP on their own infrastructure.

## Quick Start (Docker)

```bash
curl -sSL https://install.ecomae.com | bash -s -- --license YOUR_KEY --domain erp.yourcompany.com
```

Or manually:

```bash
git clone https://github.com/epartscart/ecomae.git
cd ecomae/deploy/on-premises
cp .env.example .env
# Edit .env with your settings
docker compose up -d
docker compose exec app php /var/www/html/deploy/on-premises/setup-wizard.php
```

## Files

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Full service stack (PHP, Nginx, MySQL, Redis, Cron) |
| `.env.example` | Environment configuration template |
| `install.sh` | One-command installer script |
| `setup-wizard.php` | Initial database + admin + company setup |
| `epc_license_manager.php` | License validation, activation, enforcement |
| `activate-license.php` | CLI license activation (online + offline) |
| `backup.php` | Automated backup (DB + files, encryption, retention) |
| `health-check.php` | System health monitoring + BOS reporting |

## License Activation

### Online (server has internet)
```bash
docker compose exec app php /var/www/html/deploy/on-premises/activate-license.php LIC-XXXX-XXXX-XXXX
```

### Offline (air-gapped)
```bash
# Generate request file
docker compose exec app php /var/www/html/deploy/on-premises/activate-license.php --request

# Upload the generated file to BOS portal → On-Premises → Offline Activation
# Download the activation certificate, then:
docker compose exec app php /var/www/html/deploy/on-premises/activate-license.php --offline /path/to/cert.txt
```

## BOS Connector

Optionally connect on-premises instances to your cloud BOS for:
- Health monitoring (disk, memory, DB size, backup status)
- Automatic updates
- Multi-branch data sync

Set in `.env`:
```
BOS_CONNECTOR_URL=https://www.ecomae.com
BOS_CONNECTOR_TOKEN=your_token
BOS_SYNC_MODE=monitoring_only  # or: full_sync, updates_only, disabled
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
| OS | Ubuntu 22.04+ / RHEL 9+ / Windows Server 2019+ | | |
| Docker | 24+ with Compose v2 | | |
