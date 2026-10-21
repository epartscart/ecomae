#!/usr/bin/env bash
# Provision a dedicated UAT MySQL account for an ERP-only tenant's sandbox
# (D365-style least privilege: the sandbox host authenticates as {db_user}_uat
# with privileges on {db}_sandbox ONLY — a leaked UAT session can never reach
# the live tenant schema).
#
# Idempotent: re-running rotates nothing when db_uat_pass is already stored.
#
# Usage: bash scripts/cloudpanel_sandbox_uat_user.sh <site_key> <live_db> <db_user>
#   e.g. bash scripts/cloudpanel_sandbox_uat_user.sh sham sham sham
#
# Root MySQL access via MYSQL_PWD / ~/.my.cnf.
set -euo pipefail

MYSQL_BIN="${MYSQL_BIN:-mysql}"

SITE_KEY="${1:?usage: $0 <site_key> <live_db> <db_user>}"
LIVE_DB="${2:?usage: $0 <site_key> <live_db> <db_user>}"
DB_USER="${3:?usage: $0 <site_key> <live_db> <db_user>}"
SANDBOX_DB="${LIVE_DB}_sandbox"
UAT_USER="${DB_USER}_uat"

for ident in "$SITE_KEY" "$LIVE_DB" "$DB_USER" "$SANDBOX_DB" "$UAT_USER"; do
  if ! [[ "$ident" =~ ^[a-z0-9_]+$ ]]; then
    echo "unsafe identifier '$ident' — expected [a-z0-9_]" >&2; exit 2
  fi
done

# 1. Registry slots for the UAT credential (nullable — code falls back to the
#    tenant's primary account when absent). MariaDB accepts IF NOT EXISTS on
#    ADD COLUMN; MySQL 8 does not — guard via information_schema.
# Many tenant DBs carry their own epc_portal_tenants stub — the real registry is
# the schema whose row holds this site_key (usually `ecomae`). Pick it or fail.
REG_SCHEMA=""
MATCHES=0
for s in $($MYSQL_BIN -N -e "SELECT table_schema FROM information_schema.tables WHERE table_name='epc_portal_tenants'"); do
  if [[ "$($MYSQL_BIN -N -e "SELECT COUNT(*) FROM \`$s\`.epc_portal_tenants WHERE site_key='$SITE_KEY'" 2>/dev/null)" == "1" ]]; then
    MATCHES=$((MATCHES + 1))
    # The platform registry lives in `ecomae` — prefer it when several
    # schemas carry the same site_key (tenant DB stubs can echo the row).
    if [[ "$s" == "ecomae" || -z "$REG_SCHEMA" ]]; then
      REG_SCHEMA="$s"
    fi
  fi
done
if [[ -z "$REG_SCHEMA" ]]; then
  echo "no epc_portal_tenants schema holds site_key '$SITE_KEY'" >&2; exit 3
fi
if [[ "$MATCHES" -gt 1 ]]; then
  echo "WARN: $MATCHES schemas hold site_key '$SITE_KEY' — updating '$REG_SCHEMA'" >&2
fi
REG_DB="\`${REG_SCHEMA}\`.\`epc_portal_tenants\`"
for col in "db_uat_user VARCHAR(64) NULL" "db_uat_pass VARCHAR(128) NULL"; do
  cname="${col%% *}"
  if ! $MYSQL_BIN -N -e "SELECT 1 FROM information_schema.columns WHERE table_schema='$REG_SCHEMA' AND table_name='epc_portal_tenants' AND column_name='$cname'" | grep -q 1; then
    $MYSQL_BIN -e "ALTER TABLE $REG_DB ADD COLUMN \`$cname\` ${col#* };"
  fi
done

# 2. Resolve or mint the password.
EXISTING=$($MYSQL_BIN -N -e "SELECT IFNULL(db_uat_pass,'') FROM $REG_DB WHERE site_key='$SITE_KEY' LIMIT 1")
if [[ -n "$EXISTING" ]]; then
  PASS="$EXISTING"
else
  PASS=$(head -c 18 /dev/urandom | base64 | tr -dc 'a-zA-Z0-9' | head -c 24)
fi

# 3. Create the UAT account on every host the tenant account uses; grant ONLY
#    the sandbox schema.
$MYSQL_BIN -N -e "SELECT DISTINCT host FROM mysql.user WHERE user='$DB_USER'" \
  | while read -r h; do
      $MYSQL_BIN -e "CREATE USER IF NOT EXISTS \`$UAT_USER\`@'$h' IDENTIFIED WITH mysql_native_password BY '$PASS'; \
        ALTER USER \`$UAT_USER\`@'$h' IDENTIFIED WITH mysql_native_password BY '$PASS'; \
        GRANT ALL PRIVILEGES ON \`$SANDBOX_DB\`.* TO \`$UAT_USER\`@'$h';"
    done
$MYSQL_BIN -e "FLUSH PRIVILEGES;"

# 4. Persist on the registry row — RouteTenantResolver swaps to these
#    credentials for sandbox host contexts.
$MYSQL_BIN -e "UPDATE $REG_DB SET db_uat_user='$UAT_USER', db_uat_pass='$PASS' WHERE site_key='$SITE_KEY';"

echo "OK — $UAT_USER provisioned on $SANDBOX_DB only; registry row '$SITE_KEY' updated"
