#!/usr/bin/env bash
# Sync PHP $DP_Config->secret_succession into /etc/ecomae-aspnet/platform.env
# so ASP.NET CP/ERP/BOS/storefront login accepts the same credentials as PHP.
# Never prints the secret.
#
#   ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES \
#   ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
#     bash scripts/cloudpanel_sync_secret_succession_from_php.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_PLATFORM_ENV:-/etc/ecomae-aspnet/platform.env}"
PHP_SCRIPT="$ROOT/scripts/php/sync_secret_succession_to_platform_env.php"

if [[ "${ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES\n' >&2
  exit 2
fi
[[ -f "$PHP_SCRIPT" ]] || { printf 'ERROR: missing %s\n' "$PHP_SCRIPT" >&2; exit 1; }
command -v php >/dev/null 2>&1 || { printf 'ERROR: php CLI required\n' >&2; exit 1; }

printf '== Sync SecretSuccession from PHP DP_Config ==\n'
printf 'Env file: %s\n' "$ENV_FILE"
if [[ -n "${ECOMAE_PHP_DOCROOT:-}" ]]; then
  printf 'PHP docroot override: %s\n' "$ECOMAE_PHP_DOCROOT"
fi

ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES \
ECOMAE_PLATFORM_ENV="$ENV_FILE" \
  php "$PHP_SCRIPT"

bash "$ROOT/scripts/cloudpanel_verify_secret_succession_configured.sh"

if [[ "${ECOMAE_CONFIRM_RESTART_PLATFORM:-}" == "YES" ]]; then
  systemctl restart ecomae-platform.service
  bash "$ROOT/scripts/wait_for_aspnet_health.sh"
  printf 'Platform restarted. Try the same PHP admin email/password on /bos/login /cp/login /erp/login.\n'
else
  printf 'NOTE: set ECOMAE_CONFIRM_RESTART_PLATFORM=YES to restart ecomae-platform.service\n'
  printf '  systemctl restart ecomae-platform.service\n'
fi
