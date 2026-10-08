#!/usr/bin/env bash
# Builds fixture.sql (the PHP inventory schema plus data.sql) and records the goldens of PHP
# epc_erp_inventory_record_sale_demand on a throwaway database.
# Usage: harness.sh <repo_root> <mysql_password>
set -euo pipefail
root="$1"
password="$2"
here="$(cd "$(dirname "$0")" && pwd)"
db="ecomae_cpw_sd$(date +%s)"
cnf="$(mktemp)"
printf '[client]\nuser=ecomae\npassword=%s\nhost=127.0.0.1\n' "$password" > "$cnf"
trap 'mysql --defaults-extra-file="$cnf" -e "DROP DATABASE IF EXISTS \`$db\`"; rm -f "$cnf"' EXIT
dsn="mysql:host=127.0.0.1;dbname=$db;charset=utf8"

mysql --defaults-extra-file="$cnf" -e "CREATE DATABASE \`$db\`"
php "$here/harness.php" "$root" "$dsn" ecomae "$password" schema
mysqldump --defaults-extra-file="$cnf" --no-data --skip-comments --skip-add-drop-table --compact "$db" \
	| sed -e 's/ AUTO_INCREMENT=[0-9]*//' -e '/^\/\*[!M]/d' -e '/^SET /d' > "$here/fixture.sql"
cat "$here/data.sql" >> "$here/fixture.sql"
mysql --defaults-extra-file="$cnf" "$db" < "$here/data.sql"
php "$here/harness.php" "$root" "$dsn" ecomae "$password" run "$here"
