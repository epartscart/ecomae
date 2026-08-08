#!/usr/bin/env bash
# Diagnose why ASP.NET /cp/login returns invalid_credentials for a contact on a host.
# Never prints password hashes in full — only length + kind. Never prints secrets.
#
#   ECOMAE_DIAG_EMAIL='taxofin2025@gmail.com' \
#   ECOMAE_DIAG_HOST='www.epartscart.com' \
#     bash scripts/cloudpanel_diagnose_cp_login_user.sh
set -euo pipefail

EMAIL="${ECOMAE_DIAG_EMAIL:-}"
HOST="${ECOMAE_DIAG_HOST:-www.epartscart.com}"
if [[ -z "$EMAIL" ]]; then
  printf 'USAGE: ECOMAE_DIAG_EMAIL=user@example.com ECOMAE_DIAG_HOST=www.epartscart.com bash %s\n' "$0" >&2
  exit 2
fi
if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
[[ -f "$ENV_FILE" ]] || { printf 'ERROR: missing %s\n' "$ENV_FILE" >&2; exit 1; }

has_secret=0
grep -qE '^EcomAE__SecretSuccession=.+' "$ENV_FILE" && has_secret=1 || true
printf '======== CP LOGIN DIAG ========\n'
printf 'HOST=%s EMAIL=%s\n' "$HOST" "$EMAIL"
printf 'secret_succession_configured=%s\n' "$has_secret"
systemctl is-active ecomae-platform.service 2>/dev/null || true

CS="$(grep -E '^ConnectionStrings__TenantRegistry=' "$ENV_FILE" | head -n1 | cut -d= -f2- || true)"
[[ -n "$CS" ]] || { printf 'ERROR: ConnectionStrings__TenantRegistry missing\n' >&2; exit 1; }

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
DB_HOST="${PARTS[0]}"; REG_DB="${PARTS[1]}"; DB_USER="${PARTS[2]}"; DB_PASS="${PARTS[3]}"
export MYSQL_PWD="$DB_PASS"

printf '\n-- portal tenant rows for host --\n'
mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT CONCAT('site_key=', IFNULL(site_key,''),
  ' hostname=', IFNULL(hostname,''),
  ' db_name=', IFNULL(db_name,''),
  ' erp_only=', IFNULL(erp_only_shared,0),
  ' active=', IFNULL(is_active,1),
  ' status=', IFNULL(status,''))
FROM epc_portal_tenants
WHERE hostname IN ('${HOST}', REPLACE('${HOST}', 'www.', ''), CONCAT('www.', REPLACE('${HOST}', 'www.', '')))
   OR site_key LIKE CONCAT('%', SUBSTRING_INDEX(REPLACE('${HOST}','www.',''), '.', 1), '%')
ORDER BY CASE WHEN IFNULL(TRIM(db_name),'')<>'' THEN 0 ELSE 1 END,
         CASE WHEN hostname='${HOST}' THEN 0 ELSE 1 END
LIMIT 20;
" || true

printf '\n-- resolver simulation (status dns_pending|live required) --\n'
mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT CONCAT('RESOLVER_CANDIDATE hostname=', IFNULL(hostname,''),
  ' db_name=', IFNULL(db_name,''),
  ' status=', IFNULL(status,''),
  ' active=', IFNULL(is_active,1))
FROM epc_portal_tenants
WHERE hostname IN ('${HOST}', REPLACE('${HOST}', 'www.', ''), CONCAT('www.', REPLACE('${HOST}', 'www.', '')))
  AND status IN ('dns_pending','live')
  AND COALESCE(is_active,1)=1
ORDER BY CASE WHEN IFNULL(TRIM(db_name),'')<>'' THEN 0 ELSE 1 END,
         CASE WHEN hostname='${HOST}' THEN 0 ELSE 1 END
LIMIT 5;
" || true

SHOP_DB="$(mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT db_name FROM epc_portal_tenants
WHERE hostname IN ('${HOST}', REPLACE('${HOST}', 'www.', ''), CONCAT('www.', REPLACE('${HOST}', 'www.', '')))
  AND IFNULL(TRIM(db_name),'') <> ''
  AND status IN ('dns_pending','live')
  AND COALESCE(is_active,1)=1
ORDER BY CASE WHEN hostname='${HOST}' THEN 0 ELSE 1 END, IFNULL(erp_only_shared,0) ASC
LIMIT 1;
" 2>/dev/null || true)"

