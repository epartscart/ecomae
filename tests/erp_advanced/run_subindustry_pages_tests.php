<?php
/**
 * Smoke tests for dedicated sub-industry pages (distinct from industry hub).
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

section('Presentation variants');
$p1 = epc_industry_seo_sub_presentation('precious-metals-refining');
$p2 = epc_industry_seo_sub_presentation('biomass-bioenergy');
$p3 = epc_industry_seo_sub_presentation('solar-energy');
check('presentation has key', isset($p1['key'], $p1['tone']) && $p1['key'] !== '');
$keys = array('atelier', 'ledger', 'mosaic', 'dock');
check('key is known variant', in_array($p1['key'], $keys, true));
check('stable for same slug', epc_industry_seo_sub_presentation('precious-metals-refining')['key'] === $p1['key']);
$uniq = array_unique(array($p1['key'], $p2['key'], $p3['key']));
check('different slugs can differ', count($uniq) >= 2);

section('Render jewellery sub page');
$_SERVER['REQUEST_URI'] = '/precious-metals-refining';
$_SERVER['HTTP_HOST'] = 'jewellery.ecomae.com';
$GLOBALS['epc_industry_subdomain_active'] = true;
$GLOBALS['epc_industry_subdomain_slug'] = 'jewellery';
$GLOBALS['epc_industry_subdomain_group'] = 'jewellery';
ob_start();
include $root . '/content/general_pages/industry_templates/jewellery.php';
$html = (string) ob_get_clean();

check('uses sub page body class', strpos($html, 'isp-body') !== false);
check('not hub 3d body', strpos($html, 'ind-premium-3d') === false);
check('no 3d stage animation', strpos($html, 'ind3d-stage') === false && strpos($html, 'ind3d-canvas') === false);
check('title is sub-industry', stripos($html, 'Precious metals refining') !== false);
check('shows Gold Items category', strpos($html, 'Gold Items') !== false);
check('shows Diamond Pieces', strpos($html, 'Diamond Pieces') !== false);
check('shows Certification', strpos($html, 'Certification') !== false);
check('canonical sub path', strpos($html, 'jewellery.ecomae.com/precious-metals-refining') !== false);
check('presentation class present', (bool) preg_match('/isp-pres-(atelier|ledger|mosaic|dock)/', $html));
check('hub link back', strpos($html, 'jewellery.ecomae.com/') !== false);
check('no shared hub hero video wrap', strpos($html, 'heroVidWrap') === false && strpos($html, 'ind-hero-video') === false);

section('Render energy biomass sub page');
$_SERVER['REQUEST_URI'] = '/biomass-bioenergy';
$_SERVER['HTTP_HOST'] = 'energy.ecomae.com';
$GLOBALS['epc_industry_subdomain_slug'] = 'energy';
$GLOBALS['epc_industry_subdomain_group'] = 'energy';
ob_start();
include $root . '/content/general_pages/industry_templates/energy.php';
$html2 = (string) ob_get_clean();
check('energy sub page renders', strpos($html2, 'isp-body') !== false && stripos($html2, 'Biomass') !== false);
check('energy sub has categories section', strpos($html2, 'Product &amp; service categories') !== false || strpos($html2, 'id="categories"') !== false);

section('Hub still 3d when no sub path');
$_SERVER['REQUEST_URI'] = '/';
ob_start();
include $root . '/content/general_pages/industry_templates/jewellery.php';
$hub = (string) ob_get_clean();
check('hub keeps 3d skin', strpos($hub, 'ind-premium-3d') !== false);
check('hub not isp-body', strpos($hub, 'isp-body') === false);

section('Wiring');
$base = (string) file_get_contents($root . '/content/general_pages/industry_templates/_base_template.php');
check('base branches to sub page', strpos($base, '_sub_industry_page.php') !== false);
check('sub page file exists', is_file($root . '/content/general_pages/industry_templates/_sub_industry_page.php'));

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
