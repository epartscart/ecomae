<?php
/**
 * Price upload history / download path regression tests.
 *
 *   php tests/erp_advanced/run_price_upload_history_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/shop/docpart/docpart_price_upload_history.php';
require_once $root . '/content/shop/docpart/epc_price_import_helpers.php';
require_once $root . '/content/general_pages/epc_cp_page_assets.php';

$pass = 0;
$fail = 0;
function check(string $l, bool $c): void
{
	global $pass, $fail;
	if ($c) {
		$pass++;
		echo "  PASS  $l\n";
	} else {
		$fail++;
		echo "  FAIL  $l\n";
	}
}

echo "== History AJAX bootstrap path ==\n";
$historyAjax = $root . '/cp/content/shop/prices_upload/ajax_epc_price_upload_history.php';
$ajaxInit = $root . '/cp/content/shop/prices_upload/epc_prices_ajax_init.php';
$wrongInit = $root . '/cp/content/shop/epc_prices_ajax_init.php';
check('history ajax exists', is_file($historyAjax));
check('ajax init exists in prices_upload/', is_file($ajaxInit));
check('wrong shop-level ajax init does NOT exist', !is_file($wrongInit));
$src = (string) file_get_contents($historyAjax);
check('history ajax requires ./epc_prices_ajax_init.php', strpos($src, "__DIR__ . '/epc_prices_ajax_init.php'") !== false);
check('history ajax does NOT use broken ../epc_prices_ajax_init.php', strpos($src, "__DIR__ . '/../epc_prices_ajax_init.php'") === false);
check('history ajax boots via require (not JSON header first)', preg_match('/^<\?php\s*\/\*\*/s', $src) === 1
	&& preg_match('/^<\?php\s*header\s*\(\s*[\'"]Content-Type:\s*application\/json/i', $src) !== 1);
check('history ajax streams octet-stream for files', strpos($src, "application/octet-stream") !== false);
check('download_latest falls back to DB export', strpos($src, 'epc_history_stream_db_export') !== false
	&& strpos($src, "action === 'download_latest'") !== false);

echo "\n== Archive file helper ==\n";
$tmpSrc = sys_get_temp_dir() . '/epc_price_hist_test_' . getmypid() . '.csv';
file_put_contents($tmpSrc, "brand,article,price\nACME,ABC123,10.50\n");
$rel = epc_price_history_archive_file($tmpSrc, 999001, 'test_prices.csv');
check('archive returns relative path', $rel !== '' && strpos($rel, '/content/files/price_upload_history/999001/') === 0);
$abs = $root . $rel;
check('archived file exists on disk', $rel !== '' && is_file($abs));
check('archived content preserved', $rel !== '' && strpos((string) file_get_contents($abs), 'ACME') !== false);
@unlink($tmpSrc);
if ($rel !== '' && is_file($abs)) {
	@unlink($abs);
}

echo "\n== Source labels ==\n";
check('ftp label', epc_price_history_source_label('pyprices_ftp') === 'FTP (pyprices)');
check('email label', epc_price_history_source_label('pyprices_email') === 'Email (pyprices)');
check('pc upload label', epc_price_history_source_label('pyprices_upload') === 'PC upload (pyprices)');

echo "\n== CP page assets cover price child pages ==\n";
$map = epc_cp_page_asset_url_map();
check('shop/prices has history js', isset($map['shop/prices']['js']) && count($map['shop/prices']['js']) >= 2);
check('shop/prices/price has history js', isset($map['shop/prices/price']['js']));
check('shop/prices/guide has history js', isset($map['shop/prices/guide']['js']));
$priceAssets = epc_cp_page_assets_for_url('shop/prices/price');
$joined = implode(' ', array_keys($priceAssets['js']));
check('price page assets include history config', strpos($joined, 'epc_prices_upload_history_config.php') !== false);
check('price page assets include history js', strpos($joined, 'epc_prices_upload_history.js') !== false);

echo "\n== History config applies portal ==\n";
$cfgSrc = (string) file_get_contents($root . '/cp/content/shop/prices_upload/epc_prices_upload_history_config.php');
check('history config calls epc_portal_apply_config', strpos($cfgSrc, 'epc_portal_apply_config') !== false);

echo "\n== Diagnostics uses ajax init ==\n";
$diagSrc = (string) file_get_contents($root . '/cp/content/shop/prices_upload/ajax_epc_price_upload_diagnostics.php');
check('diagnostics requires epc_prices_ajax_init', strpos($diagSrc, "epc_prices_ajax_init.php") !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
