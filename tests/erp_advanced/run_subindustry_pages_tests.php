<?php
/**
 * Smoke tests for premium sub-industry storefronts (same 3D stack as industry hubs).
 *
 *   php tests/erp_advanced/run_subindustry_pages_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/general_pages/epc_industry_seo.php';

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

section('Presentation helpers still exist');
$p1 = epc_industry_seo_sub_presentation('precious-metals-refining');
check('presentation has key', isset($p1['key'], $p1['tone']) && $p1['key'] !== '');

section('Render jewellery sub page as premium 3D storefront');
$_SERVER['REQUEST_URI'] = '/precious-metals-refining';
$_SERVER['HTTP_HOST'] = 'jewellery.ecomae.com';
$GLOBALS['epc_industry_subdomain_active'] = true;
$GLOBALS['epc_industry_subdomain_slug'] = 'jewellery';
$GLOBALS['epc_industry_subdomain_group'] = 'jewellery';
ob_start();
include $root . '/content/general_pages/industry_templates/jewellery.php';
$html = (string) ob_get_clean();

check('uses premium 3d body', strpos($html, 'ind-premium-3d') !== false);
check('marks sub-site class', strpos($html, 'ind-sub-site') !== false);
check('has 3d stage animation', strpos($html, 'ind3d-stage') !== false);
check('has hero video wrap', strpos($html, 'heroVidWrap') !== false && strpos($html, 'ind-hero-video') !== false);
check('not lightweight isp-body', strpos($html, 'isp-body') === false);
check('title is sub-industry', stripos($html, 'Precious metals refining') !== false);
check('shows Gold Items category', strpos($html, 'Gold Items') !== false);
check('shows Diamond Pieces', strpos($html, 'Diamond Pieces') !== false);
check('shows Certification', strpos($html, 'Certification') !== false);
check('shows Silver Collection', strpos($html, 'Silver Collection') !== false);
check('canonical sub path', strpos($html, 'jewellery.ecomae.com/precious-metals-refining') !== false);
check('hub link back', strpos($html, 'jewellery.ecomae.com/') !== false);
check('related verticals section', strpos($html, 'id="related-verticals"') !== false || strpos($html, 'Related') !== false);
check('categories section present', strpos($html, 'id="categories"') !== false);
check('products section present', strpos($html, 'id="products"') !== false);
check('sub-specific product present', stripos($html, 'Precious Metals') !== false);
check('sub hero photo not only hub hero', strpos($html, 'photo-1602751584552') !== false || strpos($html, 'photo-1573408301185') !== false);

section('Render energy biomass sub page');
$_SERVER['REQUEST_URI'] = '/biomass-bioenergy';
$_SERVER['HTTP_HOST'] = 'energy.ecomae.com';
$GLOBALS['epc_industry_subdomain_slug'] = 'energy';
$GLOBALS['epc_industry_subdomain_group'] = 'energy';
ob_start();
include $root . '/content/general_pages/industry_templates/energy.php';
$html2 = (string) ob_get_clean();
check('energy sub uses premium 3d', strpos($html2, 'ind-premium-3d') !== false && stripos($html2, 'Biomass') !== false);
check('energy sub has categories', strpos($html2, 'id="categories"') !== false);
check('energy sub has 3d stage', strpos($html2, 'ind3d-stage') !== false);

section('Hub still 3d when no sub path');
$_SERVER['REQUEST_URI'] = '/';
$_SERVER['HTTP_HOST'] = 'jewellery.ecomae.com';
$GLOBALS['epc_industry_subdomain_slug'] = 'jewellery';
$GLOBALS['epc_industry_subdomain_group'] = 'jewellery';
ob_start();
include $root . '/content/general_pages/industry_templates/jewellery.php';
$hub = (string) ob_get_clean();
check('hub keeps 3d skin', strpos($hub, 'ind-premium-3d') !== false);
check('hub not sub-site class', strpos($hub, 'ind-sub-site') === false);
check('hub still lists sub-industries', strpos($hub, 'id="industries"') !== false);

section('Wiring');
$base = (string) file_get_contents($root . '/content/general_pages/industry_templates/_base_template.php');
check('base uses prepare script', strpos($base, '_sub_industry_prepare.php') !== false);
check('base does not early-return to lightweight page', strpos($base, "require __DIR__ . '/_sub_industry_page.php'") === false);
check('prepare file exists', is_file($root . '/content/general_pages/industry_templates/_sub_industry_prepare.php'));

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
