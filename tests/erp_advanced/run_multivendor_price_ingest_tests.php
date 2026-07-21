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
$read = epc_multivendor_read_source_rows($tmp, 'inventory');
check('sample CSV parses', !empty($read['ok']));
check('sample has 5 source rows', count($read['rows'] ?? []) === 5);
check('maps vendor_full', ($read['map']['vendor_full'] ?? -1) >= 0);
check('maps vendor_short', ($read['map']['vendor_short'] ?? -1) >= 0);
check('maps data_type', ($read['map']['data_type'] ?? -1) >= 0);

$groups = epc_multivendor_group_by_vendor($read['rows'] ?? []);
// inventory S-UAE, inventory R-UAE, sales S-UAE
check('groups into 3 vendor/type buckets', count($groups) === 3);

$salesSuae = null;
$invSuae = null;
foreach ($groups as $g) {
	if ($g['vendor_short'] === 'S-UAE' && ($g['data_type'] ?? '') === 'inventory') {
		$invSuae = $g;
		check('S-UAE inventory full name kept', $g['vendor_full'] === 'S-UAE Trading LLC');
		check('S-UAE inventory has 1 product (TOYOTA)', count($g['products']) === 1);
	}
	if ($g['vendor_short'] === 'R-UAE' && ($g['data_type'] ?? '') === 'inventory') {
		check('R-UAE inventory full name kept', $g['vendor_full'] === 'R-UAE Spare Parts FZE');
	}
	if ($g['vendor_short'] === 'S-UAE' && ($g['data_type'] ?? '') === 'sales') {
		$salesSuae = $g;
	}
}
check('has S-UAE sales group', is_array($salesSuae));
if (is_array($salesSuae)) {
	check('sales keeps 2 rows (min+max) for DENSO', count($salesSuae['products']) === 2);
	$prices = array();
	foreach ($salesSuae['products'] as $p) {
		if (($p['article'] ?? '') === '0671007450') {
			$prices[] = (float) $p['price'];
		}
	}
	sort($prices);
	check('sales min price 18', isset($prices[0]) && abs($prices[0] - 18.0) < 0.001);
	check('sales max price 29.90', isset($prices[1]) && abs($prices[1] - 29.90) < 0.001);
	check('sales mid price 22.50 dropped', count($prices) === 2);
	$tiers = array();
	foreach ($salesSuae['products'] as $p) {
		if (($p['article'] ?? '') === '0671007450') {
			$tiers[(string) ($p['epc_price_tier'] ?? '')] = (float) ($p['price'] ?? 0);
		}
	}
	check('sales min tier marked', isset($tiers['min']) && abs($tiers['min'] - 18.0) < 0.001);
	check('sales max tier marked', isset($tiers['max']) && abs($tiers['max'] - 29.90) < 0.001);
	$minStorageOk = false;
	foreach ($salesSuae['products'] as $p) {
		if (($p['epc_price_tier'] ?? '') === 'min' && ($p['storage'] ?? '') === 'epc_mv_min') {
			$minStorageOk = true;
		}
	}
	check('sales min storage marker', $minStorageOk);
}

// Inventory uniqueness + qty sum
$invCsv = sys_get_temp_dir() . '/epc_mv_inv_' . getmypid() . '.csv';
file_put_contents(
	$invCsv,
	"Brand,Article,Qty,Price,Vendor full name,Vendor short,Data type\n"
	. "TOYOTA,AAA,2,10,V Full,V1,inventory\n"
	. "TOYOTA,AAA,3,12,V Full,V1,inventory\n"
);
$invRead = epc_multivendor_read_source_rows($invCsv, 'inventory');
$invGroups = epc_multivendor_group_by_vendor($invRead['rows'] ?? []);
$invProducts = array_values($invGroups)[0]['products'] ?? array();
check('inventory collapses to 1 row', count($invProducts) === 1);
check('inventory qty summed to 5', isset($invProducts[0]) && (int) $invProducts[0]['exist'] === 5);
check('inventory keeps lower price 10', isset($invProducts[0]) && abs((float) $invProducts[0]['price'] - 10.0) < 0.001);

check('normalize sales alias', epc_multivendor_normalize_data_type('Sale') === 'sales');
check('normalize purchase alias', epc_multivendor_normalize_data_type('buy') === 'purchase');
check('list suffix sales', epc_multivendor_data_type_list_suffix('sales') === ' · Sales');

$bad = sys_get_temp_dir() . '/epc_mv_bad_' . getmypid() . '.csv';
file_put_contents($bad, "Brand,Article,Price\nTOYOTA,1,10\n");
$badRead = epc_multivendor_read_source_rows($bad);
check('rejects missing vendor columns', empty($badRead['ok']));

@unlink($tmp);
@unlink($invCsv);
@unlink($bad);

echo $failed === 0 ? "ALL PASSED\n" : "FAILED {$failed}\n";
exit($failed === 0 ? 0 : 1);
