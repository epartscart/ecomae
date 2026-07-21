<?php
/**
 * Multivendor min-price ACL unit checks (no DB required for core helpers).
 *
 *   php tests/erp_advanced/run_multivendor_min_price_acl_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

$root = dirname(__DIR__, 2);
define('_ASTEXE_', 1);
require_once $root . '/content/shop/docpart/epc_multivendor_min_price_acl.php';
require_once $root . '/content/shop/docpart/epc_multivendor_price_ingest.php';

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

echo "== Multivendor min-price ACL ==\n";

check('min marker constant', EPC_MV_MIN_TIER === 'epc_mv_min');
check('is_min_row true', epc_mv_min_price_is_min_row('epc_mv_min'));
check('is_min_row false for max', !epc_mv_min_price_is_min_row('epc_mv_max'));
check('is_max_row true', epc_mv_min_price_is_max_row('epc_mv_max'));
check('display strips min', epc_mv_min_price_display_storage('epc_mv_min') === '');
check('display strips max', epc_mv_min_price_display_storage('epc_mv_max') === '');
check('display keeps other', epc_mv_min_price_display_storage('WH-A') === 'WH-A');
check('typed list sales', epc_mv_min_price_list_is_typed('S-UAE · Sales'));
check('typed list purchase', epc_mv_min_price_list_is_typed('S-UAE · Purchase'));
check('typed list inventory false', !epc_mv_min_price_list_is_typed('S-UAE'));

$candidates = array(
	array('manufacturer' => 'DENSO', 'article' => 'X', 'article_show' => 'X', 'name' => 'A', 'exist' => 1, 'price' => 18, 'time_to_exe' => 0, 'min_order' => 0),
	array('manufacturer' => 'DENSO', 'article' => 'X', 'article_show' => 'X', 'name' => 'B', 'exist' => 1, 'price' => 22, 'time_to_exe' => 0, 'min_order' => 0),
	array('manufacturer' => 'DENSO', 'article' => 'X', 'article_show' => 'X', 'name' => 'C', 'exist' => 1, 'price' => 30, 'time_to_exe' => 0, 'min_order' => 0),
);
$collapsed = epc_multivendor_collapse_product_candidates($candidates, 'sales');
check('collapse keeps 2', count($collapsed) === 2);
$byTier = array();
foreach ($collapsed as $row) {
	$byTier[(string) ($row['epc_price_tier'] ?? '')] = $row;
}
check('collapse has min tier', isset($byTier['min']) && abs((float) $byTier['min']['price'] - 18) < 0.001);
check('collapse has max tier', isset($byTier['max']) && abs((float) $byTier['max']['price'] - 30) < 0.001);
check('min storage set', ($byTier['min']['storage'] ?? '') === EPC_MV_MIN_TIER);
check('max storage set', ($byTier['max']['storage'] ?? '') === EPC_MV_MAX_TIER);

$tmp = sys_get_temp_dir() . '/epc_mv_tier_' . getmypid() . '.csv';
check('write csv with storage', epc_multivendor_write_docpart_csv($tmp, $collapsed));
$csv = (string) file_get_contents($tmp);
@unlink($tmp);
check('csv header has Storage', strpos($csv, 'Storage') !== false);
check('csv contains min marker', strpos($csv, 'epc_mv_min') !== false);
check('csv contains max marker', strpos($csv, 'epc_mv_max') !== false);

$page = (string) file_get_contents($root . '/cp/content/shop/prices_upload/multivendor_data_upload.php');
check('cp page has ACL panel', strpos($page, 'epcMvMinRestrict') !== false);
check('cp page has groups select', strpos($page, 'epcMvMinGroups') !== false);
$js = (string) file_get_contents($root . '/cp/content/shop/prices_upload/epc_multivendor_cp.js');
check('js loads ACL', strpos($js, 'min_price_acl_get') !== false);
check('js saves ACL', strpos($js, 'min_price_acl_save') !== false);
$ajax = (string) file_get_contents($root . '/cp/content/shop/prices_upload/ajax_epc_multivendor_ingest.php');
check('ajax has acl get', strpos($ajax, 'min_price_acl_get') !== false);
check('ajax has acl save', strpos($ajax, 'min_price_acl_save') !== false);
$ci = (string) file_get_contents($root . '/content/shop/docpart/suppliers_handlers/prices/common_interface.php');
check('prices handler filters min rows', strpos($ci, 'epc_mv_min_price_should_hide_row') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
