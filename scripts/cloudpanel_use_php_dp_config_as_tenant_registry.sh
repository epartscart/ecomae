#!/usr/bin/env bash
# Point ASP.NET TenantRegistry at PHP DP_Config DB credentials (same DB PHP uses).
# Unblocks smoke when asap lacks epc_api_clients CREATE rights and ecomae_aspnet
# cannot CONNECT to ecomae, but PHP DP_Config already has the table + sessions.
#
# Requires ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY=YES.
# Optionally restarts platform with ECOMAE_CONFIRM_RESTART_PLATFORM=YES.
# Never prints passwords. Never removes PHP.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"

printf '%s\n' '== Use PHP DP_Config as TenantRegistry =='

if [[ "${ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY:-}" != "YES" ]]; then
  printf 'Refusing without confirmation.\n' >&2
  printf 'Diagnose first:\n' >&2
  printf '  bash scripts/cloudpanel_diagnose_smoke_db.sh\n' >&2
  printf 'Then:\n' >&2
  printf '  ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY=YES \\\n' >&2
  printf '    bash scripts/cloudpanel_use_php_dp_config_as_tenant_registry.sh\n' >&2
  exit 2
fi

if [[ -z "${ECOMAE_PHP_DOCROOT:-}" ]]; then
  for candidate in \
    /home/ecomae/htdocs/www.ecomae.com \
    /home/ecomae/htdocs \
    /home/cloudpanel/htdocs/www.ecomae.com \
    /var/www/www.ecomae.com
  do
    if [[ -f "$candidate/config.php" ]]; then
      export ECOMAE_PHP_DOCROOT="$candidate"
      break
    fi
  done
fi

export ECOMAE_ASPNET_ENV_FILE="$ENV_FILE"
export ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY=YES
php "$ROOT/scripts/php/use_php_dp_config_as_tenant_registry.php"

if [[ "${ECOMAE_CONFIRM_RESTART_PLATFORM:-}" == "YES" ]]; then
  systemctl restart ecomae-platform.service
  printf 'Restarted ecomae-platform.service\n'
  bash "$ROOT/scripts/wait_for_aspnet_health.sh" || true
else
  printf 'Restart required:\n'
  printf '  systemctl restart ecomae-platform.service\n'
  printf '  # or: ECOMAE_CONFIRM_RESTART_PLATFORM=YES bash scripts/cloudpanel_use_php_dp_config_as_tenant_registry.sh\n'
fi

printf '\nNext:\n'
printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
printf '  source %s\n' "$ENV_FILE"
printf '  curl -sS -H "Cookie: \$ECOMAE_ADMIN_COOKIE_HEADER" http://127.0.0.1:5100/auth/session/probe; echo\n'
printf '  bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'
