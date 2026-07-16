<?php
/**
 * Food supplements live path smoke tests.
 *
 *   php tests/erp_advanced/run_food_supplements_live_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/general_pages/epc_portal.php';
require_once $root . '/content/general_pages/epc_industry_seo.php';
require_once $root . '/content/general_pages/epc_ecomae_platform_router.php';

$pass_count = 0;
$fail_count = 0;

function check(string $label, bool $cond): void
{
	global $pass_count, $fail_count;
	if ($cond) {
		$pass_count++;
		echo "  PASS  $label\n";
	} else {
		$fail_count++;
		echo "  FAIL  $label\n";
	}
}

echo "== Live URL ==\n";
$url = epc_portal_industry_live_storefront_url('nutrition_supplements');
echo "  URL: $url\n";
check('live url is healthcare sub path', $url === 'https://healthcare.ecomae.com/food-supplements-nutrition');
check('slug helper', epc_industry_seo_sub_slug('Food supplements & nutrition') === 'food-supplements-nutrition');

echo "\n== Healthcare render ==\n";
$_SERVER['REQUEST_URI'] = '/food-supplements-nutrition';
$_SERVER['HTTP_HOST'] = 'healthcare.ecomae.com';
$GLOBALS['epc_industry_subdomain_active'] = true;
$GLOBALS['epc_industry_subdomain_slug'] = 'healthcare';
$GLOBALS['epc_industry_subdomain_group'] = 'healthcare';
ob_start();
include $root . '/content/general_pages/industry_templates/healthcare.php';
$html = (string) ob_get_clean();
check('premium 3d body', strpos($html, 'ind-premium-3d') !== false);
check('title Food supplements', stripos($html, 'Food supplements') !== false);
check('Vitamins category', strpos($html, 'Vitamins') !== false);
check('Protein category', strpos($html, 'Protein') !== false);
check('canonical path', strpos($html, 'healthcare.ecomae.com/food-supplements-nutrition') !== false);

echo "\n== Platform route aliases ==\n";
$r1 = epc_ecomae_platform_match_path('/platform/industry/nutrition_supplements');
$r2 = epc_ecomae_platform_match_path('/platform/industries/nutrition_supplements');
$r3 = epc_ecomae_platform_match_path('/platform/industries/nutrition-supplements');
check('industry underscore route', ($r1['page'] ?? '') === 'industry' && ($r1['params']['code'] ?? '') === 'nutrition_supplements');
check('industries underscore alias', ($r2['page'] ?? '') === 'industry' && ($r2['params']['code'] ?? '') === 'nutrition_supplements');
check('industries hyphen alias', ($r3['page'] ?? '') === 'industry' && ($r3['params']['code'] ?? '') === 'nutrition_supplements');

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
