#!/bin/bash
# ═══════════════════════════════════════════════════════════════
# ECOM AE / eParts Cart — Production Restore Script
# ═══════════════════════════════════════════════════════════════
#
# Usage:
#   sudo bash epc_restore.sh <backup_timestamp>
#   sudo bash epc_restore.sh 20260621_120000
#   sudo bash epc_restore.sh --critical-only 20260621_120000
#
# Modes:
#   (default)        Full restore — docroot + databases + configs
#   --critical-only  Restore only the storefront-critical files
#   --docroot-only   Restore only the docroot (no DB)
#   --db-only        Restore only databases
# ═══════════════════════════════════════════════════════════════

set -euo pipefail

DOCROOT="/home/ecomae/htdocs/www.ecomae.com"
BACKUP_DIR="/home/ecomae/backups"

# ─── Parse args ───────────────────────────────────────────────
MODE="full"
BACKUP_TS=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --critical-only) MODE="critical"; shift ;;
        --docroot-only)  MODE="docroot"; shift ;;
        --db-only)       MODE="db"; shift ;;
        *)               BACKUP_TS="$1"; shift ;;
    esac
done

[[ $EUID -eq 0 ]] || { echo "Must run as root (sudo)"; exit 1; }
[[ -n "$BACKUP_TS" ]] || { echo "Usage: $0 [--critical-only|--docroot-only|--db-only] <backup_timestamp>"; exit 1; }

BACKUP_PATH="${BACKUP_DIR}/${BACKUP_TS}"
[[ -d "$BACKUP_PATH" ]] || { echo "Backup not found: $BACKUP_PATH"; echo "Available:"; ls -1 "$BACKUP_DIR"; exit 1; }

echo "=== Restore from: $BACKUP_PATH (mode: $MODE) ==="

# ─── Critical files only ─────────────────────────────────────
if [[ "$MODE" == "critical" ]]; then
    echo "Restoring critical storefront files..."
    cd "$BACKUP_PATH/critical"
    find . -type f | while read -r f; do
        dest="${DOCROOT}/${f#./}"
        mkdir -p "$(dirname "$dest")"
        cp -p "$f" "$dest"
        echo "  Restored: ${f#./}"
    done
    echo "Done. Run widget warm after restore:"
    echo "  curl -sk 'https://www.epartscart.com/epc-home-widgets-warm.php?token=epartscart-deploy-2026&lang=/en'"
    exit 0
fi

# ─── Docroot restore ─────────────────────────────────────────
if [[ "$MODE" == "full" || "$MODE" == "docroot" ]]; then
    echo "--- Restoring docroot ---"
    # Safety: create a pre-restore snapshot
    PRE_RESTORE="${BACKUP_DIR}/pre_restore_$(date +%Y%m%d_%H%M%S)"
    mkdir -p "$PRE_RESTORE"
    echo "  Pre-restore snapshot: $PRE_RESTORE"
    tar czf "${PRE_RESTORE}/pre_restore_docroot.tar.gz" \
        --exclude='.git' --exclude='node_modules' --exclude='backups' \
        -C "$(dirname "$DOCROOT")" "$(basename "$DOCROOT")" 2>/dev/null || true

    # Extract backup over docroot
    tar xzf "${BACKUP_PATH}/docroot/www.ecomae.com.tar.gz" \
        -C "$(dirname "$DOCROOT")" 2>/dev/null
    echo "  Docroot restored"

    # Fix permissions
    chown -R ecomae:ecomae "$DOCROOT" 2>/dev/null || true
    echo "  Permissions fixed"
fi

# ─── Database restore ────────────────────────────────────────
if [[ "$MODE" == "full" || "$MODE" == "db" ]]; then
    echo "--- Restoring databases ---"
    for dump in "${BACKUP_PATH}"/mysql/*.sql.gz; do
        [[ -f "$dump" ]] || continue
        db=$(basename "$dump" .sql.gz)
        echo "  Restoring: $db"
        mysql -e "CREATE DATABASE IF NOT EXISTS \`$db\`" 2>/dev/null || true
        zcat "$dump" | mysql "$db" 2>/dev/null
        echo "  OK: $db"
    done
fi

echo ""
echo "=== Restore complete ==="
echo ""
echo "Post-restore checklist:"
echo "  1. Verify: curl -sk 'https://www.epartscart.com/epc-pf-own-home-verify.php?token=epartscart-deploy-2026&fast=1'"
echo "  2. Warm caches: curl -sk 'https://www.epartscart.com/epc-home-widgets-warm.php?token=epartscart-deploy-2026&lang=/en'"
echo "  3. Check imports: curl -sk 'https://www.epartscart.com/epc-imports-pause.php?token=epartscart-deploy-2026'"
echo "  4. Test homepage: curl -sk --max-time 180 -o /dev/null -w '%{http_code}' 'https://www.epartscart.com/en/'"
echo "  5. Test BOS: curl -sk -o /dev/null -w '%{http_code}' 'https://www.ecomae.com/bos/'"
