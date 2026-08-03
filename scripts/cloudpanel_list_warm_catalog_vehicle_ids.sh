#!/usr/bin/env bash
# List warm vehicle-cache IDs from TenantRegistry DB.
#   models         → mfa_id counts from epc_umapi_models
#   modifications  → ms_id counts from epc_umapi_modifications
#   vin            → warm rows from epc_umapi_vin_cache (vehicle_count>0)
# Never prints DB passwords. Never removes PHP.
#
# Usage:
#   set -a; source /etc/ecomae-aspnet/platform.env; set +a
#   bash scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh models
#   bash scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh modifications
#   bash scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh vin
set -euo pipefail

KIND="${1:-models}"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
SECTION="${ECOMAE_CATALOG_SECTION:-passenger}"
LIMIT="${ECOMAE_WARM_ID_LIMIT:-${ECOMAE_WARM_MFA_LIMIT:-10}}"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

SQL=""
case "$KIND" in
  models|mfa|mfa_id)
    COL="mfa_id"
    TABLE="epc_umapi_models"
    SQL="$(printf "SELECT \`%s\`, COUNT(*) AS c FROM \`%s\` WHERE section='%s' GROUP BY \`%s\` ORDER BY c DESC LIMIT %s;" \
      "$COL" "$TABLE" "$SECTION" "$COL" "$LIMIT")"
    ;;
  modifications|mods|ms|ms_id)
    COL="ms_id"
    TABLE="epc_umapi_modifications"
    SQL="$(printf "SELECT \`%s\`, COUNT(*) AS c FROM \`%s\` WHERE section='%s' GROUP BY \`%s\` ORDER BY c DESC LIMIT %s;" \
      "$COL" "$TABLE" "$SECTION" "$COL" "$LIMIT")"
    ;;
  vin|vins|vin_cache)
    TABLE="epc_umapi_vin_cache"
    COL="vin"
    SQL="$(printf "SELECT \`vin\`, \`language\`, \`region\`, \`vehicle_count\` FROM \`%s\` WHERE \`vehicle_count\` > 0 ORDER BY \`updated_at\` DESC LIMIT %s;" \
      "$TABLE" "$LIMIT")"
    ;;
  *)
    printf 'Usage: %s models|modifications|vin\n' "$(basename "$0")" >&2
    exit 2
    ;;
esac

parsed="$(python3 - "$ENV_FILE" <<'PY'
import sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
host, port, db, user, password = "127.0.0.1", "3306", "", "", ""
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
        pk, pv = pk.strip().lower(), pv.strip()
        if pk in ("server", "data source", "host") and pv:
            host = pv.split(",")[0]
        elif pk == "port" and pv:
            port = pv
        elif pk in ("database", "initial catalog") and pv:
            db = pv
        elif pk in ("user", "uid", "user id") and pv:
            user = pv
        elif pk in ("password", "pwd") and pv:
            password = pv
print(f"{host}\t{port}\t{db}\t{user}\t{password}")
PY
)"
HOST="$(printf '%s' "$parsed" | cut -f1)"
PORT="$(printf '%s' "$parsed" | cut -f2)"
DB_NAME="$(printf '%s' "$parsed" | cut -f3)"
DB_USER="$(printf '%s' "$parsed" | cut -f4)"
DB_PASS="$(printf '%s' "$parsed" | cut -f5-)"

if [[ -z "$DB_NAME" || -z "$DB_USER" ]]; then
  printf 'ERROR: could not parse ConnectionStrings__TenantRegistry from %s\n' "$ENV_FILE" >&2
  exit 1
fi

printf 'DB=%s table=%s kind=%s (host/user redacted)\n' "$DB_NAME" "$TABLE" "$KIND"

if command -v mysql >/dev/null 2>&1; then
  MYSQL_PWD="$DB_PASS" mysql -h "$HOST" -P "$PORT" -u "$DB_USER" "$DB_NAME" -B -N -e "$SQL"
  exit 0
fi

php -r '
$h=$argv[1]; $p=$argv[2]; $d=$argv[3]; $u=$argv[4]; $w=$argv[5]; $sql=$argv[6];
$pdo=new PDO("mysql:host=$h;port=$p;dbname=$d;charset=utf8mb4",$u,$w,[PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION]);
foreach($pdo->query($sql, PDO::FETCH_NUM) as $row){ echo implode("\t", $row), "\n"; }
' "$HOST" "$PORT" "$DB_NAME" "$DB_USER" "$DB_PASS" "$SQL"
