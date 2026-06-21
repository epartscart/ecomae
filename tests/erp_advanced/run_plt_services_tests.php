<?php
/**
 * CLI tests for Platform / cross-cutting services (batch jobs + feature mgmt):
 * pure recurrence (next-run, is-due), batch job save (with next_run), run
 * history + recurrence roll, feature flags (save/toggle/enabled), summary and
 * multi-company scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_plt_services_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_platform.php';

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

foreach (array('epc_plt_batch_run', 'epc_plt_batch_job', 'epc_plt_feature') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_plt_ensure_schema($db);

$CO = 1;

section('Pure: recurrence');
check('next run = from + minutes', epc_plt_next_run(1000, 60) === 1000 + 3600);
check('one-time (0) -> no next run', epc_plt_next_run(1000, 0) === 0);
check('negative recurrence -> 0', epc_plt_next_run(1000, -5) === 0);
check('is due when next <= now', epc_plt_is_due(500, 600) === true);
check('not due when next > now', epc_plt_is_due(700, 600) === false);
check('not due when no next run', epc_plt_is_due(0, 600) === false);

section('Batch jobs');
$j1 = epc_plt_batch_job_save($db, $CO, array('code' => 'AGEING', 'name' => 'Nightly ageing', 'recurrence_min' => 1440, 'active' => 1));
check('job saved with id', $j1 > 0);
$jobs = epc_plt_batch_jobs($db, $CO);
check('active recurring job has next_run', (int) $jobs[0]['next_run'] > 0);
check('code required', (function () use ($db, $CO) { try { epc_plt_batch_job_save($db, $CO, array('code' => '')); return false; } catch (Throwable $e) { return true; } })());
$j2 = epc_plt_batch_job_save($db, $CO, array('code' => 'ONETIME', 'name' => 'One-off', 'recurrence_min' => 0, 'active' => 1));
$jobs = epc_plt_batch_jobs($db, $CO);
$oneTime = null;
foreach ($jobs as $jj) { if ($jj['code'] === 'ONETIME') { $oneTime = $jj; } }
check('one-time job has no next_run', (int) $oneTime['next_run'] === 0);

section('Batch run history + recurrence roll');
$r1 = epc_plt_batch_run($db, $j1, 'ended', 'ok');
check('run logged with id', $r1['run_id'] > 0);
check('ended run sets a future next_run', $r1['next_run'] > time());
check('invalid status rejected', (function () use ($db, $j1) { try { epc_plt_batch_run($db, $j1, 'exploded'); return false; } catch (Throwable $e) { return true; } })());
$rErr = epc_plt_batch_run($db, $j1, 'error', 'boom');
check('error run keeps prior next_run', $rErr['next_run'] === $r1['next_run']);
check('run history = 2', count(epc_plt_batch_runs($db, $j1)) === 2);
check('job status reflects last run', epc_plt_batch_jobs($db, $CO)[0]['status'] === 'error');
check('run on missing job rejected', (function () use ($db) { try { epc_plt_batch_run($db, 999999, 'ended'); return false; } catch (Throwable $e) { return true; } })());

section('Feature management');
$f1 = epc_plt_feature_save($db, $CO, array('code' => 'NEW_GRID', 'name' => 'New grid UX', 'enabled' => 0));
check('feature saved', $f1 > 0);
check('feature disabled by default', epc_plt_feature_enabled($db, $CO, 'NEW_GRID') === false);
epc_plt_feature_toggle($db, $CO, 'NEW_GRID', true);
check('feature toggled on', epc_plt_feature_enabled($db, $CO, 'NEW_GRID') === true);
check('unknown feature -> false', epc_plt_feature_enabled($db, $CO, 'NOPE') === false);
epc_plt_feature_save($db, $CO, array('code' => 'BETA_X', 'name' => 'Beta X', 'enabled' => 1));
check('features list = 2', count(epc_plt_features($db, $CO)) === 2);
check('feature code required', (function () use ($db, $CO) { try { epc_plt_feature_save($db, $CO, array('code' => '')); return false; } catch (Throwable $e) { return true; } })());

section('Summary + multi-company');
epc_plt_batch_job_save($db, 2, array('code' => 'OTHER', 'active' => 1));
check('company 2 isolated (1 job)', epc_plt_summary($db, 2)['jobs'] === 1);
$sum = epc_plt_summary($db, $CO);
check('summary jobs = 2', $sum['jobs'] === 2);
check('summary runs = 2', $sum['runs'] === 2);
check('summary features = 2', $sum['features'] === 2);
check('summary features_on = 2', $sum['features_on'] === 2);

echo "\n========================================\n";
echo 'PLATFORM SERVICES TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
