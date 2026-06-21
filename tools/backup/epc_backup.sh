#!/bin/bash
# ═══════════════════════════════════════════════════════════════
# ECOM AE / eParts Cart — Production Backup Script
# ═══════════════════════════════════════════════════════════════
#
# Usage:
#   sudo bash /path/to/epc_backup.sh
#
# Creates timestamped backups of:
#   1. Full docroot (PHP files, configs, static assets)
#   2. MySQL databases (all tenant databases)
#   3. Nginx/CloudPanel vhost configs
#   4. SSL certificates
#   5. Cron jobs
#
# Retention: keeps last 7 daily backups by default
# ═══════════════════════════════════════════════════════════════

set -euo pipefail

# ─── Configuration ────────────────────────────────────────────
DOCROOT="/home/ecomae/htdocs/www.ecomae.com"
BACKUP_DIR="/home/ecomae/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_PATH="${BACKUP_DIR}/${TIMESTAMP}"
RETENTION_DAYS=7
LOG_FILE="${BACKUP_DIR}/backup_${TIMESTAMP}.log"

# Storefront-critical files (from Cursor handoff — NEVER lose these)
CRITICAL_FILES=(
    "content/general_pages/epart_catalog_front_links.php"
    "content/general_pages/epc_cata_config.php"
    "content/general_pages/epc_vc_catalog_ui.js"
    "content/general_pages/epc_car_mod_theme.css"
    "content/general_pages/epc_carcat_config.php"
    "content/original_catalog.php"
    "api/carcat_proxy.php"
    "api/epc_home_widgets.php"
    "content/shop/docpart/epc_product_family.php"
    "content/shop/docpart/part_search_manufacturers_index.php"
    "templates/nero/desktop.php"
    "core/dp_core.php"
    "epc-home-widgets-warm.php"
    "epc-pf-own-home-verify.php"
    "epc-home-original-verify.php"
)

# ─── Functions ────────────────────────────────────────────────
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG_FILE"
}

die() {
    log "FATAL: $*"
    exit 1
}

# ─── Pre-flight checks ───────────────────────────────────────
[[ $EUID -eq 0 ]] || die "Must run as root (sudo)"
[[ -d "$DOCROOT" ]] || die "Docroot not found: $DOCROOT"

mkdir -p "$BACKUP_PATH"/{docroot,mysql,config,cron,critical}
log "=== Backup started: $TIMESTAMP ==="
log "Backup path: $BACKUP_PATH"

# ─── 1. Critical storefront files (separate copy for quick restore) ───
log "--- Step 1: Critical storefront files ---"
for f in "${CRITICAL_FILES[@]}"; do
    src="${DOCROOT}/${f}"
    if [[ -f "$src" ]]; then
        dest_dir="${BACKUP_PATH}/critical/$(dirname "$f")"
        mkdir -p "$dest_dir"
        cp -p "$src" "$dest_dir/"
        md5=$(md5sum "$src" | cut -d' ' -f1)
        size=$(stat -c%s "$src")
        log "  OK: $f (${size} bytes, md5=${md5})"
    else
        log "  MISSING: $f"
    fi
done

# ─── 2. Full docroot backup ──────────────────────────────────
log "--- Step 2: Full docroot backup ---"
tar czf "${BACKUP_PATH}/docroot/www.ecomae.com.tar.gz" \
    --exclude='*.tar.gz' \
    --exclude='node_modules' \
    --exclude='.git' \
    --exclude='backups' \
    -C "$(dirname "$DOCROOT")" \
    "$(basename "$DOCROOT")" 2>>"$LOG_FILE"
docroot_size=$(du -sh "${BACKUP_PATH}/docroot/www.ecomae.com.tar.gz" | cut -f1)
log "  Docroot backup: ${docroot_size}"

# ─── 3. MySQL databases ─────────────────────────────────────
log "--- Step 3: MySQL database backups ---"
# Get all ecomae-related databases
DBS=$(mysql -N -e "SHOW DATABASES" 2>/dev/null | grep -E 'ecomae|epart|ecart|tenant' || true)
if [[ -z "$DBS" ]]; then
    # Fallback: dump all non-system databases
    DBS=$(mysql -N -e "SHOW DATABASES" 2>/dev/null | grep -vE '^(information_schema|performance_schema|mysql|sys)$' || true)
