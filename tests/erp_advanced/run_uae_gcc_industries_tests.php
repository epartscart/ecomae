<?php
/**
 * UAE/GCC DET–DED industry catalogue coverage tests.
 *
 *   php tests/erp_advanced/run_uae_gcc_industries_tests.php
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
require_once $root . '/content/general_pages/epc_industry_consolidation.php';
require_once $root . '/content/general_pages/epc_ded_activity_mapping.php';
require_once $root . '/content/general_pages/epc_portal_theme_templates.php';

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

function section(string $t): void
{
	echo "\n== $t ==\n";
}

section('Onboard catalogue includes missing verticals');
$all = epc_portal_industries();
check('nutrition_supplements present', isset($all['nutrition_supplements']));
check('furniture_interiors present', isset($all['furniture_interiors']));
check('food_beverage present', isset($all['food_beverage']));
check('pharmacy_retail present', isset($all['pharmacy_retail']));
check('beauty_skincare present', isset($all['beauty_skincare']));
check('grocery_retail present', isset($all['grocery_retail']));
check('fmcg_wholesale present', isset($all['fmcg_wholesale']));
check('logistics_freight present', isset($all['logistics_freight']));
check('hospitality_travel present', isset($all['hospitality_travel']));
check('building_materials present', isset($all['building_materials']));
check('at least 35 onboard industries', count($all) >= 35);

section('Names are client-readable');
check('supplements label', stripos($all['nutrition_supplements']['name'], 'supplement') !== false);
check('furniture label', stripos($all['furniture_interiors']['name'], 'furniture') !== false);

section('Group mapping (DED templates)');
$map = epc_portal_industry_group_map();
check('supplements → healthcare', ($map['nutrition_supplements'] ?? '') === 'healthcare_medical');
check('furniture → home_living', ($map['furniture_interiors'] ?? '') === 'home_living');
check('food → food_beverage', ($map['food_beverage'] ?? '') === 'food_beverage');
check('resolve supplements code', epc_industry_resolve_group('nutrition_supplements') === 'healthcare_medical');
check('resolve furniture code', epc_industry_resolve_group('furniture_interiors') === 'home_living');
check('resolve supplement keyword', epc_industry_resolve_group('Vitamin & food supplements trading') === 'healthcare_medical');
check('resolve furniture keyword', epc_industry_resolve_group('Furniture retail showroom') === 'home_living');

section('DED bridge');
check('DED total activities ~2987', epc_ded_total_activities() >= 2900);
check('DED division coverage audit clean', epc_ded_coverage_audit() === array());
$bridge = epc_ded_portal_industry_bridge();
check('bridge has divisions', count($bridge) >= 15);
check('every DED division has portal codes', epc_ded_portal_bridge_complete());
$gaps = epc_portal_ded_group_coverage_gaps();
// nonprofit may remain intentional; commerce/lifestyle groups must not gap
$criticalGaps = array_intersect($gaps, array(
	'food_beverage', 'home_living', 'healthcare_medical', 'beauty_wellness',
	'wholesale_trading', 'logistics_transport', 'hospitality_travel', 'retail_ecommerce',
));
check('no critical group gaps', $criticalGaps === array());

section('Settings dropdown / ecosystems');
$settings = epc_portal_settings_industries();
check('settings includes supplements', isset($settings['nutrition_supplements']));
check('settings includes furniture', isset($settings['furniture_interiors']));
$grouped = epc_portal_industries_grouped($settings);
$commerceCount = isset($grouped['commerce']['industries']) ? count($grouped['commerce']['industries']) : 0;
check('commerce ecosystem has 10+ industries', $commerceCount >= 10);

section('Theme defaults');
check('supplements default style', epc_portal_default_theme_template('nutrition_supplements') === 'signature');
check('furniture default style', epc_portal_default_theme_template('furniture_interiors') === 'modern');

echo "\n----------------------------\n";
echo 'Industries: ' . count($all) . "  DED activities: " . epc_ded_total_activities() . "\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
if ($gaps !== array()) {
	echo 'Non-critical group gaps: ' . implode(', ', $gaps) . "\n";
}
exit($fail_count > 0 ? 1 : 0);
