<?php
/**
 * CLI tests for Costing value-models: pure FIFO/LIFO/moving-average/standard
 * COGS + closing value + variance, per-item model, transactions, closing runs,
 * compare, summary, multi-company scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_cost_models_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_cost_models.php';

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

foreach (array('epc_costm_close', 'epc_costm_txn', 'epc_costm_item') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_costm_ensure_schema($db);

$CO = 1;

// Classic example: receive 10@10, receive 10@12, issue 15.
$txns = array(
    array('txn_type' => 'receipt', 'qty' => 10, 'unit_cost' => 10),
    array('txn_type' => 'receipt', 'qty' => 10, 'unit_cost' => 12),
    array('txn_type' => 'issue', 'qty' => 15, 'unit_cost' => 0),
);

section('Pure: FIFO');
$fifo = epc_costm_compute('fifo', $txns);
// FIFO issue 15 = 10@10 + 5@12 = 160; remaining 5@12 = 60
check('FIFO COGS = 160', abs($fifo['cogs'] - 160) < 0.001);
check('FIFO closing qty = 5', abs($fifo['closing_qty'] - 5) < 0.001);
check('FIFO closing value = 60', abs($fifo['closing_value'] - 60) < 0.001);

section('Pure: LIFO');
$lifo = epc_costm_compute('lifo', $txns);
// LIFO issue 15 = 10@12 + 5@10 = 170; remaining 5@10 = 50
check('LIFO COGS = 170', abs($lifo['cogs'] - 170) < 0.001);
check('LIFO closing qty = 5', abs($lifo['closing_qty'] - 5) < 0.001);
check('LIFO closing value = 50', abs($lifo['closing_value'] - 50) < 0.001);

section('Pure: Moving average');
$ma = epc_costm_compute('moving_avg', $txns);
// avg after both receipts = (100+120)/20 = 11; issue 15*11 = 165; remaining 5*11 = 55
check('MA COGS = 165', abs($ma['cogs'] - 165) < 0.001);
check('MA closing qty = 5', abs($ma['closing_qty'] - 5) < 0.001);
check('MA closing value = 55', abs($ma['closing_value'] - 55) < 0.001);

section('Pure: Standard + PPV');
$std = epc_costm_compute('standard', $txns, 11.0);
// issue 15@11 = 165; PPV = 10*(10-11) + 10*(12-11) = -10 + 10 = 0; closing 5*11=55
check('STD COGS = 165 (at std)', abs($std['cogs'] - 165) < 0.001);
check('STD variance (PPV) = 0', abs($std['variance'] - 0) < 0.001);
check('STD closing value = 55', abs($std['closing_value'] - 55) < 0.001);
$std2 = epc_costm_compute('standard', array(array('txn_type' => 'receipt', 'qty' => 10, 'unit_cost' => 13)), 11.0);
check('STD PPV positive when buy above std (10*2=20)', abs($std2['variance'] - 20) < 0.001);

section('Pure: edge cases');
check('unknown model falls back to moving_avg', epc_costm_compute('zzz', $txns)['model'] === 'moving_avg');
$short = epc_costm_compute('fifo', array(array('txn_type' => 'receipt', 'qty' => 5, 'unit_cost' => 10), array('txn_type' => 'issue', 'qty' => 8, 'unit_cost' => 0)));
// 5@10 = 50 + shortfall 3@10 (last cost) = 30 -> 80; closing qty 0
check('FIFO shortfall valued at last cost (COGS 80)', abs($short['cogs'] - 80) < 0.001);
check('FIFO over-issue closing qty 0', abs($short['closing_qty'] - 0) < 0.001);

section('Per-item model + transactions + closing run');
epc_costm_item_set($db, $CO, 9001, 'fifo', 0);
check('item model saved', epc_costm_item_get($db, $CO, 9001)['model'] === 'fifo');
check('invalid model rejected', (function () use ($db, $CO) { try { epc_costm_item_set($db, $CO, 1, 'bogus'); return false; } catch (Throwable $e) { return true; } })());
epc_costm_txn_add($db, $CO, 9001, 'receipt', 10, 10, 1000);
epc_costm_txn_add($db, $CO, 9001, 'receipt', 10, 12, 2000);
epc_costm_txn_add($db, $CO, 9001, 'issue', 15, 0, 3000);
check('three txns stored chronologically', count(epc_costm_txns($db, $CO, 9001)) === 3);
check('invalid txn type rejected', (function () use ($db, $CO) { try { epc_costm_txn_add($db, $CO, 9001, 'bogus', 1, 1); return false; } catch (Throwable $e) { return true; } })());
$run = epc_costm_close_run($db, $CO, 9001, '2026-06');
check('closing run uses FIFO (COGS 160, value 60)', abs($run['cogs'] - 160) < 0.001 && abs($run['closing_value'] - 60) < 0.001);
check('closing persisted', count(epc_costm_closes($db, $CO, 9001)) === 1);

section('Switching model re-runs differently');
epc_costm_item_set($db, $CO, 9001, 'lifo', 0);
$run2 = epc_costm_close_run($db, $CO, 9001, '2026-06b');
check('after switch to LIFO closing value = 50', abs($run2['closing_value'] - 50) < 0.001);
check('two closings now', count(epc_costm_closes($db, $CO, 9001)) === 2);

section('Compare all models');
$cmp = epc_costm_compare($txns, 11.0);
check('compare returns all 4 models', count($cmp) === 4 && isset($cmp['fifo'], $cmp['lifo'], $cmp['moving_avg'], $cmp['standard']));
check('compare FIFO vs LIFO COGS differ (160 vs 170)', abs($cmp['fifo']['cogs'] - 160) < 0.001 && abs($cmp['lifo']['cogs'] - 170) < 0.001);

section('Summary + multi-company');
epc_costm_item_set($db, 2, 5, 'standard', 7);
check('company 2 isolated', epc_costm_summary($db, 2)['items'] === 1);
$sum = epc_costm_summary($db, $CO);
check('summary items = 1', $sum['items'] === 1);
check('summary closings = 2', $sum['closings'] === 2);
check('summary closing value uses latest (LIFO 50)', abs($sum['closing_value'] - 50) < 0.001);

echo "\n========================================\n";
echo 'COST MODELS TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
