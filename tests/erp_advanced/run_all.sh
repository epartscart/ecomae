#!/usr/bin/env bash
# Run every ERP advanced test runner and tally the totals.
#
#   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_NAME2=erp_test_b \
#     DB_USER=erp DB_PASS=erp bash tests/erp_advanced/run_all.sh
set -u

DIR="$(cd "$(dirname "$0")" && pwd)"
export DB_HOST="${DB_HOST:-127.0.0.1}"
export DB_NAME="${DB_NAME:-erp_test}"
export DB_NAME2="${DB_NAME2:-erp_test_b}"
export DB_USER="${DB_USER:-erp}"
export DB_PASS="${DB_PASS:-erp}"

# Drop all tables in both test DBs so re-runs start clean (runners that don't
# self-drop would otherwise hit duplicate-key seeds on the 2nd run).
wipe_db() {
    local db="$1"
    php -r '
        $h=getenv("DB_HOST"); $u=getenv("DB_USER"); $p=getenv("DB_PASS"); $d=$argv[1];
        try {
            $pdo=new PDO("mysql:host=$h;dbname=$d;charset=utf8",$u,$p,array(PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION));
            $pdo->exec("SET FOREIGN_KEY_CHECKS=0");
            foreach($pdo->query("SHOW TABLES")->fetchAll(PDO::FETCH_COLUMN) as $t){$pdo->exec("DROP TABLE IF EXISTS `$t`");}
            $pdo->exec("SET FOREIGN_KEY_CHECKS=1");
        } catch (Throwable $e) { /* db may not exist; ignore */ }
    ' "$db" 2>/dev/null
}
wipe_db "$DB_NAME"
wipe_db "$DB_NAME2"

total_pass=0
total_fail=0
failed_suites=""

for f in "$DIR"/run_*.php; do
    name="$(basename "$f")"
    out="$(php "$f" 2>&1)"
    code=$?
    # Pull the "N passed, M failed" summary line from each runner.
    line="$(echo "$out" | grep -iE "[0-9]+ pass" | tail -1)"
    p="$(echo "$line" | grep -oiE "[0-9]+ pass" | grep -oE "[0-9]+" | head -1)"
    m="$(echo "$line" | grep -oiE "[0-9]+ fail" | grep -oE "[0-9]+" | head -1)"
    p="${p:-0}"; m="${m:-0}"
    total_pass=$((total_pass + p))
    total_fail=$((total_fail + m))
    if [ "$code" -ne 0 ] || [ "$m" -ne 0 ]; then
        failed_suites="$failed_suites $name"
        printf "  FAIL  %-34s %s pass / %s fail\n" "$name" "$p" "$m"
    else
        printf "  ok    %-34s %s pass\n" "$name" "$p"
    fi
done

echo "==================================================="
echo "TOTAL: ${total_pass} passed, ${total_fail} failed"
if [ -n "$failed_suites" ]; then
    echo "Failing suites:$failed_suites"
    exit 1
fi
echo "==================================================="
exit 0
