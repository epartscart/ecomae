<?php
/**
 * CLI tests for Quality management: pure test evaluation + verdict, test plans
 * & tests, quality orders (record results + verdict), non-conformance workflow,
 * summary and multi-company scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_quality_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_quality.php';

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

foreach (array('epc_qm_result', 'epc_qm_order', 'epc_qm_test', 'epc_qm_plan', 'epc_qm_ncr') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_qm_ensure_schema($db);

$CO = 1;

section('Pure: evaluate test');
$qt = array('test_type' => 'quantitative', 'min_val' => 10, 'max_val' => 20);
check('quantitative within range -> pass', epc_qm_eval_test($qt, 15) === 'pass');
check('quantitative below min -> fail', epc_qm_eval_test($qt, 9.9) === 'fail');
check('quantitative above max -> fail', epc_qm_eval_test($qt, 20.1) === 'fail');
check('quantitative at boundary -> pass', epc_qm_eval_test($qt, 20) === 'pass');
check('quantitative null measure -> fail', epc_qm_eval_test($qt, null) === 'fail');
$qtMinOnly = array('test_type' => 'quantitative', 'min_val' => 5, 'max_val' => null);
check('min-only bound passes above', epc_qm_eval_test($qtMinOnly, 1000) === 'pass');
check('min-only bound fails below', epc_qm_eval_test($qtMinOnly, 4) === 'fail');
$ql = array('test_type' => 'qualitative', 'expected' => 'Pass');
check('qualitative match (case-insensitive) -> pass', epc_qm_eval_test($ql, null, 'pass') === 'pass');
check('qualitative mismatch -> fail', epc_qm_eval_test($ql, null, 'cracked') === 'fail');

section('Pure: verdict');
check('empty -> not evaluated', epc_qm_verdict(array()) === '');
check('all pass -> pass', epc_qm_verdict(array('pass', 'pass')) === 'pass');
check('any fail -> fail', epc_qm_verdict(array('pass', 'fail', 'pass')) === 'fail');

section('Plans + tests');
$plan = epc_qm_plan_save($db, $CO, array('code' => 'QP-BRAKE', 'name' => 'Brake pad inspection', 'active' => 1));
check('plan saved with id', $plan > 0);
check('plan code required', (function () use ($db, $CO) { try { epc_qm_plan_save($db, $CO, array('code' => '')); return false; } catch (Throwable $e) { return true; } })());
$t1 = epc_qm_test_add($db, $plan, array('name' => 'Thickness', 'test_type' => 'quantitative', 'unit' => 'mm', 'min_val' => 10, 'max_val' => 14));
$t2 = epc_qm_test_add($db, $plan, array('name' => 'Surface', 'test_type' => 'qualitative', 'expected' => 'smooth'));
check('two tests on plan', count(epc_qm_plan_tests($db, $plan)) === 2);
check('invalid test type rejected', (function () use ($db, $plan) { try { epc_qm_test_add($db, $plan, array('name' => 'x', 'test_type' => 'bogus')); return false; } catch (Throwable $e) { return true; } })());
check('plans list shows test_count=2', epc_qm_plans($db, $CO)[0]['test_count'] === 2);

section('Quality order — pass path');
$o1 = epc_qm_order_create($db, $CO, array('plan_id' => $plan, 'ref_type' => 'po', 'ref_id' => 'PO-1001', 'item_id' => 5, 'qty' => 100));
check('order created', $o1 > 0);
$rec1 = epc_qm_order_record($db, $o1, array($t1 => array('value_num' => 12), $t2 => array('value_text' => 'smooth')));
check('order pass verdict', $rec1['verdict'] === 'pass');
check('two results stored', count(epc_qm_order_results($db, $o1)) === 2);

section('Quality order — fail path + re-record');
$o2 = epc_qm_order_create($db, $CO, array('plan_id' => $plan, 'ref_type' => 'po', 'ref_id' => 'PO-1002', 'item_id' => 5, 'qty' => 50));
$rec2 = epc_qm_order_record($db, $o2, array($t1 => array('value_num' => 8), $t2 => array('value_text' => 'rough')));
check('order fail verdict', $rec2['verdict'] === 'fail');
// re-record corrected: results replaced, not duplicated
$rec2b = epc_qm_order_record($db, $o2, array($t1 => array('value_num' => 12), $t2 => array('value_text' => 'smooth')));
check('re-record flips to pass', $rec2b['verdict'] === 'pass');
check('re-record replaces (still 2 results)', count(epc_qm_order_results($db, $o2)) === 2);
check('record unknown order throws', (function () use ($db) { try { epc_qm_order_record($db, 999999, array()); return false; } catch (Throwable $e) { return true; } })());

section('Non-conformance workflow');
$ncr = epc_qm_ncr_create($db, $CO, array('order_id' => $o2, 'title' => 'Undersized thickness', 'severity' => 'major', 'disposition' => 'rework'));
check('ncr created', $ncr > 0);
check('invalid severity rejected', (function () use ($db, $CO) { try { epc_qm_ncr_create($db, $CO, array('title' => 'x', 'severity' => 'apocalyptic')); return false; } catch (Throwable $e) { return true; } })());
check('invalid disposition rejected', (function () use ($db, $CO) { try { epc_qm_ncr_create($db, $CO, array('title' => 'x', 'severity' => 'minor', 'disposition' => 'eat_it')); return false; } catch (Throwable $e) { return true; } })());
epc_qm_ncr_update($db, $ncr, array('status' => 'investigate', 'disposition' => 'rework', 'corrective_action' => 'Adjust press'));
$open = epc_qm_ncrs($db, $CO)[0];
check('ncr status updated', $open['status'] === 'investigate' && $open['time_closed'] == 0);
check('invalid status rejected', (function () use ($db, $ncr) { try { epc_qm_ncr_update($db, $ncr, array('status' => 'levitating')); return false; } catch (Throwable $e) { return true; } })());
epc_qm_ncr_update($db, $ncr, array('status' => 'closed', 'disposition' => 'rework', 'corrective_action' => 'Done'));
$closed = epc_qm_ncrs($db, $CO)[0];
check('ncr close stamps time_closed', $closed['status'] === 'closed' && $closed['time_closed'] > 0);

section('Summary + multi-company');
epc_qm_plan_save($db, 2, array('code' => 'OTHER', 'name' => 'Other co', 'active' => 1));
check('company 2 isolated (1 plan, 0 orders)', epc_qm_summary($db, 2)['plans'] === 1 && epc_qm_summary($db, 2)['orders'] === 0);
$sum = epc_qm_summary($db, $CO);
check('summary plans = 1', $sum['plans'] === 1);
check('summary orders = 2', $sum['orders'] === 2);
check('summary passed = 2', $sum['passed'] === 2);
check('summary open_ncr = 0 (closed)', $sum['open_ncr'] === 0);

echo "\n========================================\n";
echo 'QUALITY TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
