<?php
/**
 * Regression: all price upload ways must call the nginx-safe pyprices bridge.
 *
 *   php tests/erp_advanced/run_price_upload_ways_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

$root = dirname(__DIR__, 2);
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

echo "== pyprices endpoint helper ==\n";
require_once $root . '/content/shop/docpart/epc_price_upload_diagnostics.php';
check('helper returns relative path', epc_pyprices_api_url('') === '/pyprices/pyprices-api.php');
check('helper joins domain', epc_pyprices_api_url('https://www.epartscart.com/') === 'https://www.epartscart.com/pyprices/pyprices-api.php');
check('bridge file exists', is_file($root . '/pyprices/pyprices-api.php'));

echo "\n== Callers use pyprices-api.php (not static api.py POST) ==\n";
$files = [
	'cp/content/shop/prices_upload/prices_manager.php',
	'cp/content/shop/prices_upload/for_pyprices/for_cron/cron_task_executor.php',
	'content/shop/docpart/epc_price_upload_diagnostics.php',
	'epc-test-all-upload-paths.php',
	'epc-test-imap.php',
	'cp/content/shop/prices_upload/epc_prices_manager_perf.php',
];
foreach ($files as $rel) {
	$src = (string) file_get_contents($root . '/' . $rel);
	check($rel . ' references pyprices-api.php', strpos($src, 'pyprices-api.php') !== false);
	// Allow mentioning api.py only in comments / Windows shebang docs, not as POST URL.
	$postToPy = preg_match('#["\']/?pyprices/api\.py["\']#', $src)
		|| preg_match('#domain_path\s*\.\s*["\']pyprices/api\.py["\']#', $src)
		|| preg_match('#/pyprices/api\.py[\'"]#', $src);
	check($rel . ' does not POST to /pyprices/api.py', !$postToPy);
}

echo "\n== External upload API parse + aliases ==\n";
$api = $root . '/api/prices/upload_price.php';
check('upload_price.php exists', is_file($api));
$lint = [];
$code = 0;
exec('php -l ' . escapeshellarg($api) . ' 2>&1', $lint, $code);
check('upload_price.php php -l clean', $code === 0);
$apiSrc = (string) file_get_contents($api);
check('accepts price_id alias', strpos($apiSrc, "price_id") !== false);
check('accepts file/price_file aliases', strpos($apiSrc, "'file'") !== false && strpos($apiSrc, "'price_file'") !== false);
check('no semicolon-inside-array bug', !preg_match("/translate_str_by_id\(\d+\);/", $apiSrc));

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
