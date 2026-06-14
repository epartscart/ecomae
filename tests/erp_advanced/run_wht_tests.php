<?php
/**
 * CLI tests for withholding tax: codes, calc, record (computed withheld),
 * certificate issue, settle, summary, scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_wht_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_withholding.php';

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

foreach (array('epc_wht_txn', 'epc_wht_code') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_wht_ensure_schema($db);

$CO = 1;

section('Codes');
$c5 = epc_wht_code_save($db, array('company_id' => $CO, 'code' => 'WHT5', 'name' => 'Services 5%', 'rate' => 5, 'account' => '2300'));
$c10 = epc_wht_code_save($db, array('company_id' => $CO, 'code' => 'WHT10', 'name' => 'Royalty 10%', 'rate' => 10));
check('two codes created', $c5 > 0 && $c10 > 0 && count(epc_wht_codes($db, $CO)) === 2);
check('code+name required', (function () use ($db, $CO) { try { epc_wht_code_save($db, array('company_id' => $CO, 'code' => '', 'name' => '')); return false; } catch (Throwable $e) { return true; } })());
check('rate bounds enforced', (function () use ($db, $CO) { try { epc_wht_code_save($db, array('company_id' => $CO, 'code' => 'X', 'name' => 'X', 'rate' => 250)); return false; } catch (Throwable $e) { return true; } })());
check('codes scoped', count(epc_wht_codes($db, 999)) === 0);

section('Calc + record');
check('calc 5% of 10000 = 500', epc_wht_calc($db, $c5, 10000) === 500.0);
check('calc 10% of 2500 = 250', epc_wht_calc($db, $c10, 2500) === 250.0);
$t1 = epc_wht_record($db, array('company_id' => $CO, 'code_id' => $c5, 'vendor' => 'Consultant A', 'doc_ref' => 'INV-1', 'base_amount' => 10000));
check('txn records computed wht 500', (float) epc_wht_txn_get($db, $t1)['wht_amount'] === 500.0);
check('txn stores rate snapshot', (float) epc_wht_txn_get($db, $t1)['rate'] === 5.0);
check('txn starts accrued', epc_wht_txn_get($db, $t1)['status'] === 'accrued');
check('base must be positive', (function () use ($db, $CO, $c5) { try { epc_wht_record($db, array('company_id' => $CO, 'code_id' => $c5, 'base_amount' => 0)); return false; } catch (Throwable $e) { return true; } })());
check('unknown code rejected', (function () use ($db, $CO) { try { epc_wht_record($db, array('company_id' => $CO, 'code_id' => 9999, 'base_amount' => 100)); return false; } catch (Throwable $e) { return true; } })());

section('Certificate + settle');
$cert = epc_wht_certificate_issue($db, $t1);
check('certificate auto-numbered', strpos($cert, 'WHT-') === 0 && epc_wht_txn_get($db, $t1)['certificate_no'] === $cert);
check('cannot re-issue certificate', (function () use ($db, $t1) { try { epc_wht_certificate_issue($db, $t1); return false; } catch (Throwable $e) { return true; } })());
epc_wht_settle($db, $t1);
check('txn settled', epc_wht_txn_get($db, $t1)['status'] === 'settled');
check('cannot re-settle', (function () use ($db, $t1) { try { epc_wht_settle($db, $t1); return false; } catch (Throwable $e) { return true; } })());

section('Summary + scope');
$t2 = epc_wht_record($db, array('company_id' => $CO, 'code_id' => $c10, 'vendor' => 'Licensor', 'base_amount' => 2500));
$sum = epc_wht_summary($db, $CO);
check('summary codes=2', (int) $sum['codes'] === 2);
check('summary txns=2', (int) $sum['txns'] === 2);
check('summary total withheld = 750', $sum['total_withheld'] === 750.0);
check('summary settled = 500', $sum['settled'] === 500.0);
check('summary accrued = 250', $sum['accrued'] === 250.0);
check('txns filter by status', count(epc_wht_txns($db, $CO, 'settled')) === 1);
check('other company empty summary', (int) epc_wht_summary($db, 999)['txns'] === 0);

echo "\n========================================\n";
echo "WITHHOLDING TAX TESTS: $pass_count passed, $fail_count failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
