#!/usr/bin/env bash
# Create epc_api_clients once in the ASP.NET TenantRegistry database (e.g. asap).
# Tries: apply/elevated defaults-file, mysql socket/root/sudo, ECOMAE_MYSQL_ADMIN_*,
# then PHP CREATE paths. On denial: diagnose + one DDL print + align alternative.
# Never prints secrets. Never removes PHP.
#
# Usage:
#   ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"

printf '%s\n' '== Ensure epc_api_clients table =='

if [[ "${ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE:-}" != "YES" ]]; then
  printf 'Refusing without confirmation.\n' >&2
  printf 'Run:\n' >&2
  printf '  ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n' >&2
  exit 2
fi

if [[ ! -f "$ENV_FILE" ]]; then
  printf 'Missing env file: %s\n' "$ENV_FILE" >&2
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
      printf 'Using PHP docroot: %s\n' "$ECOMAE_PHP_DOCROOT"
      break
    fi
  done
fi

DB_NAME="$(python3 - "$ENV_FILE" <<'PY'
import sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
db = ""
for line in raw.splitlines():
    line = line.strip()
    if not line or line.startswith("#") or "=" not in line:
        continue
    k, v = line.split("=", 1)
    if k.strip() != "ConnectionStrings__TenantRegistry":
        continue
    v = v.strip().strip("'").strip('"')
    for part in v.split(";"):
        part = part.strip()
        if "=" not in part:
            continue
        pk, pv = part.split("=", 1)
        if pk.strip().lower() in ("database", "initial catalog"):
            db = pv.strip()
print(db)
PY
)"

if [[ -z "$DB_NAME" ]]; then
  printf 'Could not parse Database= from ConnectionStrings__TenantRegistry in %s\n' "$ENV_FILE" >&2
  exit 1
fi

printf 'Target database: %s\n' "$DB_NAME"

# Prefer dedicated elevated apply (debian-sys-maint / defaults-file) before PHP paths.
if [[ -x "$ROOT/scripts/cloudpanel_apply_epc_api_clients_ddl.sh" ]]; then
  set +e
  ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES \
    ECOMAE_ASPNET_ENV_FILE="$ENV_FILE" \
    bash "$ROOT/scripts/cloudpanel_apply_epc_api_clients_ddl.sh"
  apply_rc=$?
  set -e
  if [[ "$apply_rc" -eq 0 ]]; then
    exit 0
  fi
  printf 'Elevated apply unavailable — trying PHP CREATE paths\n'
fi

SQL_FILE="$ROOT/scripts/sql/epc_api_clients.sql"
if [[ ! -f "$SQL_FILE" ]]; then
  printf 'Missing %s\n' "$SQL_FILE" >&2
  exit 1
fi

run_mysql_admin() {
  local label="$1"
  shift
  if "$@" -N -e "SELECT 1" >/dev/null 2>&1; then
    printf 'Using %s\n' "$label"
    {
      printf 'USE `%s`;\n' "$DB_NAME"
      cat "$SQL_FILE"
    } | "$@"
    printf 'OK created/verified via %s\n' "$label"
    printf '\nNext:\n'
    printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
    printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
    return 0
  fi
  return 1
}

if command -v mysql >/dev/null 2>&1; then
  if [[ -n "${ECOMAE_MYSQL_ADMIN_USER:-}" ]]; then
    export MYSQL_PWD="${ECOMAE_MYSQL_ADMIN_PASSWORD:-${ECOMAE_MYSQL_ROOT_PASSWORD:-}}"
    if run_mysql_admin "mysql -u${ECOMAE_MYSQL_ADMIN_USER}" mysql -u"${ECOMAE_MYSQL_ADMIN_USER}" -h127.0.0.1; then
      unset MYSQL_PWD
      exit 0
    fi
    unset MYSQL_PWD
  elif [[ -n "${ECOMAE_MYSQL_ROOT_PASSWORD:-}" ]]; then
    export MYSQL_PWD="${ECOMAE_MYSQL_ROOT_PASSWORD}"
    if run_mysql_admin 'mysql -uroot (ECOMAE_MYSQL_ROOT_PASSWORD)' mysql -uroot -h127.0.0.1; then
      unset MYSQL_PWD
      exit 0
    fi
    unset MYSQL_PWD
  fi
fi

export ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES
export ECOMAE_ASPNET_ENV_FILE="$ENV_FILE"
set +e
php "$ROOT/scripts/php/ensure_epc_api_clients_table.php"
rc=$?
set -e

if [[ "$rc" -ne 0 ]]; then
  printf '\nBLOCKED: could not CREATE %s.epc_api_clients with available credentials.\n' "$DB_NAME" >&2
  printf 'Diagnose (redacted):\n' >&2
  bash "$ROOT/scripts/cloudpanel_diagnose_smoke_db.sh" >&2 || true
  printf '\nChoose one recovery:\n' >&2
  printf '  A) ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES bash scripts/cloudpanel_apply_epc_api_clients_ddl.sh\n' >&2
  printf '     (or paste: bash scripts/cloudpanel_print_epc_api_clients_ddl.sh)\n' >&2
  printf '  B) ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES \\\n' >&2
  printf '       bash scripts/cloudpanel_align_tenant_registry_to_php_db.sh\n' >&2
  printf '     systemctl restart ecomae-platform.service\n' >&2
  exit "$rc"
fi

printf '\nNext:\n'
printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
