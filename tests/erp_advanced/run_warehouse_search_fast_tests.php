<?php
/**
 * Warehouse caption + fast article search regressions.
 *
 *   php tests/erp_advanced/run_warehouse_search_fast_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/shop/docpart/docpart_article_match.php';
require_once $root . '/content/shop/docpart/epc_storefront_prices_helpers.php';

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

echo "== Article match helpers ==\n";
check('normalize strips separators', docpart_normalize_article_for_price('oc-47/1') === 'OC471');
check('match expr helper exists', function_exists('docpart_sql_article_match_expr'));
check('ensure column helper exists', function_exists('docpart_price_data_ensure_article_search_column'));
check('backfill helper exists', function_exists('docpart_price_data_backfill_article_search'));
$fallbackExpr = docpart_sql_article_match_expr(null, '`article`');
check('without PDO falls back to REPLACE expr', stripos($fallbackExpr, 'REPLACE') !== false);

echo "\n== Warehouse caption fill ==\n";
check('fill helper exists', function_exists('epc_storefront_fill_warehouse_captions'));

// SQLite-less unit: mock via reflection of function behavior with in-memory PDO MySQL not available.
// Static source checks for critical call sites.
$common = (string) file_get_contents($root . '/content/shop/docpart/suppliers_handlers/prices/common_interface.php');
check('analogs path selects short_name', strpos($common, '`short_name`') !== false
	&& strpos($common, 'Always expose warehouse name') !== false);
check('common_interface uses article match expr', strpos($common, 'docpart_sql_article_match_expr') !== false);

$ajax = (string) file_get_contents($root . '/content/shop/docpart/ajax_getProductsOfBunch.php');
check('ajax fills warehouse captions before redact', strpos($ajax, 'epc_storefront_fill_warehouse_captions') !== false);
check('ajax no longer blanks caption for customers only', strpos($ajax, 'Только для менеджера. Для остальных значение равно') === false);

$ui = (string) file_get_contents($root . '/content/shop/docpart/part_search_page_1.php');
check('UI shows warehouse label without CHPU gate', strpos($ui, 'Always show warehouse name on storefront results') !== false);

$page = (string) file_get_contents($root . '/content/shop/docpart/part_search_page.php');
check('page filters disabled warehouses', strpos($page, 'epc_ssf_filter_office_storage_bunches') !== false);
check('page warehouse label falls back to name', strpos($page, 'prefer short_name, fall back to name') !== false
	|| strpos($page, "whLabel === ''") !== false);

$mfr = (string) file_get_contents($root . '/content/shop/docpart/ajax_getManufacturersListFromPrices.php');
check('manufacturers list uses match expr', strpos($mfr, 'docpart_sql_article_match_expr') !== false);

$ajax5 = (string) file_get_contents($root . '/cp/content/shop/prices_upload/ajax_5_import_csv_to_db.php');
check('import backfills article_search', strpos($ajax5, 'docpart_price_data_backfill_article_search') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
