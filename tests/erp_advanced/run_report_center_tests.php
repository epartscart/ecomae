<?php
/**
 * CLI tests for the report center: registry integrity, per-module lookup,
 * generic table reader (company scoping + safety), and run() shape.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_report_center_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);
if (empty($_SERVER['DOCUMENT_ROOT'])) {
    $_SERVER['DOCUMENT_ROOT'] = dirname(__DIR__, 2);
}

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_report_center.php';
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

$CO = 1;

section('Registry integrity');
$reg = epc_rc_registry();
check('registry non-empty', count($reg) > 10);
$keys = array_column($reg, 'key');
check('report keys unique', count($keys) === count(array_unique($keys)));
$ok = true;
foreach ($reg as $r) {
    if (!isset($r['key'], $r['area'], $r['name'], $r['desc']) || !is_callable($r['run'])) {
        $ok = false;
        break;
    }
}
check('every report well-formed (key/area/name/desc/run)', $ok);

section('Per-module lookup');
check('AP module has reports', count(epc_rc_reports_for('ap')) >= 1);
check('banking module has reports', count(epc_rc_reports_for('banking')) >= 1);
check('tax module has reports', count(epc_rc_reports_for('tax')) >= 1);
check('unknown module empty', count(epc_rc_reports_for('does_not_exist')) === 0);
check('report_get found', epc_rc_report_get('ap_vendor_list') !== null);
check('report_get missing -> null', epc_rc_report_get('nope') === null);

section('Generic table reader');
check('missing table -> empty (safe)', epc_rc_table_rows($db, 'epc_no_such_table_xyz', $CO) === array());
check('bad table name rejected', epc_rc_table_rows($db, 'bad name; DROP', $CO) === array());
// seed a known company-scoped table and confirm scoping
epc_cft_ensure_schema($db);
$db->exec("DELETE FROM `epc_cft_forecast` WHERE `name`='RC probe'");
epc_cft_forecast_save($db, array('company_id' => $CO, 'name' => 'RC probe', 'opening_balance' => 1));
$mine = epc_rc_table_rows($db, 'epc_cft_forecast', $CO);
$other = epc_rc_table_rows($db, 'epc_cft_forecast', 999999);
check('reader returns company rows', count($mine) >= 1);
check('reader scopes by company', count($other) === 0);

section('run() shape');
$res = epc_rc_run($db, 'cash_forecasts_rep', $CO);
check('run returns columns+rows', isset($res['columns'], $res['rows']) && is_array($res['columns']) && is_array($res['rows']));
check('columns derived from rows', count($res['rows']) === 0 || count($res['columns']) > 0);
check('run unknown report throws', (function () use ($db, $CO) { try { epc_rc_run($db, 'ghost', $CO); return false; } catch (Throwable $e) { return true; } })());

echo "\n========================================\n";
echo "REPORT CENTER TESTS: $pass_count passed, $fail_count failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
