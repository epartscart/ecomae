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

// Default mode is combine (one file, per-row Data type).
$combineRead = epc_multivendor_read_source_rows($tmp);
check('combine default parses sample', !empty($combineRead['ok']));
check('combine mode flag', ($combineRead['mode'] ?? '') === 'combine');
check('combine sample has 6 source rows', count($combineRead['rows'] ?? []) === 6);
check('resolve empty → combine', epc_multivendor_resolve_data_type_mode('') === 'combine');
check('resolve combine alias', epc_multivendor_resolve_data_type_mode('mixed') === 'combine');
check('is_combine_mode', epc_multivendor_is_combine_mode('one file'));

$read = epc_multivendor_read_source_rows($tmp, 'inventory');
check('sample CSV parses', !empty($read['ok']));
check('sample has 6 source rows', count($read['rows'] ?? []) === 6);
check('maps vendor_full', ($read['map']['vendor_full'] ?? -1) >= 0);
check('maps vendor_short', ($read['map']['vendor_short'] ?? -1) >= 0);
check('maps data_type', ($read['map']['data_type'] ?? -1) >= 0);

$groups = epc_multivendor_group_by_vendor($read['rows'] ?? []);
// inventory S-UAE Trading LLC, inventory Gulf Parts (same code S-UAE), inventory R-UAE, sales S-UAE Trading LLC
check('groups into 4 vendor-name+code/type buckets', count($groups) === 4);

$salesSuae = null;
$invSuae = null;
$sameCodeDifferentName = 0;
foreach ($groups as $g) {
	if ($g['vendor_short'] === 'S-UAE' && ($g['data_type'] ?? '') === 'inventory') {
		$sameCodeDifferentName++;
		if ($g['vendor_full'] === 'S-UAE Trading LLC') {
			$invSuae = $g;
			check('S-UAE Trading inventory full name kept', $g['vendor_full'] === 'S-UAE Trading LLC');
			check('S-UAE Trading inventory has 1 product (TOYOTA)', count($g['products']) === 1);
		}
		if ($g['vendor_full'] === 'Gulf Parts Trading') {
			check('same code different name stays separate', true);
			check('Gulf Parts inventory has BOSCH', count($g['products']) === 1);
		}
	}
	if ($g['vendor_short'] === 'R-UAE' && ($g['data_type'] ?? '') === 'inventory') {
		check('R-UAE inventory full name kept', $g['vendor_full'] === 'R-UAE Spare Parts FZE');
	}
	if ($g['vendor_short'] === 'S-UAE' && ($g['data_type'] ?? '') === 'sales') {
		$salesSuae = $g;
	}
}
check('same code yields 2 inventory buckets (different names)', $sameCodeDifferentName === 2);
check('vendor_key uses name+code', epc_multivendor_vendor_key('Gulf Parts Trading', 'S-UAE')
	!== epc_multivendor_vendor_key('S-UAE Trading LLC', 'S-UAE'));
