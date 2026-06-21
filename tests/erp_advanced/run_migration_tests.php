<?php
/**
 * CLI tests for the data migration toolkit.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_migration_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_migration.php';

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

foreach (array('epc_mig_rows', 'epc_mig_batches', 'epc_mig_mappings', 'mig_target_items') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
// A fake target table to migrate into, with a writer/remover.
$db->exec("CREATE TABLE `mig_target_items` (`id` int AUTO_INCREMENT PRIMARY KEY, `code` varchar(40) UNIQUE, `name` varchar(120), `price` decimal(12,2))");
$writer = function (array $payload, string $natKey) use ($db): int {
    $db->prepare("INSERT INTO mig_target_items (code,name,price) VALUES (?,?,?) ON DUPLICATE KEY UPDATE name=VALUES(name), price=VALUES(price)")
       ->execute(array($payload['code'], $payload['name'] ?? '', (float) ($payload['price'] ?? 0)));
    $r = $db->prepare("SELECT id FROM mig_target_items WHERE code=?");
    $r->execute(array($payload['code']));
    return (int) $r->fetchColumn();
};
$remover = function (int $id) use ($db): void {
    $db->prepare("DELETE FROM mig_target_items WHERE id=?")->execute(array($id));
};

section('Field mapping');
epc_mig_save_mapping($db, 'tally', 'item', array('Item Code' => 'code', 'Item Name' => 'name', 'Rate' => 'price'));
$mapped = epc_mig_apply_mapping(
    array('Item Code' => 'code', 'Item Name' => 'name', 'Rate' => 'price'),
    array('Item Code' => 'A100', 'Item Name' => 'Filter', 'Rate' => '25.50', 'Ignored' => 'x')
);
check('mapped to ERP fields', $mapped['code'] === 'A100' && $mapped['name'] === 'Filter' && $mapped['price'] === '25.50');
check('unmapped source column dropped', !isset($mapped['Ignored']));
$reload = $db->query("SELECT map_json FROM epc_mig_mappings WHERE source_system='tally' AND entity='item'")->fetchColumn();
check('mapping template persisted', strpos((string) $reload, 'Item Code') !== false);

section('Row validation');
$spec = array('required' => array('code', 'name'), 'numeric' => array('price'), 'natural_key' => 'code');
$goodRows = array(
    array('code' => 'A100', 'name' => 'Filter', 'price' => '25.50'),
    array('code' => 'A101', 'name' => 'Belt', 'price' => '10'),
);
$v = epc_mig_validate_rows($goodRows, $spec);
check('clean rows valid', $v['valid'] === true && count($v['errors']) === 0);
$badRows = array(
    array('code' => 'A100', 'name' => 'Filter', 'price' => '25.50'),
    array('code' => '', 'name' => '', 'price' => 'abc'),       // missing + non-numeric
    array('code' => 'A100', 'name' => 'Dup', 'price' => '5'),  // duplicate key
);
$vb = epc_mig_validate_rows($badRows, $spec);
check('bad rows flagged invalid', $vb['valid'] === false);
$msgs = implode(' | ', array_column($vb['errors'], 'error'));
check('missing required reported', strpos($msgs, "missing required field 'code'") !== false);
check('non-numeric reported', strpos($msgs, "field 'price' must be numeric") !== false);
check('duplicate key reported', strpos($msgs, 'duplicate natural key') !== false);

section('Opening-balance trial-balance check');
$obOk = epc_mig_opening_balance_check(array(array('debit' => 1000, 'credit' => 0), array('debit' => 0, 'credit' => 1000)));
check('balanced opening (Dr=Cr) passes', $obOk['balanced'] === true);
$obBad = epc_mig_opening_balance_check(array(array('debit' => 1000, 'credit' => 0), array('debit' => 0, 'credit' => 700)));
check('unbalanced opening flagged with difference 300', $obBad['balanced'] === false && abs($obBad['difference'] - 300.0) < 0.01);

section('Batch create + commit (idempotent upsert)');
$batch = epc_mig_batch_create($db, 'MIG-ITEMS-1', 'item', 'tally', $goodRows, $spec, 'admin');
check('batch created, 2 rows, 0 errors, valid', $batch['row_count'] === 2 && $batch['error_count'] === 0 && $batch['valid'] === true);
$commit = epc_mig_batch_commit($db, $batch['batch_id'], $writer);
check('commit wrote 2 rows', $commit['committed'] === 2 && $commit['status'] === 'committed');
check('target table has 2 items', (int) $db->query("SELECT COUNT(*) FROM mig_target_items")->fetchColumn() === 2);
$commitAgain = epc_mig_batch_commit($db, $batch['batch_id'], $writer);
check('re-commit is no-op (already_committed)', $commitAgain['status'] === 'already_committed');

section('Batch with errors is blocked from commit');
$errBatch = epc_mig_batch_create($db, 'MIG-ITEMS-BAD', 'item', 'tally', $badRows, $spec, 'admin');
check('error batch records error_count', $errBatch['error_count'] > 0 && $errBatch['valid'] === false);
$blocked = epc_mig_batch_commit($db, $errBatch['batch_id'], $writer);
check('commit blocked due to validation errors', $blocked['status'] === 'blocked' && $blocked['committed'] === 0);

section('Rollback');
$rb = epc_mig_batch_rollback($db, $batch['batch_id'], $remover);
check('rollback removed 2 rows', $rb['rolled_back'] === 2 && $rb['status'] === 'rolled_back');
check('target table empty after rollback', (int) $db->query("SELECT COUNT(*) FROM mig_target_items")->fetchColumn() === 0);
$rbAgain = epc_mig_batch_rollback($db, $batch['batch_id'], $remover);
check('rollback again is safe (0 rows)', $rbAgain['rolled_back'] === 0);

echo "\n========================================\n";
echo "DATA MIGRATION TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
