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
#    tenant's primary account when absent).
mysql epc_portal -e "ALTER TABLE epc_portal_tenants
  ADD COLUMN IF NOT EXISTS \`db_uat_user\` VARCHAR(64) NULL,
  ADD COLUMN IF NOT EXISTS \`db_uat_pass\` VARCHAR(128) NULL;" 2>/dev/null || \
mysql -e "ALTER TABLE epc_portal_tenants
  ADD COLUMN IF NOT EXISTS \`db_uat_user\` VARCHAR(64) NULL,
  ADD COLUMN IF NOT EXISTS \`db_uat_pass\` VARCHAR(128) NULL;"

# 2. Resolve or mint the password.
EXISTING=$(mysql -N -e "SELECT IFNULL(db_uat_pass,'') FROM epc_portal_tenants WHERE site_key='$SITE_KEY' LIMIT 1")
if [[ -n "$EXISTING" ]]; then
  PASS="$EXISTING"
else
  PASS=$(head -c 18 /dev/urandom | base64 | tr -dc 'a-zA-Z0-9' | head -c 24)
fi

# 3. Create the UAT account on every host the tenant account uses; grant ONLY
#    the sandbox schema.
mysql -N -e "SELECT DISTINCT host FROM mysql.user WHERE user='$DB_USER'" \
  | while read -r h; do
      mysql -e "CREATE USER IF NOT EXISTS \`$UAT_USER\`@'$h' IDENTIFIED BY '$PASS'; \
        ALTER USER \`$UAT_USER\`@'$h' IDENTIFIED BY '$PASS'; \
        GRANT ALL PRIVILEGES ON \`$SANDBOX_DB\`.* TO \`$UAT_USER\`@'$h';"
    done
mysql -e "FLUSH PRIVILEGES;"

# 4. Persist on the registry row — RouteTenantResolver swaps to these
#    credentials for sandbox host contexts.
mysql -e "UPDATE epc_portal_tenants SET db_uat_user='$UAT_USER', db_uat_pass='$PASS' WHERE site_key='$SITE_KEY';"

echo "OK — $UAT_USER provisioned on $SANDBOX_DB only; registry row '$SITE_KEY' updated"
