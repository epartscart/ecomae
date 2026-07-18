<?php
/**
 * Offline tests for multi-vendor price ingest (no DB).
 * php tests/erp_advanced/run_multivendor_price_ingest_tests.php
 */
declare(strict_types=1);

$root = dirname(__DIR__, 2);
require_once $root . '/content/shop/docpart/epc_multivendor_price_ingest.php';

$failed = 0;
function check(string $label, bool $ok): void
{
	global $failed;
	echo ($ok ? '[OK] ' : '[FAIL] ') . $label . PHP_EOL;
	if (!$ok) {
		$failed++;
	}
}

$tmp = sys_get_temp_dir() . '/epc_mv_test_' . getmypid() . '.csv';
file_put_contents($tmp, epc_multivendor_sample_csv());
$read = epc_multivendor_read_source_rows($tmp);
check('sample CSV parses', !empty($read['ok']));
check('sample has 3 product rows', count($read['rows'] ?? []) === 3);
check('maps vendor_full', ($read['map']['vendor_full'] ?? -1) >= 0);
check('maps vendor_short', ($read['map']['vendor_short'] ?? -1) >= 0);

$groups = epc_multivendor_group_by_vendor($read['rows'] ?? []);
check('groups into 2 vendors', count($groups) === 2);
$shorts = array();
foreach ($groups as $g) {
	$shorts[] = $g['vendor_short'];
	if ($g['vendor_short'] === 'S-UAE') {
		check('S-UAE full name kept', $g['vendor_full'] === 'S-UAE Trading LLC');
		check('S-UAE has 2 products', count($g['products']) === 2);
	}
	if ($g['vendor_short'] === 'R-UAE') {
		check('R-UAE full name kept', $g['vendor_full'] === 'R-UAE Spare Parts FZE');
	}
}
check('has S-UAE', in_array('S-UAE', $shorts, true));
check('has R-UAE', in_array('R-UAE', $shorts, true));

$bad = sys_get_temp_dir() . '/epc_mv_bad_' . getmypid() . '.csv';
file_put_contents($bad, "Brand,Article,Price\nTOYOTA,1,10\n");
$badRead = epc_multivendor_read_source_rows($bad);
check('rejects missing vendor columns', empty($badRead['ok']));

@unlink($tmp);
@unlink($bad);

echo $failed === 0 ? "ALL PASSED\n" : "FAILED {$failed}\n";
exit($failed === 0 ? 0 : 1);
