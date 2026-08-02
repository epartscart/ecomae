#!/usr/bin/env bash
# Create epc_api_clients once in the ASP.NET TenantRegistry database (e.g. asap).
# Prefers mysql root/socket; falls back to PHP DP_Config credentials.
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

SQL="$(cat <<'SQL'
CREATE TABLE IF NOT EXISTS `epc_api_clients` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `client_key_hash` CHAR(64) NOT NULL,
  `client_key_prefix` VARCHAR(32) NOT NULL DEFAULT '',
  `product` ENUM('catalog','price_pro','both') NOT NULL DEFAULT 'catalog',
  `label` VARCHAR(120) NOT NULL DEFAULT '',
  `contact_email` VARCHAR(190) NOT NULL DEFAULT '',
  `active` TINYINT(1) NOT NULL DEFAULT 1,
  `daily_limit` INT NOT NULL DEFAULT 1000,
  `calls_today` INT NOT NULL DEFAULT 0,
  `calls_reset_date` DATE NULL,
  `allowed_actions_json` TEXT NOT NULL,
  `time_created` INT NOT NULL DEFAULT 0,
  `time_updated` INT NOT NULL DEFAULT 0,
  UNIQUE KEY `client_key_hash` (`client_key_hash`),
  KEY `product_active` (`product`, `active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
SQL
)"

if command -v mysql >/dev/null 2>&1; then
  if mysql --protocol=socket -N -e "SELECT 1" >/dev/null 2>&1; then
    printf 'Using mysql socket (local auth)\n'
    mysql --protocol=socket -e "USE \`${DB_NAME}\`; ${SQL}"
    printf 'OK created/verified via mysql socket\n'
    printf '\nNext:\n'
    printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
    exit 0
  fi
  if mysql -uroot -N -e "SELECT 1" >/dev/null 2>&1; then
    printf 'Using mysql -uroot\n'
    mysql -uroot -e "USE \`${DB_NAME}\`; ${SQL}"
    printf 'OK created/verified via mysql -uroot\n'
    printf '\nNext:\n'
    printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
    exit 0
  fi
  printf 'mysql present but root/socket auth unavailable — falling back to PHP DP_Config\n'
fi

export ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES
export ECOMAE_ASPNET_ENV_FILE="$ENV_FILE"
php "$ROOT/scripts/php/ensure_epc_api_clients_table.php"

printf '\nNext:\n'
printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
