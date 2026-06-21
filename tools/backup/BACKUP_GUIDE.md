# Backup & Recovery Guide — ECOM AE / eParts Cart

## Quick Start

### Run a backup now
```bash
sudo bash /home/ecomae/htdocs/www.ecomae.com/tools/backup/epc_backup.sh
```

### Set up daily automatic backups (3 AM)
```bash
(sudo crontab -l 2>/dev/null; echo "0 3 * * * /home/ecomae/htdocs/www.ecomae.com/tools/backup/epc_backup_cron.sh >> /home/ecomae/backups/cron.log 2>&1") | sudo crontab -
```

### Restore from backup
```bash
# List available backups
ls /home/ecomae/backups/

# Full restore
sudo bash /home/ecomae/htdocs/www.ecomae.com/tools/backup/epc_restore.sh 20260621_120000

# Restore only storefront-critical files (fastest)
sudo bash /home/ecomae/htdocs/www.ecomae.com/tools/backup/epc_restore.sh --critical-only 20260621_120000

# Restore only docroot (no DB)
sudo bash /home/ecomae/htdocs/www.ecomae.com/tools/backup/epc_restore.sh --docroot-only 20260621_120000

# Restore only databases
sudo bash /home/ecomae/htdocs/www.ecomae.com/tools/backup/epc_restore.sh --db-only 20260621_120000
```

## What Gets Backed Up

| Component | Location | Backup Path |
|-----------|----------|-------------|
| Full docroot | `/home/ecomae/htdocs/www.ecomae.com` | `docroot/www.ecomae.com.tar.gz` |
| Critical storefront files | 15 key files (see below) | `critical/` (individual copies) |
| MySQL databases | All tenant DBs | `mysql/<db>.sql.gz` |
| Nginx configs | `/etc/nginx/sites-*` | `config/nginx-*` |
| SSL certificates | `/etc/letsencrypt/live` | `config/ssl-certs/` |
| Cron jobs | root + ecomae crontabs | `cron/` |
| Import pause status | `/tmp/epc_imports_paused` | `config/import_pause_status.txt` |
| Version snapshot | EPC_CATA_VERSION + file hashes | `config/` |

## Critical Storefront Files

These files are deployed by Cursor via `force_push_one.py` and are NOT in Git.
They get a separate copy in `critical/` for fast restore:

- `content/general_pages/epart_catalog_front_links.php` (homepage 6 sections)
- `content/general_pages/epc_cata_config.php` (version + catalog config)
- `content/general_pages/epc_vc_catalog_ui.js` (catalog UI JS)
- `content/general_pages/epc_car_mod_theme.css` (catalog theme)
- `content/general_pages/epc_carcat_config.php` (original catalog config)
- `content/original_catalog.php` (original catalog page)
- `api/carcat_proxy.php` (CarCat API proxy)
- `api/epc_home_widgets.php` (homepage widget renderers)
- `content/shop/docpart/epc_product_family.php` (product family)
- `content/shop/docpart/part_search_manufacturers_index.php` (/en/parts)
- `templates/nero/desktop.php` (template includes)
- `core/dp_core.php` (nav/footer bootstrap)
- `epc-home-widgets-warm.php` (cache warm)
- `epc-pf-own-home-verify.php` (verify probe)
- `epc-home-original-verify.php` (original catalog probe)

## Safe Deploy Rules

### Before any `git pull` on the server:
1. Run a backup first: `sudo bash tools/backup/epc_backup.sh`
2. Check if Git changes touch any critical file above
3. If they do, diff against live before deploying

### After any deploy:
1. Warm widget caches:
   ```
   curl -sk --max-time 300 "https://www.epartscart.com/epc-home-widgets-warm.php?token=epartscart-deploy-2026&lang=/en"
   ```
2. Verify storefront:
   ```
   curl -sk --max-time 60 "https://www.epartscart.com/epc-pf-own-home-verify.php?token=epartscart-deploy-2026&fast=1"
   ```
3. Check BOS:
   ```
   curl -sk -o /dev/null -w "%{http_code}" "https://www.ecomae.com/bos/"
   ```

## Retention

- Default: 7 days of daily backups
- Change `RETENTION_DAYS` in `epc_backup.sh` to adjust
- Backups stored in `/home/ecomae/backups/<YYYYMMDD_HHMMSS>/`

## Lane Split (who deploys what)

| Lane | Owner | Deploy Method |
|------|-------|---------------|
| Storefront (homepage, PF, catalog, parts, CP wiring) | Cursor | `force_push_one.py` |
| BOS + backend / ERP / imports / platform | Devin | Git deploy |

**Rule:** Devin must NOT `git reset --hard` or blindly overwrite storefront files.
Always backup first, always diff against live.
