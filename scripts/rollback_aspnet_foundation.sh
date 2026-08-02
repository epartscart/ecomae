#!/usr/bin/env bash
set -euo pipefail

RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-0}"
RUN_NGINX_RELOAD="${ECOMAE_RUN_NGINX_RELOAD:-0}"
TARGET_RELEASE="${1:-}"

printf '== EcomAE ASP.NET Core foundation rollback ==\n'
printf 'Release root: %s\n' "$RELEASE_ROOT"

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
