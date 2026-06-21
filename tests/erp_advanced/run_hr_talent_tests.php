<?php
/**
 * CLI tests for HR depth: recruitment (job requisitions, applicants, pipeline,
 * headcount fill) + performance (reviews, weighted goals, finalize), scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_hr_talent_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_hr_talent.php';

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

foreach (array('epc_hrt_goal', 'epc_hrt_review', 'epc_hrt_applicant', 'epc_hrt_job') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_hrt_ensure_schema($db);

$CO = 1;

section('Recruitment — job requisitions');
$j1 = epc_hrt_job_save($db, array('company_id' => $CO, 'title' => 'Accountant', 'department' => 'Finance', 'headcount' => 2, 'hiring_manager' => 'CFO'));
check('job created as open', epc_hrt_job_get($db, $j1)['status'] === 'open');
check('job title required', (function () use ($db, $CO) { try { epc_hrt_job_save($db, array('company_id' => $CO, 'title' => '')); return false; } catch (Throwable $e) { return true; } })());
check('jobs scoped', count(epc_hrt_jobs($db, $CO)) === 1 && count(epc_hrt_jobs($db, 999)) === 0);

section('Applicant pipeline');
$a1 = epc_hrt_applicant_add($db, $j1, array('name' => 'Sara', 'email' => 's@x.com', 'rating' => 4));
$a2 = epc_hrt_applicant_add($db, $j1, array('name' => 'Omar'));
$a3 = epc_hrt_applicant_add($db, $j1, array('name' => 'Lina'));
check('three applicants added', count(epc_hrt_applicants($db, $j1)) === 3);
check('applicant starts at applied', epc_hrt_applicant_get($db, $a1)['stage'] === 'applied');
check('applicant name required', (function () use ($db, $j1) { try { epc_hrt_applicant_add($db, $j1, array('name' => '')); return false; } catch (Throwable $e) { return true; } })());
epc_hrt_applicant_set_stage($db, $a1, 'screening');
epc_hrt_applicant_set_stage($db, $a1, 'interview');
epc_hrt_applicant_set_stage($db, $a1, 'offer');
check('applicant advanced through pipeline', epc_hrt_applicant_get($db, $a1)['stage'] === 'offer');
check('invalid stage rejected', (function () use ($db, $a1) { try { epc_hrt_applicant_set_stage($db, $a1, 'bogus'); return false; } catch (Throwable $e) { return true; } })());
epc_hrt_applicant_set_stage($db, $a2, 'rejected');
check('applicant can be rejected', epc_hrt_applicant_get($db, $a2)['stage'] === 'rejected');

section('Hiring fills headcount');
epc_hrt_applicant_set_stage($db, $a1, 'hired');
check('job still open after 1 of 2 hires', epc_hrt_job_get($db, $j1)['status'] === 'open' && (int) epc_hrt_job_get($db, $j1)['hired'] === 1);
check('re-setting same applicant hired does not double-count', (function () use ($db, $a1, $j1) { epc_hrt_applicant_set_stage($db, $a1, 'hired'); return (int) epc_hrt_job_get($db, $j1)['hired'] === 1; })());
epc_hrt_applicant_set_stage($db, $a3, 'hired');
check('job auto-filled when headcount reached', epc_hrt_job_get($db, $j1)['status'] === 'filled' && (int) epc_hrt_job_get($db, $j1)['hired'] === 2);
check('jobs filter by status', count(epc_hrt_jobs($db, $CO, 'filled')) === 1);

section('Performance reviews + weighted goals');
$r1 = epc_hrt_review_save($db, array('company_id' => $CO, 'employee_id' => 10, 'employee_name' => 'Sara', 'period' => 'H1-2026', 'reviewer' => 'Manager'));
check('review created as draft', epc_hrt_review_get($db, $r1)['status'] === 'draft');
check('review needs name or id', (function () use ($db, $CO) { try { epc_hrt_review_save($db, array('company_id' => $CO, 'employee_name' => '', 'employee_id' => 0)); return false; } catch (Throwable $e) { return true; } })());
epc_hrt_goal_add($db, $r1, array('title' => 'Close month-end on time', 'weight' => 3, 'rating' => 5));
epc_hrt_goal_add($db, $r1, array('title' => 'Reduce errors', 'weight' => 1, 'rating' => 3));
check('adding goal moves review to in_progress', epc_hrt_review_get($db, $r1)['status'] === 'in_progress');
check('two goals', count(epc_hrt_goals($db, $r1)) === 2);
check('goal rating bounds enforced', (function () use ($db, $r1) { try { epc_hrt_goal_add($db, $r1, array('title' => 'X', 'rating' => 9)); return false; } catch (Throwable $e) { return true; } })());
check('weighted rating = (3*5+1*3)/4 = 4.5', epc_hrt_review_weighted_rating($db, $r1) === 4.5);

section('Finalize review');
$overall = epc_hrt_review_finalize($db, $r1);
check('finalize returns weighted overall 4.5', $overall === 4.5);
check('review completed + rating stored', epc_hrt_review_get($db, $r1)['status'] === 'completed' && (float) epc_hrt_review_get($db, $r1)['overall_rating'] === 4.5);
check('cannot add goal after completed', (function () use ($db, $r1) { try { epc_hrt_goal_add($db, $r1, array('title' => 'X', 'rating' => 1)); return false; } catch (Throwable $e) { return true; } })());
check('cannot re-finalize', (function () use ($db, $r1) { try { epc_hrt_review_finalize($db, $r1); return false; } catch (Throwable $e) { return true; } })());
$r2 = epc_hrt_review_save($db, array('company_id' => $CO, 'employee_name' => 'Omar'));
check('cannot finalize with no goals', (function () use ($db, $r2) { try { epc_hrt_review_finalize($db, $r2); return false; } catch (Throwable $e) { return true; } })());

section('Summary + scope');
$sum = epc_hrt_summary($db, $CO);
check('summary filled_jobs=1', (int) $sum['filled_jobs'] === 1);
check('summary applicants=3', (int) $sum['applicants'] === 3);
check('summary hired=2', (int) $sum['hired'] === 2);
check('summary reviews_done=1', (int) $sum['reviews_done'] === 1);
check('summary reviews_open=1', (int) $sum['reviews_open'] === 1);
check('other company empty', (int) epc_hrt_summary($db, 999)['applicants'] === 0);

echo "\n========================================\n";
echo "HR TALENT TESTS: $pass_count passed, $fail_count failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
