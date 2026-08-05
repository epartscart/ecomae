#!/usr/bin/env bash
set -euo pipefail

RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-0}"
RUN_NGINX_RELOAD="${ECOMAE_RUN_NGINX_RELOAD:-0}"
KEEP_PHP_FALLBACK=0
ROUTE_HINT=""
TARGET_RELEASE=""
SHOW_HELP=0

usage() {
    cat <<'EOF'
Usage: bash scripts/rollback_aspnet_foundation.sh [options] [target-release-dir]

Options:
  --keep-php-fallback   Explicitly retain PHP as the traffic/fallback runtime
                        (default behavior; recommended after ASP.NET primary promote)
  --route <path>        Optional exact-route hint to remove (operator note only)
  -h, --help            Show this help

Environment:
  ECOMAE_ASPNET_RELEASE_ROOT   Default: /var/www/ecomae-aspnet
  ECOMAE_RUN_SYSTEMD=1         Restart ecomae-platform.service
  ECOMAE_RUN_NGINX_RELOAD=1    nginx -t && reload

Notes:
  - This script does NOT delete the PHP project. Reference mode keeps PHP installed
    so previous results remain visible for gap-finding.
  - Emergency traffic rollback: remove ASP.NET exact-route proxy location blocks
    so CloudPanel/PHP handles those routes again.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --keep-php-fallback)
            KEEP_PHP_FALLBACK=1
            shift
            ;;
        --route)
            ROUTE_HINT="${2:-}"
            if [[ -z "$ROUTE_HINT" ]]; then
                printf -- '--route requires a path argument\n' >&2
                exit 2
            fi
            shift 2
            ;;
        -h|--help)
            SHOW_HELP=1
            shift
            ;;
        --)
            shift
            break
            ;;
        -*)
            printf 'Unknown option: %s\n' "$1" >&2
            usage >&2
            exit 2
            ;;
        *)
            if [[ -n "$TARGET_RELEASE" ]]; then
                printf 'Unexpected argument: %s\n' "$1" >&2
                exit 2
            fi
            TARGET_RELEASE="$1"
            shift
            ;;
    esac
done

if [[ "$SHOW_HELP" == "1" ]]; then
    usage
    exit 0
fi

# Keeping PHP fallback is the safe default even when the flag is omitted.
KEEP_PHP_FALLBACK=1

printf '== EcomAE ASP.NET Core foundation rollback ==\n'
printf 'Release root: %s\n' "$RELEASE_ROOT"
printf 'Keep PHP fallback/reference: yes (--keep-php-fallback)\n'
if [[ -n "$ROUTE_HINT" ]]; then
    printf 'Route hint (operator): %s\n' "$ROUTE_HINT"
fi

if [[ -n "$TARGET_RELEASE" ]]; then
    if [[ ! -d "$TARGET_RELEASE" ]]; then
        printf 'Target release does not exist: %s\n' "$TARGET_RELEASE" >&2
        exit 1
    fi
    ln -sfn "$TARGET_RELEASE" "$RELEASE_ROOT/current"
    printf 'Current symlink moved to: %s\n' "$TARGET_RELEASE"
else
    printf 'No target release supplied. Leaving symlink unchanged.\n'
fi

if [[ "$RUN_SYSTEMD" == "1" ]]; then
    systemctl restart ecomae-platform.service || true
    systemctl status ecomae-platform.service --no-pager || true
else
    printf 'Skipped systemd restart. Set ECOMAE_RUN_SYSTEMD=1 to restart service.\n'
fi

if [[ "$RUN_NGINX_RELOAD" == "1" ]]; then
    nginx -t
    systemctl reload nginx
else
    printf 'Skipped nginx reload. Remove ASP.NET Core location blocks and reload CloudPanel/Nginx manually if needed.\n'
fi

printf '\nEmergency traffic rollback: remove ASP.NET Core proxy location blocks so CloudPanel/PHP handles routes again.\n'
printf 'PHP project/docroot is retained as reference (not deleted) for previous-result compares.\n'
if [[ -n "$ROUTE_HINT" ]]; then
    printf 'Focus removal on exact-route shadow for: %s\n' "$ROUTE_HINT"
fi
