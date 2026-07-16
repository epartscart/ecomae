<?php
/**
 * Smoke tests: industry theme + storefront package auto-apply for new tenants.
 *
 *   php tests/erp_advanced/run_tenant_industry_theme_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/general_pages/epc_portal_storefront_packages.php';
require_once $root . '/content/general_pages/epc_portal_theme_templates.php';
require_once $root . '/content/general_pages/epc_portal_tenant_intro.php';

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

section('Package registry ↔ industry');
check('auto_parts → automotive package', epc_portal_storefront_package_for_industry('auto_parts') === 'automotive_spareparts_pro');
check('jewellery → kiyasha package', epc_portal_storefront_package_for_industry('jewellery') === 'jewellery_retail_kiyasha');
check('fashion → namshi package', epc_portal_storefront_package_for_industry('fashion') === 'fashion_retail_namshi');
check('electronics → virgin package', epc_portal_storefront_package_for_industry('electronics') === 'electronics_retail_virgin');
check('medical → no package yet', epc_portal_storefront_package_for_industry('medical') === '');

section('Default visual styles');
check('jewellery default classic', epc_portal_default_theme_template('jewellery') === 'classic');
check('electronics default midnight', epc_portal_default_theme_template('electronics') === 'midnight');
check('fashion default signature', epc_portal_default_theme_template('fashion') === 'signature');
check('medical default modern', epc_portal_default_theme_template('medical') === 'modern');

section('Apply profile — jewellery');
$settings = array('tagline' => 'Keep me', 'access_mode' => 'full');
$contact = array('trade_name' => 'Demo Jewels');
$out = epc_portal_apply_industry_theme_profile($settings, $contact, 'jewellery');
check('jewellery sets package', ($out['storefront_package'] ?? '') === 'jewellery_retail_kiyasha');
check('jewellery sets classic style', ($out['theme_template'] ?? '') === 'classic');
check('jewellery writes contact package', ($contact['storefront_package'] ?? '') === 'jewellery_retail_kiyasha');
check('jewellery sets theme colours', !empty($settings['theme']['primary']));
check('jewellery uses tenant brand', !empty($contact['use_tenant_brand']));

section('Apply profile — auto_parts');
$settings = array();
$contact = array();
$out = epc_portal_apply_industry_theme_profile($settings, $contact, 'auto_parts');
check('auto_parts package', ($out['storefront_package'] ?? '') === 'automotive_spareparts_pro');
check('auto_parts disables hub logo', isset($contact['use_animated_hub_logo']) && $contact['use_animated_hub_logo'] === false);
check('auto_parts disables tenant brand', isset($contact['use_tenant_brand']) && $contact['use_tenant_brand'] === false);

section('Apply profile — medical (colours only)');
$settings = array();
$contact = array();
$out = epc_portal_apply_industry_theme_profile($settings, $contact, 'medical');
check('medical has no package', ($out['storefront_package'] ?? '') === '');
check('medical still has style', ($out['theme_template'] ?? '') === 'modern');
check('medical theme colours set', !empty($settings['theme']['primary']));

section('Overrides');
$settings = array();
$contact = array();
$out = epc_portal_apply_industry_theme_profile($settings, $contact, 'jewellery', array(
	'theme_template' => 'midnight',
	'storefront_package' => 'none',
));
check('override clears package', ($out['storefront_package'] ?? '') === '');
check('override style midnight', ($out['theme_template'] ?? '') === 'midnight');

section('Intro POST captures theme fields');
$intro = epc_portal_intro_from_post(array(
	'contact_person' => 'A',
	'contact_email' => 'a@b.com',
	'admin_email' => 'a@b.com',
	'country' => 'United Arab Emirates',
	'country_code' => 'AE',
	'theme_template' => 'signature',
	'storefront_package' => 'fashion_retail_namshi',
));
check('intro stores theme_template', ($intro['theme_template'] ?? '') === 'signature');
check('intro stores storefront_package', ($intro['storefront_package'] ?? '') === 'fashion_retail_namshi');

section('Wiring');
$base = (string) file_get_contents($root . '/content/general_pages/epc_portal_tenant_intro.php');
check('apply_intro uses theme profile helper', strpos($base, 'epc_portal_apply_industry_theme_profile') !== false);
check('tenant apply helper exists', strpos($base, 'function epc_portal_apply_industry_theme_to_tenant') !== false);
$hub = (string) file_get_contents($root . '/cp/content/shop/tenant_hub/tenant_hub_main.php');
check('tenant hub has apply theme POST', strpos($hub, 'epc_th_apply_theme') !== false);
$panel = (string) file_get_contents($root . '/content/shop/tenant_hub/epc_tenant_onboard_panel.php');
check('onboard has theme selectors', strpos($panel, 'epc_intro_theme_template') !== false && strpos($panel, 'epc_intro_storefront_package') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