if [[ -z "$SHOP_DB" ]]; then
  printf 'RESULT=FAIL tenant_db_unbound host=%s — rows may lack db_name OR status not in (dns_pending,live). Run cloudpanel_fix_epartscart_portal_tenant_db.sh\n' "$HOST"
  unset MYSQL_PWD
  exit 1
fi
printf 'resolved_shop_db=%s\n' "$SHOP_DB"

printf '\n-- users row in shop DB (no full hash) --\n'
mysql -h "$DB_HOST" -u "$DB_USER" "$SHOP_DB" -N -e "
SELECT CONCAT(
  'user_id=', IFNULL(user_id,0),
  ' email=', IFNULL(email,''),
  ' email_confirmed=', IFNULL(email_confirmed,0),
  ' unlocked=', IFNULL(unlocked,0),
  ' hash_len=', CHAR_LENGTH(IFNULL(password,'')),
  ' hash_kind=', CASE
      WHEN CHAR_LENGTH(IFNULL(password,''))=32 AND password REGEXP '^[0-9a-fA-F]{32}\$' THEN 'md5'
      WHEN password LIKE '\$2y\$%' OR password LIKE '\$2a\$%' OR password LIKE '\$2b\$%' THEN 'bcrypt'
      ELSE 'other'
    END
)
FROM users
WHERE LOWER(email)=LOWER('${EMAIL//\'/\\\'}')
LIMIT 3;
" || true

FOUND="$(mysql -h "$DB_HOST" -u "$DB_USER" "$SHOP_DB" -N -e "
SELECT COUNT(*) FROM users WHERE LOWER(email)=LOWER('${EMAIL//\'/\\\'}');
" 2>/dev/null || echo 0)"

# Cross-check taxofinca / other common tenants if email suggests them
if [[ "$EMAIL" == *taxofin* || "$EMAIL" == *taxofinca* ]]; then
  printf '\n-- cross-check taxofinca shop DB (if present) --\n'
  TAX_DB="$(mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT db_name FROM epc_portal_tenants
WHERE (hostname LIKE '%taxofinca%' OR site_key LIKE '%taxofinca%')
  AND IFNULL(TRIM(db_name),'') <> ''
ORDER BY IFNULL(erp_only_shared,0) ASC LIMIT 1;
" 2>/dev/null || true)"
  if [[ -n "$TAX_DB" ]]; then
    printf 'taxofinca_db=%s\n' "$TAX_DB"
    mysql -h "$DB_HOST" -u "$DB_USER" "$TAX_DB" -N -e "
SELECT CONCAT('FOUND_ON_TAXOFINCA user_id=', IFNULL(user_id,0),
  ' email_confirmed=', IFNULL(email_confirmed,0),
  ' unlocked=', IFNULL(unlocked,0))
FROM users WHERE LOWER(email)=LOWER('${EMAIL//\'/\\\'}') LIMIT 1;
" || true
  else
    printf 'taxofinca_db=(none)\n'
  fi
fi

# backend groups?
if [[ "${FOUND:-0}" != "0" ]]; then
  printf '\n-- backend group binds --\n'
  mysql -h "$DB_HOST" -u "$DB_USER" "$SHOP_DB" -N -e "
SELECT CONCAT('group_id=', g.group_id, ' groups=', COUNT(*))
FROM users u
JOIN users_groups_bind g ON g.user_id=u.user_id
WHERE LOWER(u.email)=LOWER('${EMAIL//\'/\\\'}')
GROUP BY g.group_id;
" || true
fi

unset MYSQL_PWD

printf '\n-- public login POST shape (wrong password; expects 302) --\n'
curl -sS -o /dev/null -w 'POST /cp/login code=%{http_code} redirect=%{redirect_url}\n' --max-time 20 \
  -X POST "https://${HOST}/cp/login" \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'surface=cp' \
  --data-urlencode 'redirect=/cp' \
  --data-urlencode 'contact_type=email' \
  --data-urlencode "contact=${EMAIL}" \
  --data-urlencode 'password=__diagnose_wrong__' || true

if [[ "${FOUND:-0}" == "0" ]]; then
  printf 'RESULT=FAIL user_not_in_shop_db=%s host=%s — account may belong to another tenant host\n' "$SHOP_DB" "$HOST"
  exit 1
fi
if [[ "$has_secret" != "1" ]]; then
  printf 'RESULT=FAIL secret_succession_missing — run cloudpanel_sync_secret_succession_from_php.sh\n'
  exit 1
fi
printf 'RESULT=OK user_present_in_%s — if login still fails: wrong password, no backend group, or SecretSuccession mismatch (md5)\n' "$SHOP_DB"
exit 0
