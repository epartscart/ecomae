<?php
/**
 * CP prices manager performance / CMS-eval safety regression tests.
 *
 *   php tests/erp_advanced/run_cp_prices_perf_tests.php
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

echo "== Prices manager CMS-eval safety ==\n";
$mgr = (string) file_get_contents($root . '/cp/content/shop/prices_upload/prices_manager.php');
check('no bare exit on guide redirect', !preg_match('/header\(\s*[\'"]Location:[^;]+;\s*\n\s*exit\s*;/', $mgr));
check('uses render_manager flag', strpos($mgr, '$epc_prices_render_manager') !== false);
check('guide path does not return from eval', !preg_match('/guide\.php\'\);\s*\n\s*return\s*;/', $mgr));

echo "\n== Perf helper ==\n";
$perf = (string) file_get_contents($root . '/cp/content/shop/prices_upload/epc_prices_manager_perf.php');
check('page view skips SHOW INDEX', strpos($perf, 'Page view: never SHOW INDEX') !== false || strpos($perf, 'if (!$allowAlter)') !== false);
check('defer history always on', strpos($perf, 'function epc_prices_defer_inline_update_history') !== false);

echo "\n== Storefront schema ==\n";
$ssf = (string) file_get_contents($root . '/content/shop/docpart/epc_storefront_storage_flags.php');
check('ensure_schema has allowAlter', strpos($ssf, 'function epc_ssf_ensure_schema(PDO $db, bool $allowAlter = false)') !== false);
$panel = (string) file_get_contents($root . '/cp/content/shop/prices_upload/epc_storefront_storage_panel.php');
check('panel calls ensure_schema without alter', strpos($panel, 'epc_ssf_ensure_schema($db_link, false)') !== false);

echo "\n== Relief scripts ==\n";
check('epc-db-relief exists', is_file($root . '/epc-db-relief.php'));
$relief = (string) file_get_contents($root . '/epc-db-relief.php');
check('relief can SHOW PROCESSLIST', strpos($relief, 'SHOW FULL PROCESSLIST') !== false);
check('relief can KILL', strpos($relief, 'KILL ') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
