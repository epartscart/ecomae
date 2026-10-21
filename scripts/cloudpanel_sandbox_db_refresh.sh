#!/usr/bin/env bash
# Refresh an ERP-only tenant's UAT database from production (D365-style
# "copy production to sandbox"): dumps <live_db> into <live_db>_sandbox and
# grants the tenant's MySQL user on it.
#
# Usage: bash scripts/cloudpanel_sandbox_db_refresh.sh <live_db> <db_user>
#   e.g. bash scripts/cloudpanel_sandbox_db_refresh.sh ecomae_sham ecomae_sham
#
# Root MySQL access comes from the usual CloudPanel root credentials
# (MYSQL_PWD env or ~/.my.cnf). Idempotent — drops and recreates the sandbox
# schema on every run, so treat it as destructive to UAT data by design.
set -euo pipefail

LIVE_DB="${1:?usage: $0 <live_db> <db_user>}"
DB_USER="${2:?usage: $0 <live_db> <db_user>}"
SANDBOX_DB="${LIVE_DB}_sandbox"

if ! [[ "$LIVE_DB" =~ ^[a-z0-9_]+$ && "$SANDBOX_DB" =~ ^[a-z0-9_]+$ ]]; then
  echo "bad database name '$LIVE_DB'" >&2; exit 2
fi

mysql -e "CREATE DATABASE IF NOT EXISTS \`$SANDBOX_DB\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -e "GRANT ALL PRIVILEGES ON \`$SANDBOX_DB\`.* TO \`$DB_USER\`@'localhost'; GRANT ALL PRIVILEGES ON \`$SANDBOX_DB\`.* TO \`$DB_USER\`@'127.0.0.1'; FLUSH PRIVILEGES;"

# Replace the sandbox contents with a clean copy of live. --single-transaction
# keeps the live DB readable during the dump (InnoDB only).
mysqldump --single-transaction --routines --triggers "$LIVE_DB" \
  | mysql "$SANDBOX_DB"

echo "OK — refreshed $SANDBOX_DB from $LIVE_DB"
