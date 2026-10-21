#!/usr/bin/env bash
# Refresh an ERP-only tenant's UAT database from production (D365-style
# "copy production to sandbox").
#
# Safety properties:
#   - staged import: the live dump lands in <live>_sandbox_next FIRST; the
#     visible <live>_sandbox schema is only dropped+repointed once the whole
#     dump imported cleanly — an interrupted refresh never leaves a
#     half-replaced sandbox, and tables production dropped never survive.
#   - identifiers validated ([a-z0-9_]) before use — no SQL injection surface.
#
# Usage: bash scripts/cloudpanel_sandbox_db_refresh.sh <live_db> <db_user>
#   e.g. bash scripts/cloudpanel_sandbox_db_refresh.sh sham sham
#
# Root MySQL access via MYSQL_PWD / ~/.my.cnf. Destructive to UAT by design.
set -euo pipefail

LIVE_DB="${1:?usage: $0 <live_db> <db_user>}"
DB_USER="${2:?usage: $0 <live_db> <db_user>}"
SANDBOX_DB="${LIVE_DB}_sandbox"
STAGING_DB="${LIVE_DB}_sandbox_next"

for ident in "$LIVE_DB" "$DB_USER" "$SANDBOX_DB" "$STAGING_DB"; do
  if ! [[ "$ident" =~ ^[a-z0-9_]+$ ]]; then
    echo "unsafe identifier '$ident' — expected [a-z0-9_]" >&2; exit 2
  fi
done

# 1. Build a fresh staging copy. --single-transaction keeps live readable.
mysql -e "DROP DATABASE IF EXISTS \`$STAGING_DB\`; CREATE DATABASE \`$STAGING_DB\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysqldump --single-transaction --routines --triggers "$LIVE_DB" | mysql "$STAGING_DB"

# 2. Swap staging into place only after the full import succeeded.
mysql -e "DROP DATABASE IF EXISTS \`$SANDBOX_DB\`; CREATE DATABASE \`$SANDBOX_DB\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -N -e "SELECT table_name FROM information_schema.tables WHERE table_schema='$STAGING_DB'" \
  | while read -r t; do
      mysql -e "RENAME TABLE \`$STAGING_DB\`.\`$t\` TO \`$SANDBOX_DB\`.\`$t\`"
    done
mysql -e "DROP DATABASE \`$STAGING_DB\`;"

# 2b. Stamp the refresh — Super CP reads this to show each tenant's last UAT refresh.
mysql -e "CREATE TABLE IF NOT EXISTS \`$SANDBOX_DB\`.\`epc_sandbox_meta\` (\`meta_key\` VARCHAR(64) NOT NULL PRIMARY KEY, \`meta_value\` VARCHAR(255) NOT NULL) ENGINE=InnoDB; \
  INSERT INTO \`$SANDBOX_DB\`.\`epc_sandbox_meta\`(\`meta_key\`, \`meta_value\`) VALUES \
    ('refreshed_from', '$LIVE_DB'), ('refreshed_at_utc', UTC_TIMESTAMP()), ('refresh_tool', 'cloudpanel_sandbox_db_refresh.sh') \
  ON DUPLICATE KEY UPDATE \`meta_value\` = VALUES(\`meta_value\`);"

# 3. Grant the tenant account on the rebuilt sandbox — on the host records
# that actually exist for it (a GRANT for a missing user@host errors on
# MySQL 8: "not allowed to create a user with GRANT").
mysql -N -e "SELECT DISTINCT host FROM mysql.user WHERE user='$DB_USER'" \
  | while read -r h; do
      mysql -e "GRANT ALL PRIVILEGES ON \`$SANDBOX_DB\`.* TO \`$DB_USER\`@'$h'"
    done
# Dedicated UAT account ({db_user}_uat) gets its sandbox grant rebuilt too —
# a fresh {db}_sandbox otherwise drops the grant and sandbox hosts lose auth.
UAT_USER="${DB_USER}_uat"
if mysql -N -e "SELECT 1 FROM mysql.user WHERE user='$UAT_USER'" | grep -q 1; then
  mysql -N -e "SELECT DISTINCT host FROM mysql.user WHERE user='$UAT_USER'" \
    | while read -r h; do
        mysql -e "GRANT ALL PRIVILEGES ON \`$SANDBOX_DB\`.* TO \`$UAT_USER\`@'$h'"
      done
fi
# Shared platform account (ecomae) also reads every tenant DB.
if mysql -N -e "SELECT 1 FROM mysql.user WHERE user='ecomae'" | grep -q 1; then
  mysql -e "GRANT ALL PRIVILEGES ON \`$SANDBOX_DB\`.* TO 'ecomae'@'%'"
fi
mysql -e "FLUSH PRIVILEGES;"

echo "OK — refreshed $SANDBOX_DB from $LIVE_DB (staged swap)"
