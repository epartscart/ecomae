<?php
/**
 * CLI tests for Cash & treasury depth: cash flow forecast (projection, running
 * balance, min balance) + bank instrument lifecycle (LC/BG/SBLC state machine,
 * events, exposure), scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_cash_treasury_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_cash_treasury.php';

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

foreach (array('epc_cft_instr_event', 'epc_cft_instrument', 'epc_cft_line', 'epc_cft_forecast') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_cft_ensure_schema($db);

$CO = 1;

section('Cash flow forecast');
$f1 = epc_cft_forecast_save($db, array('company_id' => $CO, 'name' => 'Q1 cash', 'opening_balance' => 10000, 'currency' => 'AED'));
check('forecast created', $f1 > 0 && epc_cft_forecast_get($db, $f1)['name'] === 'Q1 cash');
check('forecast name required', (function () use ($db, $CO) { try { epc_cft_forecast_save($db, array('company_id' => $CO, 'name' => '')); return false; } catch (Throwable $e) { return true; } })());
check('forecasts scoped', count(epc_cft_forecasts($db, $CO)) === 1 && count(epc_cft_forecasts($db, 999)) === 0);

epc_cft_line_add($db, $f1, array('due_date' => '2026-01-10', 'direction' => 'in', 'amount' => 5000, 'category' => 'AR', 'source' => 'C-1'));
epc_cft_line_add($db, $f1, array('due_date' => '2026-01-05', 'direction' => 'out', 'amount' => 8000, 'category' => 'AP', 'source' => 'V-1'));
epc_cft_line_add($db, $f1, array('due_date' => '2026-01-20', 'direction' => 'out', 'amount' => 12000, 'category' => 'Payroll'));
check('lines added', count(epc_cft_lines($db, $f1)) === 3);
check('direction validated', (function () use ($db, $f1) { try { epc_cft_line_add($db, $f1, array('direction' => 'sideways', 'amount' => 1)); return false; } catch (Throwable $e) { return true; } })());

section('Projection (running balance)');
$proj = epc_cft_projection($db, $f1);
check('opening = 10000', $proj['opening'] === 10000.0);
check('total in = 5000', $proj['total_in'] === 5000.0);
check('total out = 20000', $proj['total_out'] === 20000.0);
check('closing = -5000', $proj['closing'] === -5000.0);
// ordered by date: 01-05 out 8000 -> 2000; 01-10 in 5000 -> 7000; 01-20 out 12000 -> -5000
check('rows ordered by date', $proj['rows'][0]['due_date'] === '2026-01-05');
check('running balance after first = 2000', $proj['rows'][0]['running_balance'] === 2000.0);
check('min balance = -5000', $proj['min_balance'] === -5000.0);

section('Bank instruments — create + lifecycle');
$i1 = epc_cft_instrument_save($db, array('company_id' => $CO, 'type' => 'lc', 'beneficiary' => 'Supplier A', 'bank' => 'ENBD', 'amount' => 100000, 'currency' => 'AED', 'expiry_date' => '2026-06-30'));
check('instrument created as draft', epc_cft_instrument_get($db, $i1)['status'] === 'draft');
check('auto ref assigned', epc_cft_instrument_get($db, $i1)['ref'] !== '');
check('type validated', (function () use ($db, $CO) { try { epc_cft_instrument_save($db, array('company_id' => $CO, 'type' => 'xx')); return false; } catch (Throwable $e) { return true; } })());
check('create logged an event', count(epc_cft_instr_events($db, $i1)) === 1);

check('cannot skip draft->utilized', (function () use ($db, $i1) { try { epc_cft_instrument_set_status($db, $i1, 'utilized'); return false; } catch (Throwable $e) { return true; } })());
epc_cft_instrument_set_status($db, $i1, 'issued', 'Issued by bank');
check('draft->issued ok', epc_cft_instrument_get($db, $i1)['status'] === 'issued');
epc_cft_instrument_set_status($db, $i1, 'amended', 'Amount increased', 120000);
check('issued->amended ok', epc_cft_instrument_get($db, $i1)['status'] === 'amended');
epc_cft_instrument_set_status($db, $i1, 'utilized', 'Drawn down');
check('amended->utilized ok', epc_cft_instrument_get($db, $i1)['status'] === 'utilized');
epc_cft_instrument_set_status($db, $i1, 'closed');
check('utilized->closed ok', epc_cft_instrument_get($db, $i1)['status'] === 'closed');
check('closed is terminal', (function () use ($db, $i1) { try { epc_cft_instrument_set_status($db, $i1, 'issued'); return false; } catch (Throwable $e) { return true; } })());
check('lifecycle fully logged', count(epc_cft_instr_events($db, $i1)) === 5);

section('Exposure + summary + scope');
$i2 = epc_cft_instrument_save($db, array('company_id' => $CO, 'type' => 'bg', 'amount' => 50000));
epc_cft_instrument_set_status($db, $i2, 'issued');
$i3 = epc_cft_instrument_save($db, array('company_id' => $CO, 'type' => 'sblc', 'amount' => 30000));
// i3 left as draft -> not exposure; i1 closed -> not exposure; i2 issued -> exposure
$sum = epc_cft_instrument_summary($db, $CO);
check('summary count=3', (int) $sum['count'] === 3);
check('summary issued=1', (int) $sum['issued'] === 1);
check('summary closed=1', (int) $sum['closed'] === 1);
check('exposure = open only (50000)', $sum['exposure'] === 50000.0);
check('instruments filter by status', count(epc_cft_instruments($db, $CO, 'issued')) === 1);
check('other company empty exposure', epc_cft_instrument_summary($db, 999)['exposure'] === 0.0);

echo "\n========================================\n";
echo "CASH & TREASURY TESTS: $pass_count passed, $fail_count failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
