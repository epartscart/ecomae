#!/bin/bash
# ═══════════════════════════════════════════════════════════════
# Cron wrapper — runs daily backup at 3 AM
# ═══════════════════════════════════════════════════════════════
#
# Install with:
#   sudo crontab -e
#   0 3 * * * /home/ecomae/htdocs/www.ecomae.com/tools/backup/epc_backup_cron.sh >> /home/ecomae/backups/cron.log 2>&1
#
# Or one-liner install:
#   (sudo crontab -l 2>/dev/null; echo "0 3 * * * /home/ecomae/htdocs/www.ecomae.com/tools/backup/epc_backup_cron.sh >> /home/ecomae/backups/cron.log 2>&1") | sudo crontab -
# ═══════════════════════════════════════════════════════════════

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec bash "${SCRIPT_DIR}/epc_backup.sh"
