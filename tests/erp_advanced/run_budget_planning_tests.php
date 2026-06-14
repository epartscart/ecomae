<?php
/**
 * CLI tests for Budgeting depth: budget plans, worksheet lines (scenarios),
 * forecast positions, staged workflow (draft->review->approved->published),
 * publish total freeze, scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_budget_planning_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_budget_planning.php';

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

foreach (array('epc_bplan_position', 'epc_bplan_line', 'epc_bplan_plan') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_bplan_ensure_schema($db);

$CO = 1;

section('Plans');
$p1 = epc_bplan_save($db, array('company_id' => $CO, 'name' => 'FY26 Operating', 'fiscal_year' => '2026', 'owner' => 'CFO'));
check('plan created as draft', epc_bplan_get($db, $p1)['stage'] === 'draft');
check('plan name required', (function () use ($db, $CO) { try { epc_bplan_save($db, array('company_id' => $CO, 'name' => '')); return false; } catch (Throwable $e) { return true; } })());
check('plans scoped', count(epc_bplan_list($db, $CO)) === 1 && count(epc_bplan_list($db, 999)) === 0);

section('Worksheet lines + scenarios');
epc_bplan_line_add($db, $p1, array('account' => '6000', 'scenario' => 'base', 'period' => 'Q1', 'amount' => 10000));
epc_bplan_line_add($db, $p1, array('account' => '6000', 'scenario' => 'base', 'period' => 'Q2', 'amount' => 12000));
epc_bplan_line_add($db, $p1, array('account' => '6000', 'scenario' => 'optimistic', 'period' => 'Q1', 'amount' => 15000));
check('lines added', count(epc_bplan_lines($db, $p1)) === 3);
check('scenario filter', count(epc_bplan_lines($db, $p1, 'base')) === 2);
check('total all scenarios', epc_bplan_total($db, $p1) === 37000.0);
check('total base scenario', epc_bplan_total($db, $p1, 'base') === 22000.0);

section('Forecast positions');
$pos1 = epc_bplan_position_add($db, $p1, array('title' => 'Accountant', 'department' => 'Finance', 'headcount' => 2, 'annual_cost' => 90000, 'start_period' => 'Q1'));
$pos2 = epc_bplan_position_add($db, $p1, array('title' => 'Analyst', 'headcount' => 1, 'annual_cost' => 70000));
check('positions added', $pos1 > 0 && $pos2 > 0 && count(epc_bplan_positions($db, $p1)) === 2);
check('position title required', (function () use ($db, $p1) { try { epc_bplan_position_add($db, $p1, array('title' => '')); return false; } catch (Throwable $e) { return true; } })());
check('position cost = headcount*annual', epc_bplan_positions_cost($db, $p1) === 250000.0);

section('Staged workflow');
check('advance to review', epc_bplan_advance_stage($db, $p1) === 'review');
check('can still edit lines in review', epc_bplan_line_add($db, $p1, array('account' => '6100', 'scenario' => 'base', 'period' => 'Q3', 'amount' => 3000)) > 0);
check('advance to approved', epc_bplan_advance_stage($db, $p1) === 'approved');
check('cannot edit lines once approved', (function () use ($db, $p1) { try { epc_bplan_line_add($db, $p1, array('amount' => 1)); return false; } catch (Throwable $e) { return true; } })());
check('cannot edit positions once approved', (function () use ($db, $p1) { try { epc_bplan_position_add($db, $p1, array('title' => 'X')); return false; } catch (Throwable $e) { return true; } })());

section('Publish freezes total');
$baseTotal = epc_bplan_total($db, $p1); // 40000
$posCost = epc_bplan_positions_cost($db, $p1); // 250000
check('advance to published', epc_bplan_advance_stage($db, $p1) === 'published');
check('published total = lines + positions', (float) epc_bplan_get($db, $p1)['published_total'] === ($baseTotal + $posCost));
check('cannot advance past published', (function () use ($db, $p1) { try { epc_bplan_advance_stage($db, $p1); return false; } catch (Throwable $e) { return true; } })());

section('Publish guards + summary');
$p2 = epc_bplan_save($db, array('company_id' => $CO, 'name' => 'FY26 Capex'));
check('cannot publish a draft directly', (function () use ($db, $p2) { try { epc_bplan_publish($db, $p2); return false; } catch (Throwable $e) { return true; } })());
$sum = epc_bplan_summary($db, $CO);
check('summary plans=2', (int) $sum['plans'] === 2);
check('summary published=1', (int) $sum['published'] === 1);
check('summary draft=1', (int) $sum['draft'] === 1);
check('summary published_total matches', (float) $sum['published_total'] === ($baseTotal + $posCost));
check('list filter by stage', count(epc_bplan_list($db, $CO, 'published')) === 1);
check('other company empty summary', (int) epc_bplan_summary($db, 999)['plans'] === 0);

echo "\n========================================\n";
echo "BUDGET PLANNING TESTS: $pass_count passed, $fail_count failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
