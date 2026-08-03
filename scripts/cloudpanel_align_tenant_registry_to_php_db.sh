#!/usr/bin/env bash
# Point ConnectionStrings__TenantRegistry Database= at the PHP DP_Config database
# when TenantRegistry (e.g. asap) cannot CREATE epc_api_clients but the PHP app DB
# already has the table/sessions that ASP.NET must read.
#
# Requires ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES.
# Optionally restarts platform with ECOMAE_CONFIRM_RESTART_PLATFORM=YES.
# Never prints passwords. Never removes PHP.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"

printf '%s\n' '== Align TenantRegistry Database= to PHP app DB =='

if [[ "${ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB:-}" != "YES" ]]; then
  printf 'Refusing without confirmation.\n' >&2
  printf 'First diagnose:\n' >&2
  printf '  bash scripts/cloudpanel_diagnose_smoke_db.sh\n' >&2
  printf 'Then (only if PHP db has epc_api_clients and platform user can connect):\n' >&2
  printf '  ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES \\\n' >&2
  printf '    bash scripts/cloudpanel_align_tenant_registry_to_php_db.sh\n' >&2
  exit 2
fi

if [[ ! -f "$ENV_FILE" || ! -w "$ENV_FILE" ]]; then
  printf 'Env file missing/unwritable: %s\n' "$ENV_FILE" >&2
  exit 1
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
export ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES
set +e
php "$ROOT/scripts/php/align_tenant_registry_to_php_db.php"
rc=$?
set -e
if [[ "$rc" -ne 0 ]]; then
  exit "$rc"
fi

if [[ "${ECOMAE_CONFIRM_RESTART_PLATFORM:-}" == "YES" ]]; then
  if systemctl restart ecomae-platform.service; then
    printf 'Restarted ecomae-platform.service\n'
    bash "$ROOT/scripts/wait_for_aspnet_health.sh" || true
  else
    printf 'WARN: systemctl restart failed — restart manually\n' >&2
  fi
else
  printf 'Restart platform to pick up Database= change:\n'
  printf '  ECOMAE_CONFIRM_RESTART_PLATFORM=YES systemctl restart ecomae-platform.service\n'
  printf '  # or: systemctl restart ecomae-platform.service && bash scripts/wait_for_aspnet_health.sh\n'
fi

printf '\nNext:\n'
printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
printf '  source %s\n' "$ENV_FILE"
printf '  bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'
