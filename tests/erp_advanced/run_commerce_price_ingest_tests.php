<?php
/**
 * Commerce S/P/L price ingest unit tests (no live DB required).
 *
 *   php tests/erp_advanced/run_commerce_price_ingest_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

$root = dirname(__DIR__, 2);
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/shop/docpart/epc_commerce_price_ingest.php';

$pass = 0;
$fail = 0;
function check(string $label, bool $cond): void
{
	global $pass, $fail;
	if ($cond) {
		$pass++;
		echo "  PASS  $label\n";
	} else {
		$fail++;
		echo "  FAIL  $label\n";
	}
}

echo "== Naming ==\n";
check('sales name', epc_commerce_list_name('sales', 'MAIN') === 'MAIN-S');
check('inventory name', epc_commerce_list_name('inventory', 'MAIN') === 'MAIN-L');
check('purchase supplier', epc_commerce_list_name('purchase', 'EPC', 'Acme Parts') === 'Acme-Parts.P');
check('strips existing -S', epc_commerce_list_name('sales', 'MAIN-S') === 'MAIN-S');

echo "\n== Header mapping ==\n";
$map = epc_commerce_map_headers(array('Brand', 'Part Number', 'Description', 'Qty', 'Sales Price'));
check('brand→manufacturer', $map['manufacturer'] === 0);
check('part number→article', $map['article'] === 1);
check('description→name', $map['name'] === 2);
check('qty→exist', $map['exist'] === 3);
check('sales price→price', $map['price'] === 4);

echo "\n== Sales aggregation (highest price) ==\n";
$salesRows = array(
	array('manufacturer' => 'BOSCH', 'article' => 'OC47', 'article_show' => 'OC47', 'name' => 'Oil filter', 'exist' => 2, 'price' => 10.0, 'cost' => 0, 'supplier' => ''),
	array('manufacturer' => 'BOSCH', 'article' => 'OC47', 'article_show' => 'OC47', 'name' => 'Oil filter HQ', 'exist' => 3, 'price' => 15.5, 'cost' => 0, 'supplier' => ''),
	array('manufacturer' => 'MANN', 'article' => 'W71945', 'article_show' => 'W71945', 'name' => 'Filter', 'exist' => 1, 'price' => 20.0, 'cost' => 0, 'supplier' => ''),
);
$agg = epc_commerce_aggregate_rows('sales', $salesRows, 'EPC', 0);
check('one sales list', isset($agg['EPC-S']) && count($agg['EPC-S']) === 2);
$byArt = array();
foreach ($agg['EPC-S'] as $r) {
	$byArt[$r['article']] = $r;
}
check('highest sales price kept', isset($byArt['OC47']) && abs((float) $byArt['OC47']['price'] - 15.5) < 0.001);
check('qty summed', isset($byArt['OC47']) && (int) $byArt['OC47']['exist'] === 5);
check('name from highest price row', isset($byArt['OC47']) && $byArt['OC47']['name'] === 'Oil filter HQ');

echo "\n== Purchase aggregation (margin + per supplier) ==\n";
$purchRows = array(
	array('manufacturer' => 'BOSCH', 'article' => 'OC47', 'article_show' => 'OC47', 'name' => 'Oil', 'exist' => 5, 'price' => 0, 'cost' => 10.0, 'supplier' => 'ACME'),
	array('manufacturer' => 'BOSCH', 'article' => 'OC47', 'article_show' => 'OC47', 'name' => 'Oil', 'exist' => 2, 'price' => 0, 'cost' => 12.0, 'supplier' => 'ACME'),
	array('manufacturer' => 'BOSCH', 'article' => 'OC47', 'article_show' => 'OC47', 'name' => 'Oil', 'exist' => 1, 'price' => 0, 'cost' => 9.0, 'supplier' => 'BETA'),
);
$pAgg = epc_commerce_aggregate_rows('purchase', $purchRows, 'EPC', 20.0);
check('two supplier lists', isset($pAgg['ACME.P']) && isset($pAgg['BETA.P']));
$acme = $pAgg['ACME.P'][0];
check('lowest cost kept for ACME', abs((float) $acme['cost'] - 10.0) < 0.001);
check('margin 20% applied', abs((float) $acme['price'] - 12.0) < 0.001);

echo "\n== Inventory aggregation ==\n";
$invRows = array(
	array('manufacturer' => 'MANN', 'article' => 'W71945', 'article_show' => 'W71945', 'name' => 'F', 'exist' => 4, 'price' => 0, 'cost' => 8.0, 'supplier' => ''),
	array('manufacturer' => 'MANN', 'article' => 'W71945', 'article_show' => 'W71945', 'name' => 'F', 'exist' => 6, 'price' => 0, 'cost' => 8.0, 'supplier' => ''),
);
$iAgg = epc_commerce_aggregate_rows('inventory', $invRows, 'WH', 25.0);
check('inventory list WH-L', isset($iAgg['WH-L']));
check('qty summed inventory', (int) $iAgg['WH-L'][0]['exist'] === 10);
check('margin 25%', abs((float) $iAgg['WH-L'][0]['price'] - 10.0) < 0.001);

echo "\n== CSV round-trip read ==\n";
$tmp = sys_get_temp_dir() . '/epc_commerce_test_' . getmypid() . '.csv';
file_put_contents($tmp, "Brand,Article,Name,Qty,Price\nBOSCH,OC47,Oil,2,11.00\nBOSCH,OC47,Oil,1,14.00\n");
$read = epc_commerce_read_source_rows($tmp, 'sales');
check('read ok', !empty($read['ok']));
check('two source rows', count($read['rows']) === 2);
@unlink($tmp);

echo "\n== Files present ==\n";
check('API endpoint', is_file($root . '/epc-upload-commerce-prices.php'));
check('CP page', is_file($root . '/cp/content/shop/prices_upload/commerce_data_upload.php'));
check('CP route wrapper', is_file($root . '/cp/content/shop/prices_upload/commerce_data_page.php'));
check('AJAX', is_file($root . '/cp/content/shop/prices_upload/ajax_epc_commerce_ingest.php'));
check('setup script', is_file($root . '/epc-commerce-prices-setup.php'));
$mgr = (string) file_get_contents($root . '/cp/content/shop/prices_upload/prices_manager.php');
check('manager links to commerce', strpos($mgr, 'shop/prices/commerce') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
