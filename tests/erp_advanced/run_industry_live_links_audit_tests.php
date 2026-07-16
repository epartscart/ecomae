<?php
/**
 * Audit: every portal industry has a working live hub/sub link.
 *
 *   php tests/erp_advanced/run_industry_live_links_audit_tests.php
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
require_once $root . '/content/general_pages/epc_portal_industry_live_bridge.php';
require_once $root . '/content/general_pages/epc_industry_seo.php';
require_once $root . '/content/general_pages/epc_ecomae_platform_router.php';

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

echo "== Live bridge audit ==\n";
$audit = epc_portal_industry_live_audit();
check('no broken portal industries', $audit['broken'] === array());
if ($audit['broken'] !== array()) {
	foreach ($audit['broken'] as $code => $why) {
		echo "    BROKEN $code: $why\n";
	}
}
check('at least 40 live industries', count($audit['ok']) >= 40);

echo "\n== Critical URLs ==\n";
$expect = array(
	'nutrition_supplements' => 'https://healthcare.ecomae.com/food-supplements-nutrition',
	'furniture_interiors' => 'https://homeliving.ecomae.com/furniture-retail',
	'pharmacy_retail' => 'https://healthcare.ecomae.com/pharmacy-drug-dispensing',
	'grocery_retail' => 'https://retail.ecomae.com/supermarket-grocery',
	'fashion' => 'https://fashion.ecomae.com/',
	'jewellery' => 'https://jewellery.ecomae.com/',
	'food_beverage' => 'https://food.ecomae.com/',
	'beauty_skincare' => 'https://beauty.ecomae.com/skincare-facials',
	'fitness_training' => 'https://sports.ecomae.com/gym-fitness-center',
	'pet_services' => 'https://pet.ecomae.com/pet-supply-store',
);
foreach ($expect as $code => $url) {
	$got = epc_portal_industry_live_storefront_url($code);
	check("$code → $url", $got === $url);
}

echo "\n== Food supplements render ==\n";
$_SERVER['REQUEST_URI'] = '/food-supplements-nutrition';
$_SERVER['HTTP_HOST'] = 'healthcare.ecomae.com';
$GLOBALS['epc_industry_subdomain_active'] = true;
$GLOBALS['epc_industry_subdomain_slug'] = 'healthcare';
$GLOBALS['epc_industry_subdomain_group'] = 'healthcare';
ob_start();
include $root . '/content/general_pages/industry_templates/healthcare.php';
$html = (string) ob_get_clean();
check('supplements page renders', stripos($html, 'Food supplements') !== false && strpos($html, 'Vitamins') !== false);
check('supplements in SEO sub list', in_array('Food supplements & nutrition', epc_industry_seo_template_sub_industries('healthcare'), true));

echo "\n== Platform route aliases ==\n";
$r = epc_ecomae_platform_match_path('/platform/industries/nutrition-supplements');
check('industries hyphen alias', ($r['page'] ?? '') === 'industry' && ($r['params']['code'] ?? '') === 'nutrition_supplements');

echo "\n== /platform/industries primary grid ==\n";
require_once $root . '/content/general_pages/epc_ecomae_platform_pages.php';
ob_start();
echo epc_ecomae_platform_page_industries();
$industriesHtml = (string) ob_get_clean();
check('page lists food supplements', stripos($industriesHtml, 'Food supplements') !== false);
check('page lists furniture', stripos($industriesHtml, 'Furniture') !== false);
check('hero uses client industries count not only 28 hubs', preg_match('/\b4[0-9]\b UAE\/GCC client industries/i', $industriesHtml) === 1
	|| preg_match('/Client Industries<\/div>/', $industriesHtml) === 1);
check('primary grid id indGrid present', strpos($industriesHtml, 'id="indGrid"') !== false);
check('hubs moved to hubGrid', strpos($industriesHtml, 'id="hubGrid"') !== false);
// Count primary industry cards (portal catalogue; exclude section id="industry-hubs")
preg_match_all('/id="industry-([a-z0-9_]+)"/', $industriesHtml, $mInd);
$cardIds = array_values(array_filter($mInd[1] ?? array(), static function ($id) {
	return $id !== 'hubs';
}));
$cardCount = count($cardIds);
check('primary grid has 40+ industry cards', $cardCount >= 40);
check('primary grid matches marketing catalogue size', $cardCount === count(epc_ecomae_platform_industry_marketing()));
echo "  (primary industry cards: $cardCount)\n";

echo "\n== Sample links ==\n";
foreach (array_slice($expect, 0, 6) as $code => $url) {
	echo "  $code\n    $url\n";
}

echo "\n----------------------------\n";
echo 'Live OK: ' . count($audit['ok']) . '  Broken: ' . count($audit['broken']) . "\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