check('list base includes name when code shared', epc_multivendor_list_base_name('S-UAE', 'Gulf Parts Trading') === 'S-UAE · Gulf Parts Trading');
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
	// Sample: 12 + 5 + 2 = 19 total QTY on both min and max rows
	$minQty = null;
	$maxQty = null;
	foreach ($salesSuae['products'] as $p) {
		if (($p['article'] ?? '') !== '0671007450') {
			continue;
		}
		if (($p['epc_price_tier'] ?? '') === 'min') {
			$minQty = (int) ($p['exist'] ?? 0);
		}
		if (($p['epc_price_tier'] ?? '') === 'max') {
			$maxQty = (int) ($p['exist'] ?? 0);
		}
	}
	check('sales min qty is total 19', $minQty === 19);
	check('sales max qty is total 19', $maxQty === 19);
	check('sales min+max share same total qty', $minQty === $maxQty);
	$minStorageOk = false;
	foreach ($salesSuae['products'] as $p) {
		if (($p['epc_price_tier'] ?? '') === 'min' && ($p['storage'] ?? '') === 'epc_mv_min') {
			$minStorageOk = true;
		}
	}
	check('sales min storage marker', $minStorageOk);

	// Direct collapse unit: mid prices dropped, qty still total of all lines.
	$collapse = epc_multivendor_collapse_product_candidates(array(
		array('manufacturer' => 'X', 'article' => '1', 'exist' => 4, 'price' => 10, 'name' => 'A'),
		array('manufacturer' => 'X', 'article' => '1', 'exist' => 7, 'price' => 15, 'name' => 'A'),
		array('manufacturer' => 'X', 'article' => '1', 'exist' => 3, 'price' => 40, 'name' => 'A'),
	), 'sales');
	check('collapse sales returns 2 rows', count($collapse) === 2);
	check('collapse sales total qty on min', (int) ($collapse[0]['exist'] ?? 0) === 14);
	check('collapse sales total qty on max', (int) ($collapse[1]['exist'] ?? 0) === 14);
	check('collapse sales min price 10', abs((float) ($collapse[0]['price'] ?? 0) - 10.0) < 0.001);
	check('collapse sales max price 40', abs((float) ($collapse[1]['price'] ?? 0) - 40.0) < 0.001);
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

// Combine requires Data type column.
$noType = sys_get_temp_dir() . '/epc_mv_notype_' . getmypid() . '.csv';
file_put_contents(
	$noType,
	"Brand,Article,Qty,Price,Vendor full name,Vendor short\n"
	. "TOYOTA,AAA,1,10,V Full,V1\n"
);
$noTypeRead = epc_multivendor_read_source_rows($noType, 'combine');
check('combine rejects file without Data type column', empty($noTypeRead['ok']));
check('combine missing-type message mentions Data type', strpos((string) ($noTypeRead['message'] ?? ''), 'Data type') !== false);

// Same file with inventory-only override still works without Data type column.
$noTypeInv = epc_multivendor_read_source_rows($noType, 'inventory');
check('inventory override works without Data type column', !empty($noTypeInv['ok']));
check('inventory override has 1 row', count($noTypeInv['rows'] ?? []) === 1);

// Same vendor CODE, different NAMES must not share sales min/max buckets.
$dupCode = sys_get_temp_dir() . '/epc_mv_dup_' . getmypid() . '.csv';
file_put_contents(
	$dupCode,
	"Brand,Article,Qty,Price,Vendor full name,Vendor short,Data type\n"
	. "DENSO,X1,1,10,Alpha Trading,CODE1,sales\n"
	. "DENSO,X1,1,50,Alpha Trading,CODE1,sales\n"
	. "DENSO,X1,1,12,Beta Trading,CODE1,sales\n"
	. "DENSO,X1,1,40,Beta Trading,CODE1,sales\n"
);
$dupRead = epc_multivendor_read_source_rows($dupCode, 'sales');
$dupGroups = epc_multivendor_group_by_vendor($dupRead['rows'] ?? []);
check('same code different names = 2 sales groups', count($dupGroups) === 2);
foreach ($dupGroups as $g) {
	$prices = array();
	foreach ($g['products'] as $p) {
		$prices[] = (float) $p['price'];
	}
	sort($prices);
	if (($g['vendor_full'] ?? '') === 'Alpha Trading') {
		check('Alpha min/max 10 and 50', count($prices) === 2 && abs($prices[0] - 10) < 0.01 && abs($prices[1] - 50) < 0.01);
	}
	if (($g['vendor_full'] ?? '') === 'Beta Trading') {
		check('Beta min/max 12 and 40', count($prices) === 2 && abs($prices[0] - 12) < 0.01 && abs($prices[1] - 40) < 0.01);
	}
}

@unlink($tmp);
@unlink($invCsv);
@unlink($bad);
@unlink($noType);
@unlink($dupCode);

echo $failed === 0 ? "ALL PASSED\n" : "FAILED {$failed}\n";
exit($failed === 0 ? 0 : 1);
