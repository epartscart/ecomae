#!/usr/bin/env bash
# Diagnose TenantRegistry vs PHP app DB for final-gate smoke (no secrets printed).
# Reports whether epc_api_clients / admin sessions exist and which recovery path fits.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"

printf '%s\n' '== Smoke DB diagnose (redacted) =='
printf 'Env file: %s\n' "$ENV_FILE"

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
printf 'PHP docroot: %s\n' "${ECOMAE_PHP_DOCROOT:-MISSING}"

export ECOMAE_ASPNET_ENV_FILE="$ENV_FILE"
php "$ROOT/scripts/php/diagnose_smoke_db.php"
rc=$?

printf '\nRecovery chooser:\n'
printf '  A) Apply DDL on TenantRegistry DB (needs elevated MySQL):\n'
printf '       ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES bash scripts/cloudpanel_apply_epc_api_clients_ddl.sh\n'
printf '  B) Align ConnectionStrings__TenantRegistry Database= to PHP app DB (when A blocked and PHP DB already has table/sessions):\n'
printf '       ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES bash scripts/cloudpanel_align_tenant_registry_to_php_db.sh\n'
printf '       systemctl restart ecomae-platform.service\n'
printf '  Then issue + capture:\n'
printf '       ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
printf '         bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
printf '       source %s && bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n' "$ENV_FILE"
exit "$rc"
