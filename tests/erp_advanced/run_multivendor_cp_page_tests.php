<?php
/**
 * Multivendor CP page UI — no visible JSON / eval-safe layout.
 *
 *   php tests/erp_advanced/run_multivendor_cp_page_tests.php
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

echo "== Multivendor CP page UI ==\n";

$page = (string) file_get_contents($root . '/cp/content/shop/prices_upload/multivendor_data_upload.php');
$js = (string) file_get_contents($root . '/cp/content/shop/prices_upload/epc_multivendor_cp.js');
$css = (string) file_get_contents($root . '/cp/content/shop/prices_upload/epc_prices_cp.css');
$assets = (string) file_get_contents($root . '/content/general_pages/epc_cp_page_assets.php');

check('page has graphical root', strpos($page, 'id="epcMultivendorRoot"') !== false);
check('page has hero', strpos($page, 'epc-multivendor-hero') !== false);
check('page has upload form', strpos($page, 'id="epcMultivendorIngestForm"') !== false);
check('page has data-ajax-url', strpos($page, 'data-ajax-url=') !== false);
check('page has data-csrf-key', strpos($page, 'data-csrf-key=') !== false);
check('page registers page assets', strpos($page, 'epc_cp_register_page_assets') !== false);
$pageNoComments = (string) preg_replace('#/\*.*?\*/#s', '', $page);
check('page has no inline script tag', stripos($pageNoComments, '<script') === false);
check('page has no boot JSON dump div', strpos($page, 'epc-multivendor-boot') === false);
check('page does not echo raw ajaxUrl JSON blob', strpos($page, '{"ajaxUrl"') === false);

check('js reads root data attrs', strpos($js, 'readRootData') !== false && strpos($js, 'data-ajax-url') !== false);
check('js still supports legacy boot id', strpos($js, 'epc-multivendor-boot') !== false);

check('css has teal hero', strpos($css, 'epc-multivendor-hero') !== false && strpos($css, '#0891b2') !== false);
check('css has stats strip', strpos($css, 'epc-multivendor-stats') !== false);

check('page has vendor codes section', strpos($page, 'id="epcMvVendorCodesTable"') !== false);
check('page cache-bust mvCode1', strpos($page, 'mvCode1') !== false);
check('js loads vendor codes', strpos($js, 'vendor_codes_list') !== false && strpos($js, 'loadVendorCodes') !== false);
check('js saves vendor code', strpos($js, 'vendor_code_save') !== false);
check('match key mentions vendor name + code', stripos($page, 'vendor name + code') !== false || stripos($page, 'Vendor name + Code') !== false);

$ajax = (string) file_get_contents($root . '/cp/content/shop/prices_upload/ajax_epc_multivendor_ingest.php');
check('ajax has vendor_codes_list', strpos($ajax, "vendor_codes_list") !== false);
check('ajax has vendor_code_save', strpos($ajax, "vendor_code_save") !== false);

$ingest = (string) file_get_contents($root . '/content/shop/docpart/epc_multivendor_price_ingest.php');
check('ingest warehouse matches name+code', strpos($ingest, 'AND UPPER(TRIM(`short_name`)) = UPPER(?)') !== false);
check('ingest vendor_key uses full+short', strpos($ingest, 'function epc_multivendor_vendor_key(string $full, string $short') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
