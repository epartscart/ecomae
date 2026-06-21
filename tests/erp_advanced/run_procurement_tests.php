<?php
/**
 * CLI tests for Procurement & sourcing depth: categories, policies, purchase
 * requisitions (draft -> submit -> approve/reject -> convert), policy-driven
 * approval thresholds, totals, scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_procurement_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_procurement.php';

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

foreach (array('epc_proc_req_line', 'epc_proc_req', 'epc_proc_policy', 'epc_proc_category') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_proc_ensure_schema($db);

$CO = 1;

section('Categories');
$catIt = epc_proc_category_save($db, array('company_id' => $CO, 'code' => 'IT', 'name' => 'IT equipment', 'default_account' => '1600'));
$catSvc = epc_proc_category_save($db, array('company_id' => $CO, 'code' => 'SVC', 'name' => 'Professional services'));
check('two categories created', $catIt > 0 && $catSvc > 0);
check('categories scoped + ordered by code', count(epc_proc_categories($db, $CO)) === 2 && epc_proc_categories($db, $CO)[0]['code'] === 'IT');
check('other company sees none', count(epc_proc_categories($db, 999)) === 0);
check('category code+name required', (function () use ($db, $CO) { try { epc_proc_category_save($db, array('company_id' => $CO, 'code' => '', 'name' => '')); return false; } catch (Throwable $e) { return true; } })());
$db->prepare("UPDATE `epc_proc_category` SET `active`=0 WHERE `id`=?")->execute(array($catSvc));
check('active-only filter works', count(epc_proc_categories($db, $CO, true)) === 1);

section('Policies + approval rule');
$polIt = epc_proc_policy_save($db, array('company_id' => $CO, 'name' => 'IT spend', 'category_id' => $catIt, 'approval_threshold' => 5000, 'preferred_vendor' => 'Acme IT'));
$polDefault = epc_proc_policy_save($db, array('company_id' => $CO, 'name' => 'Company default', 'category_id' => 0, 'approval_threshold' => 1000));
check('two policies created', $polIt > 0 && $polDefault > 0);
check('policy resolves exact category', (int) epc_proc_policy_for_category($db, $CO, $catIt)['id'] === $polIt);
check('policy falls back to company default', (int) epc_proc_policy_for_category($db, $CO, 9999)['id'] === $polDefault);
check('under IT threshold -> no approval', epc_proc_requires_approval($db, $CO, $catIt, 4000) === false);
check('over IT threshold -> approval', epc_proc_requires_approval($db, $CO, $catIt, 6000) === true);
check('uncategorised uses default threshold', epc_proc_requires_approval($db, $CO, 9999, 1500) === true);
check('policy name required', (function () use ($db, $CO) { try { epc_proc_policy_save($db, array('company_id' => $CO, 'name' => '')); return false; } catch (Throwable $e) { return true; } })());

section('Requisition lifecycle — auto-approve under threshold');
$r1 = epc_proc_req_save($db, array('company_id' => $CO, 'requester' => 'Sara', 'business_unit_id' => 7, 'justification' => 'Laptops'));
check('requisition created as draft', epc_proc_req_get($db, $r1)['status'] === 'draft');
check('auto req number assigned', strpos((string) epc_proc_req_get($db, $r1)['req_number'], 'PR-') === 0);
epc_proc_req_add_line($db, $r1, array('category_id' => $catIt, 'item_code' => 'LAP', 'description' => 'Laptop', 'qty' => 2, 'unit_price' => 1500));
check('total recalculated', (float) epc_proc_req_get($db, $r1)['total'] === 3000.0);
check('preferred vendor inherited from policy', epc_proc_req_lines($db, $r1)[0]['preferred_vendor'] === 'Acme IT');
check('requires_approval false under threshold', (int) epc_proc_req_get($db, $r1)['requires_approval'] === 0);
$st1 = epc_proc_req_submit($db, $r1);
check('submit auto-approves under threshold', $st1 === 'approved' && epc_proc_req_get($db, $r1)['status'] === 'approved');
$po = epc_proc_req_convert($db, $r1);
check('approved req converts to PO', $po === 'PO-' . epc_proc_req_get($db, $r1)['req_number'] && epc_proc_req_get($db, $r1)['status'] === 'converted');

section('Requisition lifecycle — needs approval, then reject path');
$r2 = epc_proc_req_save($db, array('company_id' => $CO, 'requester' => 'Omar'));
epc_proc_req_add_line($db, $r2, array('category_id' => $catIt, 'item_code' => 'SRV', 'description' => 'Servers', 'qty' => 5, 'unit_price' => 2000));
check('over-threshold req flagged for approval', (int) epc_proc_req_get($db, $r2)['requires_approval'] === 1);
check('submit moves to submitted', epc_proc_req_submit($db, $r2) === 'submitted');
check('cannot add line after submit', (function () use ($db, $r2, $catIt) { try { epc_proc_req_add_line($db, $r2, array('category_id' => $catIt, 'qty' => 1, 'unit_price' => 1)); return false; } catch (Throwable $e) { return true; } })());
check('cannot convert before approval', (function () use ($db, $r2) { try { epc_proc_req_convert($db, $r2); return false; } catch (Throwable $e) { return true; } })());
epc_proc_req_decision($db, $r2, false, 'Manager', 'Budget exceeded');
check('rejected status + reason recorded', epc_proc_req_get($db, $r2)['status'] === 'rejected' && epc_proc_req_get($db, $r2)['decision_note'] === 'Budget exceeded');

section('Requisition lifecycle — approve path');
$r3 = epc_proc_req_save($db, array('company_id' => $CO, 'requester' => 'Lina'));
epc_proc_req_add_line($db, $r3, array('category_id' => $catIt, 'qty' => 4, 'unit_price' => 2000));
epc_proc_req_submit($db, $r3);
epc_proc_req_decision($db, $r3, true, 'Manager', 'OK');
check('approved status set', epc_proc_req_get($db, $r3)['status'] === 'approved');
check('cannot re-decide an approved req', (function () use ($db, $r3) { try { epc_proc_req_decision($db, $r3, true); return false; } catch (Throwable $e) { return true; } })());

section('Summary + scope');
$sum = epc_proc_summary($db, $CO);
check('summary counts converted=1', (int) $sum['converted'] === 1);
check('summary counts rejected=1', (int) $sum['rejected'] === 1);
check('summary counts approved=1', (int) $sum['approved'] === 1);
check('summary open value = approved r3 total (8000)', (float) $sum['open_value'] === 8000.0);
check('summary category + policy counts', (int) $sum['categories'] === 2 && (int) $sum['policies'] === 2);
check('reqs filter by status', count(epc_proc_reqs($db, $CO, 'converted')) === 1);
check('other company has empty summary', (int) epc_proc_summary($db, 999)['draft'] === 0 && count(epc_proc_reqs($db, 999)) === 0);

echo "\n========================================\n";
echo "PROCUREMENT TESTS: $pass_count passed, $fail_count failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
