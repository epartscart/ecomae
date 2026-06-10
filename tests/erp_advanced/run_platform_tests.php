<?php
/**
 * CLI integration tests for the platform layer:
 *   Credit & Collections, Collaboration/Workflow, Data import/export.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_platform_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(
    PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
));

$fin = dirname(__DIR__, 2) . '/content/shop/finance';
require_once $fin . '/epc_erp_credit.php';
require_once $fin . '/epc_erp_collab.php';
require_once $fin . '/epc_erp_dataio.php';

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

/* ----- minimal sales/cash tables for credit ageing (subset of live schema) - */
$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_sales_orders` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `customer_user_id` int(11) NOT NULL DEFAULT 0,
    `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
    `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
    `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
    `status` enum('draft','confirmed','invoiced','cancelled') NOT NULL DEFAULT 'draft',
    `time_created` int(11) NOT NULL DEFAULT 0,
    `time_updated` int(11) NOT NULL DEFAULT 0,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8");
$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_cash_bank_entries` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `account_id` int(11) NOT NULL DEFAULT 0,
    `time` int(11) NOT NULL DEFAULT 0,
    `direction` tinyint(1) NOT NULL DEFAULT 1,
    `amount` decimal(14,2) NOT NULL DEFAULT 0.00,
    `counterparty_type` varchar(16) NOT NULL DEFAULT 'none',
    `counterparty_id` int(11) NOT NULL DEFAULT 0,
    `order_id` int(11) NOT NULL DEFAULT 0,
    `active` tinyint(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8");

$cust = 501;
$now = time();
// Three invoices for the customer: current, ~45 days overdue, ~120 days overdue.
$db->exec("INSERT INTO `epc_erp_sales_orders` (`id`,`customer_user_id`,`total_amount`,`status`,`time_created`) VALUES
    (9001, $cust, 1000.00, 'invoiced', " . ($now - 10 * 86400) . "),
    (9002, $cust, 2000.00, 'invoiced', " . ($now - 75 * 86400) . "),
    (9003, $cust, 5000.00, 'invoiced', " . ($now - 150 * 86400) . ")");
// Partial receipt against invoice 9001 (500 of 1000).
$db->exec("INSERT INTO `epc_erp_cash_bank_entries` (`account_id`,`time`,`direction`,`amount`,`counterparty_type`,`counterparty_id`,`order_id`,`active`) VALUES
    (1, " . ($now - 5 * 86400) . ", 1, 500.00, 'customer', $cust, 9001, 1)");

section('Credit & Collections');
epc_credit_set_profile($db, $cust, array('credit_limit' => 5000, 'terms_days' => 30, 'risk_band' => 'watch'));
$p = epc_credit_get_profile($db, $cust);
check('credit profile persisted (limit 5000)', abs((float) $p['credit_limit'] - 5000.0) < 0.01);

$age = epc_credit_ageing($db, $cust, $now);
// 9001: total 1000 - 500 paid = 500 outstanding, 10 days old (<30 terms) -> current
// 9002: 2000, 75 days old -> 45 past due -> d31_60
// 9003: 5000, 150 days old -> 120 past due -> d90_plus
check('current bucket = 500 (partial, not yet due)', abs($age['buckets']['current'] - 500.0) < 0.01);
check('31-60 bucket = 2000', abs($age['buckets']['d31_60'] - 2000.0) < 0.01);
check('90+ bucket = 5000', abs($age['buckets']['d90_plus'] - 5000.0) < 0.01);
check('total outstanding = 7500', abs($age['total_outstanding'] - 7500.0) < 0.01);

$eval = epc_credit_evaluate($db, $cust, 0.0, $now);
check('credit evaluation denies (over limit + 90+ overdue)', $eval['approved'] === false);
check('evaluation lists reasons', count($eval['reasons']) >= 1);

$dun = epc_credit_dunning_level($age);
check('dunning level = 3 (final notice)', $dun['level'] === 3);

$stmt = epc_credit_statement($db, $cust, $now);
check('statement bundles ageing+dunning+eval', isset($stmt['ageing'], $stmt['dunning'], $stmt['evaluation']));

section('Collaboration & Workflow');
$thread = epc_collab_thread_open($db, array('entity_type' => 'purchase_order', 'entity_id' => 42, 'subject' => 'Need approval', 'department' => 'procurement', 'created_by' => 1));
check('thread opened', $thread > 0);
epc_collab_message_post($db, $thread, array('from_admin_id' => 1, 'to_department' => 'finance', 'body' => 'Please approve PO 42'));
epc_collab_message_post($db, $thread, array('from_admin_id' => 2, 'to_department' => 'procurement', 'body' => 'Approved from finance side'));
$msgs = epc_collab_thread_messages($db, $thread);
check('two messages recorded', count($msgs) === 2);
epc_collab_thread_close($db, $thread);
$closed = $db->query("SELECT status FROM epc_collab_threads WHERE id=$thread")->fetchColumn();
check('thread closed', $closed === 'closed');

// Two-step approval workflow.
$req = epc_wf_request_create($db, array('entity_type' => 'purchase_order', 'entity_id' => 42, 'title' => 'PO 42 approval', 'amount' => 12000, 'requested_by' => 1), array(
    array('approver_role' => 'manager', 'approver_admin_id' => 0),
    array('approver_role' => 'finance', 'approver_admin_id' => 0),
));
check('workflow request created', $req > 0);
$pendingMgr = epc_wf_pending_for_role($db, 'manager');
check('request pending for manager role', count($pendingMgr) === 1);

$after1 = epc_wf_decide($db, $req, 1, 'approve', 10, 'ok by manager');
check('still pending after step 1 of 2', $after1['status'] === 'pending' && (int) $after1['current_step'] === 2);
check('now pending for finance role', count(epc_wf_pending_for_role($db, 'finance')) === 1);
$after2 = epc_wf_decide($db, $req, 2, 'approve', 20, 'ok by finance');
check('approved after final step', $after2['status'] === 'approved');

// Rejection path.
$req2 = epc_wf_request_create($db, array('entity_type' => 'leave', 'entity_id' => 7, 'title' => 'Leave req', 'requested_by' => 3), array(
    array('approver_role' => 'manager', 'approver_admin_id' => 0),
));
$rej = epc_wf_decide($db, $req2, 1, 'reject', 10, 'not enough cover');
check('rejection sets status rejected', $rej['status'] === 'rejected');
$state = epc_wf_request_state($db, $req2);
check('rejected step recorded with comment', $state['steps'][0]['decision'] === 'rejected');

section('Data import / export');
$tpl = epc_dataio_template('items');
check('items template has SKU + Name headers', strpos($tpl, 'SKU') !== false && strpos($tpl, 'Name') !== false);
check('items template includes sample row', strpos($tpl, 'BRK-1001') !== false);

$csv = "SKU,Name,Unit,Cost Price,Reorder Level\r\nA-1,Widget,pc,10.50,5\r\nA-2,\"Gadget, deluxe\",pc,20,3\r\n,MissingSku,pc,1,1\r\nA-4,BadNum,pc,abc,2\r\n";
$imp = epc_dataio_import('items', $csv);
check('parsed 4 data rows', $imp['row_count'] === 4);
check('2 valid records', $imp['valid_count'] === 2);
check('2 error rows (missing sku + bad number)', $imp['error_count'] === 2);
check('quoted comma handled (Gadget, deluxe)', $imp['records'][1]['name'] === 'Gadget, deluxe');
check('numeric coercion (cost 10.5 float)', $imp['records'][0]['cost_price'] === 10.5);
check('int coercion (reorder 5)', $imp['records'][0]['reorder_level'] === 5);

$export = epc_dataio_export_csv('suppliers', array(
    array('name' => 'Acme', 'email' => 'a@acme.com', 'country_code' => 'AE'),
    array('name' => 'Beta "B" Co', 'country_code' => 'GB'),
));
check('export has header row', strpos($export, 'Name') !== false);
check('export escapes embedded quotes', strpos($export, '"Beta ""B"" Co"') !== false);

echo "\n========================================\n";
echo "PLATFORM TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
