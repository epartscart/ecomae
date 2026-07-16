<?php
/**
 * Smoke tests for industry subdomain + sub-industry SEO.
 *
 *   php tests/erp_advanced/run_industry_seo_tests.php
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
require_once $root . '/content/general_pages/epc_industry_consolidation.php';
require_once $root . '/content/general_pages/epc_industry_subdomain_router.php';

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

section('Host map + primary host');
check('energy host', epc_industry_seo_site_url_for_template('energy') === 'https://energy.ecomae.com');
check('food_beverage → food host', epc_industry_seo_site_url_for_template('food_beverage') === 'https://food.ecomae.com');
check('it_software → technology', epc_industry_seo_site_url_for_template('it_software') === 'https://technology.ecomae.com');
check('home_living → homeliving', epc_industry_seo_site_url_for_template('home_living') === 'https://homeliving.ecomae.com');
check('primary host energy', epc_industry_seo_primary_host('energy') === 'energy.ecomae.com');
check('primary host environmental→energy', epc_industry_seo_primary_host('environmental') === 'energy.ecomae.com');

section('Sub-industry slugs');
check('biomass slug', epc_industry_seo_sub_slug('Biomass & bioenergy') === 'biomass-bioenergy');
check('solar slug', epc_industry_seo_sub_slug('Solar energy') === 'solar-energy');

section('Aliases');
check('biomass alias → energy', epc_industry_subdomain_resolve_group('biomass') === 'energy');
check('bioenergy alias → energy', epc_industry_subdomain_resolve_group('bioenergy') === 'energy');
check('energy_utilities → energy', epc_industry_subdomain_resolve_group('energy_utilities') === 'energy');
check('hydrogen → energy', epc_industry_subdomain_resolve_group('hydrogen') === 'energy');

section('Template parse + sitemap');
$energySubs = epc_industry_seo_template_sub_industries('energy');
check('energy has biomass sub', in_array('Biomass & bioenergy', $energySubs, true));
check('energy has many subs', count($energySubs) >= 20);
$entries = epc_industry_seo_sitemap_entries();
$locs = array_map(static fn($e) => $e[0], $entries);
check('sitemap has energy hub', in_array('https://energy.ecomae.com/', $locs, true));
check('sitemap has biomass path', in_array('https://energy.ecomae.com/biomass-bioenergy', $locs, true));
check('sitemap has automotive hub', in_array('https://automotive.ecomae.com/', $locs, true));
check('sitemap entry count > 100', count($entries) > 100);

section('Request sub match');
$_SERVER['REQUEST_URI'] = '/biomass-bioenergy';
$m = epc_industry_seo_match_request_sub($energySubs);
check('match biomass-bioenergy', is_array($m) && ($m['label'] ?? '') === 'Biomass & bioenergy');
$_SERVER['REQUEST_URI'] = '/biomass';
$m2 = epc_industry_seo_match_request_sub($energySubs);
check('match short /biomass', is_array($m2) && ($m2['slug'] ?? '') === 'biomass-bioenergy');
$_SERVER['REQUEST_URI'] = '/';
check('hub has no active sub', epc_industry_seo_match_request_sub($energySubs) === null);

section('Template / wiring');
$base = (string) file_get_contents($root . '/content/general_pages/industry_templates/_base_template.php');
check('canonical uses seoCanonical', strpos($base, '$seoCanonical') !== false);
check('no demoKey canonical', strpos($base, 'canonical" href="https://<?php echo htmlspecialchars($demoKey)') === false);
check('JSON-LD item urls', strpos($base, '"url":') !== false && strpos($base, 'epc_industry_seo_sub_slug') !== false);
$router = (string) file_get_contents($root . '/content/general_pages/epc_ecomae_platform_router.php');
check('sitemap loads industry seo', strpos($router, 'epc_industry_seo_sitemap_entries') !== false);
check('industry sitemap branch', strpos($router, 'epc_industry_subdomain_active') !== false && strpos($router, 'biomass') !== false || strpos($router, 'epc_industry_seo_template_sub_industries') !== false);
$pages = (string) file_get_contents($root . '/content/general_pages/epc_ecomae_platform_pages.php');
check('industries hub uses seo site url', strpos($pages, 'epc_industry_seo_site_url_for_template') !== false);
$sm = (string) file_get_contents($root . '/sitemap-marketing.php');
check('sitemap-marketing includes industry abs urls', strpos($sm, 'epc_industry_seo_sitemap_entries') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
