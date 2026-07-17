<?php
/**
 * Smoke tests for /platform/industries sub-industry presentation.
 *
 *   php tests/erp_advanced/run_industries_hub_subs_tests.php
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
require_once $root . '/content/general_pages/epc_ecomae_platform_data.php';
require_once $root . '/content/general_pages/epc_industry_consolidation.php';
require_once $root . '/content/general_pages/epc_ecomae_platform_pages.php';

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

section('Helpers');
$cats = epc_industry_seo_template_sub_categories('jewellery', 'Precious metals refining');
check('jewellery refining cats', in_array('Gold Items', $cats, true) && in_array('Certification', $cats, true));

section('Industries page HTML');
$html = epc_ecomae_platform_page_industries();
check('has subhub section', strpos($html, 'id="sub-industries"') !== false);
check('has sub directory', strpos($html, 'id="subDirectory"') !== false && strpos($html, 'id="subDirSearch"') !== false);
check('featured precious metals', strpos($html, 'precious-metals-refining') !== false && stripos($html, 'Precious metals refining') !== false);
check('featured shows Gold Items', strpos($html, 'Gold Items') !== false);
check('expand tiles class', strpos($html, 'epm-sub-tile') !== false);
check('presentation pills', strpos($html, 'epm-pres-pill') !== false);
check('links to jewellery sub page', strpos($html, 'https://jewellery.ecomae.com/precious-metals-refining') !== false);
check('links to energy biomass', strpos($html, 'https://energy.ecomae.com/biomass-bioenergy') !== false);
check('hero mentions sub-industry pages', stripos($html, 'sub-industry') !== false);
check('browse all copy', stripos($html, 'Browse all sub-industries') !== false);
check('deep-link group support', strpos($html, "params.get('group')") !== false);
check('subdir items present', substr_count($html, 'epm-subdir__item') > 100);

section('Heading contrast on dark shell');
check('UAE/GCC section present', strpos($html, 'id="uae-gcc-industries"') !== false);
check('subhub h2 is light (not #0f172a)', preg_match('/\.epm-subhub__head h2\{[^}]*color:#f8fafc/', $html) === 1);
check('subhub h2 not dark slate', !preg_match('/\.epm-subhub__head h2\{[^}]*color:#0f172a/', $html));
check('subhub lead is light grey', preg_match('/\.epm-subhub__head p\{[^}]*color:#cbd5e1/', $html) === 1);

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
