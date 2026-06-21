<?php
/**
 * CLI tests for electronic reporting: formats, field map, render (csv/xml/json),
 * generate + run log, scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_elec_reporting_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_elec_reporting.php';

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

foreach (array('epc_er_run', 'epc_er_field', 'epc_er_format') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_er_ensure_schema($db);

$CO = 1;
$rows = array(
    array('vno' => 'V-001', 'name' => 'Alpha, LLC', 'amt' => '1000.00'),
    array('vno' => 'V-002', 'name' => 'Beta', 'amt' => '2500.50'),
);

section('Formats + fields');
$fCsv = epc_er_format_save($db, array('company_id' => $CO, 'code' => 'VENDLIST', 'name' => 'Vendor list', 'output_type' => 'csv'));
check('csv format created', $fCsv > 0 && epc_er_format_get($db, $fCsv)['output_type'] === 'csv');
check('code+name required', (function () use ($db, $CO) { try { epc_er_format_save($db, array('company_id' => $CO, 'code' => '', 'name' => '')); return false; } catch (Throwable $e) { return true; } })());
check('output type validated', (function () use ($db, $CO) { try { epc_er_format_save($db, array('company_id' => $CO, 'code' => 'X', 'name' => 'X', 'output_type' => 'pdf')); return false; } catch (Throwable $e) { return true; } })());
epc_er_field_add($db, $fCsv, array('label' => 'Vendor No', 'source_key' => 'vno'));
epc_er_field_add($db, $fCsv, array('label' => 'Vendor Name', 'source_key' => 'name'));
epc_er_field_add($db, $fCsv, array('label' => 'Amount', 'source_key' => 'amt'));
check('three fields, auto-ordinal', count(epc_er_fields($db, $fCsv)) === 3 && (int) epc_er_fields($db, $fCsv)[2]['ordinal'] === 3);
check('field needs label+key', (function () use ($db, $fCsv) { try { epc_er_field_add($db, $fCsv, array('label' => '', 'source_key' => '')); return false; } catch (Throwable $e) { return true; } })());

section('CSV render');
$csv = epc_er_render($db, $fCsv, $rows);
$lines = explode("\n", $csv);
check('csv header row', $lines[0] === 'Vendor No,Vendor Name,Amount');
check('csv quotes value with comma', $lines[1] === 'V-001,"Alpha, LLC",1000.00');
check('csv second row', $lines[2] === 'V-002,Beta,2500.50');
check('render needs fields', (function () use ($db, $CO, $rows) { $f = epc_er_format_save($db, array('company_id' => $CO, 'code' => 'EMPTY', 'name' => 'Empty')); try { epc_er_render($db, $f, $rows); return false; } catch (Throwable $e) { return true; } })());

section('JSON render');
$fJson = epc_er_format_save($db, array('company_id' => $CO, 'code' => 'VJSON', 'name' => 'Vendor json', 'output_type' => 'json', 'root_element' => 'vendors'));
epc_er_field_add($db, $fJson, array('label' => 'No', 'source_key' => 'vno'));
epc_er_field_add($db, $fJson, array('label' => 'Amt', 'source_key' => 'amt'));
$json = json_decode(epc_er_render($db, $fJson, $rows), true);
check('json wraps in root element', isset($json['vendors']) && count($json['vendors']) === 2);
check('json maps labels', $json['vendors'][0]['No'] === 'V-001' && $json['vendors'][1]['Amt'] === '2500.50');

section('XML render');
$fXml = epc_er_format_save($db, array('company_id' => $CO, 'code' => 'VXML', 'name' => 'Vendor xml', 'output_type' => 'xml', 'root_element' => 'Vendors', 'row_element' => 'Vendor'));
epc_er_field_add($db, $fXml, array('label' => 'VNo', 'source_key' => 'vno'));
$xml = epc_er_render($db, $fXml, $rows);
check('xml has root + row elements', strpos($xml, '<Vendors>') !== false && substr_count($xml, '<Vendor>') === 2);
check('xml carries values', strpos($xml, '<VNo>V-001</VNo>') !== false);

section('Generate + run log');
$run = epc_er_generate($db, $fCsv, $CO, $rows);
check('run logged', $run > 0 && (int) epc_er_run_get($db, $run)['row_count'] === 2);
check('run has preview', strlen((string) epc_er_run_get($db, $run)['preview']) > 0);
check('runs listed', count(epc_er_runs($db, $CO)) === 1 && count(epc_er_runs($db, $CO, $fCsv)) === 1);
check('formats scoped', count(epc_er_formats($db, $CO)) === 4 && count(epc_er_formats($db, 999)) === 0);
check('other company no runs', count(epc_er_runs($db, 999)) === 0);

echo "\n========================================\n";
echo "ELECTRONIC REPORTING TESTS: $pass_count passed, $fail_count failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
