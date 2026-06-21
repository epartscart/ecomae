<?php
/**
 * CLI tests for Governance (roles/permissions, notifications, questionnaire).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_governance_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_governance.php';

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

foreach (array('epc_qn_responses', 'epc_qn_questionnaires', 'epc_ntf_notifications', 'epc_gov_user_roles', 'epc_gov_roles') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Roles + granular permissions');
$admin = epc_gov_role_save($db, 'superadmin', 'Super Admin', array('*'), true);
$sales = epc_gov_role_save($db, 'sales', 'Sales Rep', array('sales.view', 'sales.create', 'crm.*'));
$viewer = epc_gov_role_save($db, 'viewer', 'Viewer', array('sales.view'));
check('three roles created', $admin > 0 && $sales > 0 && $viewer > 0);

epc_gov_assign_role($db, 1, $admin);   // user 1 = superadmin
epc_gov_assign_role($db, 2, $sales);   // user 2 = sales
epc_gov_assign_role($db, 3, $viewer);  // user 3 = viewer

check('superadmin wildcard grants anything', epc_gov_can($db, 1, 'payroll.delete') === true);
check('sales has exact sales.create', epc_gov_can($db, 2, 'sales.create') === true);
check('sales has crm.* wildcard -> crm.edit', epc_gov_can($db, 2, 'crm.edit') === true);
check('sales lacks payroll.view', epc_gov_can($db, 2, 'payroll.view') === false);
check('viewer can view but not create', epc_gov_can($db, 3, 'sales.view') === true && epc_gov_can($db, 3, 'sales.create') === false);
$p2 = epc_gov_user_permissions($db, 2);
check('union of permissions for user', in_array('sales.view', $p2, true) && in_array('crm.*', $p2, true));

section('Multi-role union');
epc_gov_assign_role($db, 3, $sales); // viewer ALSO becomes sales
check('user with 2 roles gains sales.create', epc_gov_can($db, 3, 'sales.create') === true);

section('Notifications inbox');
epc_ntf_push($db, 5, array('title' => 'Invoice overdue', 'body' => 'INV-1 is overdue', 'severity' => 'warning'));
epc_ntf_push($db, 5, array('title' => 'New PO', 'body' => 'PO-9 created'));
check('unread count = 2', epc_ntf_unread_count($db, 5) === 2);
$first = (int) $db->query("SELECT id FROM epc_ntf_notifications WHERE user_id=5 ORDER BY id ASC LIMIT 1")->fetchColumn();
epc_ntf_mark_read($db, $first);
check('after mark-read, unread = 1', epc_ntf_unread_count($db, 5) === 1);
$allread = epc_ntf_mark_all_read($db, 5);
check('mark-all-read clears remaining (1)', $allread === 1 && epc_ntf_unread_count($db, 5) === 0);

section('Broadcast');
$n = epc_ntf_broadcast($db, array(10, 11, 12), array('title' => 'System maintenance', 'severity' => 'info'));
check('broadcast to 3 users', $n === 3);
check('each recipient has 1 unread', epc_ntf_unread_count($db, 10) === 1 && epc_ntf_unread_count($db, 12) === 1);

section('Questionnaire + scoring');
$qn = epc_qn_save($db, 'CSAT', 'Customer Satisfaction', array(
    array('key' => 'q1', 'text' => 'Service rating (1-5)', 'type' => 'number', 'weight' => 2),
    array('key' => 'q2', 'text' => 'Product rating (1-5)', 'type' => 'number', 'weight' => 1),
    array('key' => 'q3', 'text' => 'Comments', 'type' => 'text'),
));
$r1 = epc_qn_submit($db, $qn, 'cust-1', array('q1' => 5, 'q2' => 4, 'q3' => 'Great service'));
check('score = 5*2 + 4*1 = 14 (text ignored)', abs($r1['score'] - 14.0) < 0.01);
$r2 = epc_qn_submit($db, $qn, 'cust-2', array('q1' => 3, 'q2' => 2));
check('second score = 3*2 + 2*1 = 8', abs($r2['score'] - 8.0) < 0.01);
$sum = epc_qn_summary($db, $qn);
check('2 responses, avg score (14+8)/2 = 11', $sum['responses'] === 2 && abs($sum['avg_score'] - 11.0) < 0.01);

echo "\n========================================\n";
echo "GOVERNANCE TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
