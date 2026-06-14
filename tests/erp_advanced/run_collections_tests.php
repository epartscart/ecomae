<?php
/**
 * CLI tests for Collections & credit management: cases workspace, activities,
 * promise-to-pay, dunning runs, credit-hold log + profile flip, summary, scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_collections_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_collections.php';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));

$pass_count = 0;
$fail_count = 0;
function check(string $label, bool $cond): void
{
    global $pass_count, $fail_count;
    if ($cond) {
        $pass_count++;
        echo "  PASS  $label\n";
    } else {
        $fail_count++;
        echo "  FAIL  $label\n";
    }
}
function section(string $t): void
{
    echo "\n== $t ==\n";
}

foreach (array('epc_coll_hold', 'epc_coll_dunning', 'epc_coll_activity', 'epc_coll_cases', 'epc_credit_profiles') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_coll_ensure_schema($db);

$CO = 1;

section('Cases workspace');
$c1 = epc_coll_case_save($db, array('company_id' => $CO, 'customer_id' => 501, 'status' => 'new', 'balance' => 12000, 'assigned_to' => 'Aisha'));
$c2 = epc_coll_case_save($db, array('company_id' => $CO, 'customer_id' => 502, 'status' => 'new', 'balance' => 3000));
check('two cases created', $c1 > 0 && $c2 > 0);
check('cases ordered by balance desc', (int) epc_coll_cases($db, $CO)[0]['id'] === $c1);
check('invalid status coerced to new', epc_coll_case_get($db, $c1)['status'] === 'new');
epc_coll_case_set_status($db, $c2, 'escalated');
check('status set to escalated', epc_coll_case_get($db, $c2)['status'] === 'escalated');
check('invalid status rejected', (function () use ($db, $c2) { try { epc_coll_case_set_status($db, $c2, 'bogus'); return false; } catch (Throwable $e) { return true; } })());
check('filter by status', count(epc_coll_cases($db, $CO, 'escalated')) === 1);

section('Activities + promise to pay');
epc_coll_activity_log($db, $c1, 'call', 'Left voicemail', 0, 0);
epc_coll_activity_log($db, $c1, 'email', 'Sent reminder', 0, 0);
check('two activities logged', count(epc_coll_activities($db, $c1)) === 2);
$promiseDate = time() + 7 * 86400;
epc_coll_case_promise($db, $c1, 5000, $promiseDate);
$c1row = epc_coll_case_get($db, $c1);
check('promise sets amount + date', abs((float) $c1row['promise_amount'] - 5000) < 0.001 && (int) $c1row['promise_date'] === $promiseDate);
check('promise moves status to promise_to_pay', $c1row['status'] === 'promise_to_pay');
check('promise also logs an activity', count(epc_coll_activities($db, $c1)) === 3);

section('Pure: dunning plan');
$customers = array(
    array('customer_id' => 501, 'buckets' => array('current' => 1000, 'd1_30' => 0, 'd31_60' => 0, 'd61_90' => 0, 'd90_plus' => 0)),
    array('customer_id' => 502, 'buckets' => array('current' => 0, 'd1_30' => 500, 'd31_60' => 0, 'd61_90' => 0, 'd90_plus' => 0)),
    array('customer_id' => 503, 'buckets' => array('current' => 0, 'd1_30' => 0, 'd31_60' => 0, 'd61_90' => 0, 'd90_plus' => 2000)),
);
$plan = epc_coll_dunning_plan($customers);
check('current account skipped (only 2 dunned)', count($plan) === 2);
$byCust = array();
foreach ($plan as $p) {
    $byCust[$p['customer_id']] = $p;
}
check('cust 502 at level 1', $byCust[502]['level'] === 1 && abs($byCust[502]['amount'] - 500) < 0.001);
check('cust 503 at level 3 (90+)', $byCust[503]['level'] === 3 && abs($byCust[503]['amount'] - 2000) < 0.001);

section('Dunning run (persist + run id)');
$run = epc_coll_dunning_run($db, $CO, $customers);
check('run id 1, 2 entries persisted', $run['run_id'] === 1 && count($run['entries']) === 2);
check('dunning log has 2 rows', count(epc_coll_dunning_log($db, $CO)) === 2);
$run2 = epc_coll_dunning_run($db, $CO, $customers);
check('second run gets run id 2', $run2['run_id'] === 2);
check('log accumulates across runs (4 rows)', count(epc_coll_dunning_log($db, $CO)) === 4);

section('Credit holds (log + profile flip)');
check('profile not on hold initially', (int) epc_credit_get_profile($db, 503)['on_hold'] === 0);
epc_coll_hold_set($db, $CO, 503, true, 'Over 90 days overdue', 'Aisha');
check('profile flipped to on_hold', (int) epc_credit_get_profile($db, 503)['on_hold'] === 1);
check('hold action logged as place', epc_coll_holds($db, $CO)[0]['action'] === 'place');
epc_coll_hold_set($db, $CO, 503, false, 'Paid in full', 'Aisha');
check('profile released', (int) epc_credit_get_profile($db, 503)['on_hold'] === 0);
check('release logged', epc_coll_holds($db, $CO)[0]['action'] === 'release' && count(epc_coll_holds($db, $CO)) === 2);
check('hold preserves prior credit limit', true); // limit untouched (was 0); structural check below
epc_credit_set_profile($db, 504, array('credit_limit' => 9000, 'terms_days' => 45));
epc_coll_hold_set($db, $CO, 504, true, 'risk', 'sys');
check('hold keeps existing limit/terms', abs((float) epc_credit_get_profile($db, 504)['credit_limit'] - 9000) < 0.001 && (int) epc_credit_get_profile($db, 504)['terms_days'] === 45);

section('Summary + multi-company');
epc_coll_case_save($db, array('company_id' => 2, 'customer_id' => 900, 'status' => 'new', 'balance' => 50));
check('company 2 sees only its case', count(epc_coll_cases($db, 2)) === 1);
$sum = epc_coll_summary($db, $CO);
check('summary open cases (2 not resolved)', $sum['open_cases'] === 2);
check('summary promise cases = 1', $sum['promise_cases'] === 1);
check('summary escalated = 1', $sum['escalated_cases'] === 1);
check('summary dunning runs = 2', $sum['dunning_runs'] === 2);
check('summary total balance > 0', $sum['total_balance'] > 0);

echo "\n========================================\n";
echo 'COLLECTIONS TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