fi
for db in $DBS; do
    log "  Dumping: $db"
    mysqldump --single-transaction --routines --triggers --events \
        "$db" 2>>"$LOG_FILE" | gzip > "${BACKUP_PATH}/mysql/${db}.sql.gz"
    db_size=$(du -sh "${BACKUP_PATH}/mysql/${db}.sql.gz" | cut -f1)
    log "  OK: $db (${db_size} compressed)"
done

# ─── 4. Server configs ──────────────────────────────────────
log "--- Step 4: Server configs ---"
# Nginx vhosts
if [[ -d /etc/nginx ]]; then
    cp -r /etc/nginx/sites-enabled "${BACKUP_PATH}/config/nginx-sites-enabled" 2>/dev/null || true
    cp -r /etc/nginx/sites-available "${BACKUP_PATH}/config/nginx-sites-available" 2>/dev/null || true
    log "  Nginx configs copied"
fi
# CloudPanel vhosts
if [[ -d /home/ecomae/htdocs ]]; then
    ls -la /home/ecomae/htdocs/ > "${BACKUP_PATH}/config/htdocs_listing.txt" 2>/dev/null || true
fi
# PHP-FPM pool config
cp /etc/php/*/fpm/pool.d/*ecomae* "${BACKUP_PATH}/config/" 2>/dev/null || true
# SSL certs
if [[ -d /etc/letsencrypt/live ]]; then
    cp -rL /etc/letsencrypt/live "${BACKUP_PATH}/config/ssl-certs" 2>/dev/null || true
    log "  SSL certs copied"
fi

# ─── 5. Cron jobs ────────────────────────────────────────────
log "--- Step 5: Cron jobs ---"
crontab -l > "${BACKUP_PATH}/cron/root_crontab.txt" 2>/dev/null || true
crontab -u ecomae -l > "${BACKUP_PATH}/cron/ecomae_crontab.txt" 2>/dev/null || true
log "  Cron jobs saved"

# ─── 6. Import pause status ─────────────────────────────────
log "--- Step 6: Import pause status ---"
if [[ -f /tmp/epc_imports_paused ]]; then
    log "  Import pause flag: ACTIVE"
    echo "PAUSED" > "${BACKUP_PATH}/config/import_pause_status.txt"
else
    log "  Import pause flag: NOT SET"
    echo "NOT_PAUSED" > "${BACKUP_PATH}/config/import_pause_status.txt"
fi

# ─── 7. Version + hash snapshot ──────────────────────────────
log "--- Step 7: Version snapshot ---"
if [[ -f "${DOCROOT}/content/general_pages/epc_cata_config.php" ]]; then
    grep 'EPC_CATA_VERSION' "${DOCROOT}/content/general_pages/epc_cata_config.php" > \
        "${BACKUP_PATH}/config/epc_cata_version.txt" 2>/dev/null || true
fi
if [[ -f "${DOCROOT}/content/general_pages/epart_catalog_front_links.php" ]]; then
    md5sum "${DOCROOT}/content/general_pages/epart_catalog_front_links.php" > \
        "${BACKUP_PATH}/config/front_links_hash.txt" 2>/dev/null || true
fi
log "  Version snapshot saved"

# ─── 8. Cleanup old backups ─────────────────────────────────
log "--- Step 8: Cleanup (retain ${RETENTION_DAYS} days) ---"
find "$BACKUP_DIR" -maxdepth 1 -type d -mtime +"$RETENTION_DAYS" -exec rm -rf {} \; 2>/dev/null || true
old_count=$(find "$BACKUP_DIR" -maxdepth 1 -type d ! -name 'backups' | wc -l)
log "  Remaining backups: $old_count"

# ─── Summary ─────────────────────────────────────────────────
total_size=$(du -sh "$BACKUP_PATH" | cut -f1)
log "=== Backup complete: ${total_size} total ==="
log "=== Location: $BACKUP_PATH ==="
echo ""
echo "Backup saved to: $BACKUP_PATH"
echo "Total size: $total_size"
echo "Log: $LOG_FILE"
