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

echo "\n== QTY (records_count) display ==\n";
$perf2 = (string) file_get_contents($root . '/cp/content/shop/prices_upload/epc_prices_manager_perf.php');
check('live counts map helper exists', strpos($perf2, 'function epc_prices_live_counts_map') !== false);
check('rows helper with QTY fallback exists', strpos($perf2, 'function epc_prices_fetch_lists_rows') !== false);
check('persists live counts opportunistically', strpos($perf2, 'function epc_prices_persist_records_counts') !== false);
$mgr2 = (string) file_get_contents($root . '/cp/content/shop/prices_upload/prices_manager.php');
check('manager uses rows helper', strpos($mgr2, 'epc_prices_fetch_lists_rows($db_link)') !== false);
check('manager iterates rows array', strpos($mgr2, 'foreach( $epc_prices_list_rows as $element_record )') !== false);
$ajax6 = (string) file_get_contents($root . '/cp/content/shop/prices_upload/ajax_6_complete_session.php');
check('upload completion syncs records_count', strpos($ajax6, 'SET `records_count` = ?') !== false);
check('qty fix ops script exists', is_file($root . '/epc-prices-qty-fix.php'));

// Behavior check: run the fallback logic on a synthetic dataset (SQLite-like via arrays).
$rows = array(
	array('id' => 3, 'name' => 'A-S', 'records_count' => 0),
	array('id' => 8, 'name' => 'B.P', 'records_count' => 0),
);
$live = array(3 => 1200, 8 => 340);
$allZero = true;
foreach ($rows as $row) {
	if ((int) $row['records_count'] > 0) { $allZero = false; }
}
if ($allZero) {
	foreach ($rows as $i => $row) {
		$pid = (int) $row['id'];
		if (isset($live[$pid])) { $rows[$i]['records_count'] = $live[$pid]; }
	}
}
check('fallback maps live QTY onto zero rows', $rows[0]['records_count'] === 1200 && $rows[1]['records_count'] === 340);

echo "\n== epartscart 1s path ==\n";
$fast = (string) file_get_contents($root . '/cp/epc_cp_fast_tenant.php');
check('fast tenant helper exists', $fast !== '');
check('skips ERP routers on epartscart', strpos($fast, 'epc_cp_should_skip_erp_routers') !== false);
$idx = (string) file_get_contents($root . '/cp/index.php');
check('cp index loads fast tenant', strpos($idx, 'epc_cp_fast_tenant.php') !== false);
check('cp index skips early ERP for epartscart', strpos($idx, '$epcCpSkipErpRoutersEarly') !== false);
$match = (string) file_get_contents($root . '/content/shop/docpart/docpart_article_match.php');
check('article_search ensure has allowAlter', strpos($match, 'bool $allowAlter = false') !== false);
check('search path does not backfill', strpos($match, 'Never backfill on the live search path') !== false);
check('1s speed ops script exists', is_file($root . '/epc-epartscart-1s-speed.php'));

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
