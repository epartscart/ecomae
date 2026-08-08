#!/usr/bin/env bash
# Ensure epc_portal_tenants binds a shop db_name for epartscart.com / www.epartscart.com.
# Safe: only copies db_name/user/pass from a sibling live shop row; never deletes rows.
#
#   ECOMAE_CONFIRM_FIX_EPARTSCART_PORTAL_TENANT_DB=YES \
#     bash scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh
set -euo pipefail

if [[ "${ECOMAE_CONFIRM_FIX_EPARTSCART_PORTAL_TENANT_DB:-}" != "YES" ]]; then
  printf 'REFUSE: set ECOMAE_CONFIRM_FIX_EPARTSCART_PORTAL_TENANT_DB=YES\n' >&2
  exit 2
fi
if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root\n' >&2
  exit 1
fi

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
if [[ ! -f "$ENV_FILE" ]]; then
  printf 'ERROR: missing %s\n' "$ENV_FILE" >&2
  exit 1
fi

CS="$(grep -E '^ConnectionStrings__TenantRegistry=' "$ENV_FILE" | head -n1 | cut -d= -f2- || true)"
if [[ -z "$CS" ]]; then
  printf 'ERROR: ConnectionStrings__TenantRegistry not set in %s\n' "$ENV_FILE" >&2
  exit 1
fi

mapfile -t PARTS < <(python3 - <<'PY' "$CS"
import re, sys
cs = sys.argv[1]
def pick(*keys):
    for key in keys:
        m = re.search(rf'(?:^|;)\s*{re.escape(key)}=([^;]+)', cs, re.I)
        if m:
            return m.group(1).strip()
    return ""
print(pick("Server", "Data Source") or "127.0.0.1")
print(pick("Database", "Initial Catalog"))
print(pick("User", "Uid", "User ID"))
print(pick("Password", "Pwd"))
PY
)
DB_HOST="${PARTS[0]}"
DB_NAME="${PARTS[1]}"
DB_USER="${PARTS[2]}"
DB_PASS="${PARTS[3]}"

if [[ -z "$DB_NAME" || -z "$DB_USER" ]]; then
  printf 'ERROR: could not parse TenantRegistry connection string\n' >&2
  exit 1
fi

printf '======== FIX EPARTSCART PORTAL TENANT DB ========\n'
printf 'registry_db=%s host=%s\n' "$DB_NAME" "$DB_HOST"

export MYSQL_PWD="$DB_PASS"
mysql -h "$DB_HOST" -u "$DB_USER" "$DB_NAME" -e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(erp_only_shared,0) AS erp_only,
       IFNULL(is_active,1) AS is_active, status
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com')
   OR site_key LIKE 'epartscart%'
ORDER BY hostname;
"

# Prefer updating db_name only — credential columns vary by schema.
mysql -h "$DB_HOST" -u "$DB_USER" "$DB_NAME" -e "
UPDATE epc_portal_tenants t
JOIN (
  SELECT db_name
  FROM epc_portal_tenants
  WHERE (hostname IN ('epartscart.com','www.epartscart.com') OR site_key LIKE 'epartscart%')
    AND IFNULL(TRIM(db_name),'') <> ''
    AND status IN ('dns_pending','live')
    AND COALESCE(is_active,1)=1
  ORDER BY CASE WHEN hostname='epartscart.com' THEN 0 WHEN hostname='www.epartscart.com' THEN 1 ELSE 2 END,
           IFNULL(erp_only_shared,0) ASC, IFNULL(is_demo,0) ASC
  LIMIT 1
) donor
SET t.db_name = donor.db_name
WHERE (t.hostname IN ('epartscart.com','www.epartscart.com') OR t.site_key LIKE 'epartscart%')
  AND IFNULL(TRIM(t.db_name),'') = '';
"

printf '\n-- after fix --\n'
mysql -h "$DB_HOST" -u "$DB_USER" "$DB_NAME" -e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(erp_only_shared,0) AS erp_only,
       IFNULL(is_active,1) AS is_active, status
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com')
   OR site_key LIKE 'epartscart%'
ORDER BY hostname;
"

unset MYSQL_PWD
printf 'RESULT=OK portal tenant db_name sync attempted\n'
printf 'Restart ecomae-platform to clear 60s tenant registry cache.\n'
