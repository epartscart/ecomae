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

check('assets map cache-bust mvUi3', strpos($assets, 'mvUi3') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
