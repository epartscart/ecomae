#!/usr/bin/env bash
# Remap Super CP / Super BOS / Super ERP login from ecomaedxb@gmail.com → yawer@ecomae.com
# in the platform users table. Password hash is NOT changed.
# Never prints hashes or secrets.
#
# Diagnose (no write):
#   ECOMAE_CONFIRM_REMAP_SUPER_OPERATOR=0 bash scripts/cloudpanel_remap_super_operator_yawer.sh
#
# Apply:
#   ECOMAE_CONFIRM_REMAP_SUPER_OPERATOR=YES bash scripts/cloudpanel_remap_super_operator_yawer.sh
#
# Then (so ASP.NET accepts the same PHP password):
#   ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
#     bash scripts/cloudpanel_sync_secret_succession_from_php.sh
set -euo pipefail

CANONICAL="${ECOMAE_SUPER_OPERATOR_EMAIL:-yawer@ecomae.com}"
LEGACY="${ECOMAE_SUPER_OPERATOR_LEGACY:-ecomaedxb@gmail.com}"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
APPLY="${ECOMAE_CONFIRM_REMAP_SUPER_OPERATOR:-0}"

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi
[[ -f "$ENV_FILE" ]] || { printf 'ERROR: missing %s\n' "$ENV_FILE" >&2; exit 1; }

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

describe() {
  local email="$1"
  mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT CONCAT(
  'email=', IFNULL(email,''),
  ' user_id=', IFNULL(user_id,0),
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
WHERE LOWER(email)=LOWER('${email//\'/\\\'}')
LIMIT 1;
" 2>/dev/null || true
}

printf '== Super operator remap ==\n'
printf 'platform_db=%s\n' "$REG_DB"
printf 'canonical=%s\n' "$CANONICAL"
printf 'legacy=%s\n' "$LEGACY"
printf '\n-- current rows (no hash) --\n'
printf 'canonical: %s\n' "$(describe "$CANONICAL" || echo '(missing)')"
printf 'legacy:    %s\n' "$(describe "$LEGACY" || echo '(missing)')"

HAS_SECRET=0
grep -qE '^EcomAE__SecretSuccession=.+' "$ENV_FILE" && HAS_SECRET=1 || true
printf 'secret_succession_configured=%s\n' "$HAS_SECRET"

if [[ "$APPLY" != "YES" ]]; then
  printf '\nDiagnose only. To apply (password hash stays):\n'
  printf '  ECOMAE_CONFIRM_REMAP_SUPER_OPERATOR=YES bash scripts/cloudpanel_remap_super_operator_yawer.sh\n'
  unset MYSQL_PWD
  exit 0
fi

CANON_ID="$(mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "SELECT user_id FROM users WHERE LOWER(email)=LOWER('${CANONICAL//\'/\\\'}') LIMIT 1;" || true)"
LEGACY_ID="$(mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "SELECT user_id FROM users WHERE LOWER(email)=LOWER('${LEGACY//\'/\\\'}') LIMIT 1;" || true)"

if [[ -n "$CANON_ID" && -n "$LEGACY_ID" && "$CANON_ID" != "$LEGACY_ID" ]]; then
  printf 'ERROR: collision — both emails exist as different user_id (%s vs %s)\n' "$CANON_ID" "$LEGACY_ID" >&2
  unset MYSQL_PWD
  exit 1
fi

TARGET_ID="${CANON_ID:-$LEGACY_ID}"
if [[ -z "$TARGET_ID" ]]; then
  printf 'ERROR: neither %s nor %s exists in %s.users\n' "$CANONICAL" "$LEGACY" "$REG_DB" >&2
  unset MYSQL_PWD
  exit 1
fi

BEFORE_KIND="$(mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT CASE
  WHEN CHAR_LENGTH(IFNULL(password,''))=32 AND password REGEXP '^[0-9a-fA-F]{32}\$' THEN 'md5'
  WHEN password LIKE '\$2y\$%' OR password LIKE '\$2a\$%' OR password LIKE '\$2b\$%' THEN 'bcrypt'
  ELSE 'other'
END FROM users WHERE user_id=${TARGET_ID} LIMIT 1;
")"
BEFORE_LEN="$(mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "SELECT CHAR_LENGTH(IFNULL(password,'')) FROM users WHERE user_id=${TARGET_ID} LIMIT 1;")"

mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -e "
UPDATE users
SET email='${CANONICAL//\'/\\\'}', email_confirmed=1, unlocked=1
WHERE user_id=${TARGET_ID};
INSERT IGNORE INTO users_groups_bind (user_id, group_id)
SELECT ${TARGET_ID}, id FROM groups WHERE for_backend=1;
"

AFTER_KIND="$(mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT CASE
  WHEN CHAR_LENGTH(IFNULL(password,''))=32 AND password REGEXP '^[0-9a-fA-F]{32}\$' THEN 'md5'
  WHEN password LIKE '\$2y\$%' OR password LIKE '\$2a\$%' OR password LIKE '\$2b\$%' THEN 'bcrypt'
  ELSE 'other'
END FROM users WHERE user_id=${TARGET_ID} LIMIT 1;
")"
AFTER_LEN="$(mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "SELECT CHAR_LENGTH(IFNULL(password,'')) FROM users WHERE user_id=${TARGET_ID} LIMIT 1;")"

unset MYSQL_PWD

printf '\n-- after --\n'
printf 'user_id=%s hash_kind_before=%s hash_len_before=%s hash_kind_after=%s hash_len_after=%s\n' \
  "$TARGET_ID" "$BEFORE_KIND" "$BEFORE_LEN" "$AFTER_KIND" "$AFTER_LEN"

if [[ "$BEFORE_KIND" != "$AFTER_KIND" || "$BEFORE_LEN" != "$AFTER_LEN" ]]; then
  printf 'ERROR: password hash changed — inspect before using this account\n' >&2
  exit 1
fi

printf 'RESULT=OK Super admin is %s user_id=%s password_hash_unchanged=1\n' "$CANONICAL" "$TARGET_ID"
printf 'Login on https://www.ecomae.com/erp/login and /cp/login with that email and the same previous password.\n'
if [[ "$HAS_SECRET" != "1" ]]; then
  printf 'NEXT: ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES ECOMAE_CONFIRM_RESTART_PLATFORM=YES bash scripts/cloudpanel_sync_secret_succession_from_php.sh\n'
fi
