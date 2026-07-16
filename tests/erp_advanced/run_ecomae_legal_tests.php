<?php
/**
 * Smoke tests for ECOM AE public legal policies.
 *
 *   php tests/erp_advanced/run_ecomae_legal_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/general_pages/epc_ecomae_platform_data.php';
require_once $root . '/content/general_pages/epc_ecomae_legal_content.php';
require_once $root . '/content/general_pages/epc_ecomae_legal_pages.php';

if (!function_exists('epc_ecomae_marketing_crumb')) {
	function epc_ecomae_marketing_crumb(string $label, string $hubUrl, string $hubLabel): string
	{
		return $hubLabel . ' / ' . $label;
	}
}
if (!function_exists('epc_ecomae_marketing_article_jsonld')) {
	function epc_ecomae_marketing_article_jsonld(string $headline, string $desc, string $url): string
	{
		return '';
	}
}

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

section('Catalog');
$cat = epc_ecomae_legal_catalog();
$required = array(
	'privacy', 'terms', 'cookie-policy', 'security-policy', 'right-to-use',
	'trademark', 'copyright', 'data-protection', 'acceptable-use',
	'confidentiality', 'intellectual-property', 'blockchain-disclaimer', 'dmca',
);
check('catalog has 13 policies', count($cat) >= 13);
foreach ($required as $slug) {
	check('policy exists: ' . $slug, isset($cat[$slug]) && $cat[$slug]['title'] !== '' && !empty($cat[$slug]['sections']));
}

section('Aliases');
$aliases = epc_ecomae_legal_top_level_aliases();
check('privacy alias', ($aliases['/privacy'] ?? '') === 'privacy');
check('terms alias', ($aliases['/terms'] ?? '') === 'terms');
check('security-policy alias', ($aliases['/security-policy'] ?? '') === 'security-policy');
check('en/privacy alias', ($aliases['/en/privacy'] ?? '') === 'privacy');

section('Meta + render');
$hubMeta = epc_ecomae_legal_meta(array());
check('hub meta title', strpos($hubMeta[0], 'Legal') !== false);
$privMeta = epc_ecomae_legal_meta(array('slug' => 'privacy'));
check('privacy meta title', strpos($privMeta[0], 'Privacy') !== false);
check('canonical hub', epc_ecomae_legal_canonical_path(array()) === '/legal');
check('canonical privacy', epc_ecomae_legal_canonical_path(array('slug' => 'privacy')) === '/legal/privacy');

$hubHtml = epc_ecomae_platform_page_legal(array());
check('hub lists Privacy Policy', strpos($hubHtml, 'Privacy Policy') !== false && strpos($hubHtml, '/legal/privacy') !== false);
check('hub lists Trademark', strpos($hubHtml, 'Trademark Policy') !== false);
$secHtml = epc_ecomae_platform_page_legal(array('slug' => 'security-policy'));
check('security page renders sections', strpos($secHtml, 'Responsible disclosure') !== false);
check('policy page has all-policies nav', strpos($secHtml, 'All policies') !== false);

section('Router + footer wiring');
$router = (string) file_get_contents($root . '/content/general_pages/epc_ecomae_platform_router.php');
check('router matches /legal', strpos($router, "preg_match('#^/legal") !== false);
check('router loads legal aliases', strpos($router, 'epc_ecomae_legal_top_level_aliases') !== false);
check('sitemap includes /legal', strpos($router, "array('/legal'") !== false);

$layout = (string) file_get_contents($root . '/content/general_pages/epc_ecomae_platform_layout.php');
check('footer legal section', strpos($layout, 'epm-footer__legal') !== false);
check('footer privacy link', strpos($layout, 'privacy">Privacy') !== false);
check('footer security link', strpos($layout, 'security-policy">Security') !== false);
check('footer trademark link', strpos($layout, 'trademark">Trademark') !== false);
check('footer right-to-use link', strpos($layout, 'right-to-use">Right to use') !== false);
check('footer data-protection link', strpos($layout, 'data-protection">Data protection') !== false);
check('footer all policies link', strpos($layout, 'legal">All policies') !== false || strpos($layout, 'legal">Legal policies') !== false);

$pages = (string) file_get_contents($root . '/content/general_pages/epc_ecomae_platform_pages.php');
check('pages require legal_pages', strpos($pages, 'epc_ecomae_legal_pages.php') !== false);
check('canonical handles legal', strpos($pages, "page === 'legal'") !== false || strpos($pages, "'legal'") !== false);

$nav = (string) file_get_contents($root . '/content/general_pages/epc_ecomae_platform_data.php');
check('nav resources has legal', strpos($nav, "'legal'") !== false && strpos($nav, 'Legal policies') !== false);

$sm = (string) file_get_contents($root . '/sitemap-marketing.php');
check('sitemap-marketing has /legal', strpos($sm, "'/legal'") !== false);
check('sitemap-marketing loads legal catalog', strpos($sm, 'epc_ecomae_legal_catalog') !== false);

require_once $root . '/content/general_pages/epc_ecomae_platform_router.php';
$mLegal = epc_ecomae_platform_match_path('/legal');
check('match /legal', is_array($mLegal) && ($mLegal['page'] ?? '') === 'legal' && ($mLegal['params']['slug'] ?? 'x') === '');
$mPriv = epc_ecomae_platform_match_path('/privacy');
check('match /privacy alias', is_array($mPriv) && ($mPriv['page'] ?? '') === 'legal' && ($mPriv['params']['slug'] ?? '') === 'privacy');
$mTm = epc_ecomae_platform_match_path('/trademark');
check('match /trademark alias', is_array($mTm) && ($mTm['params']['slug'] ?? '') === 'trademark');
$mSlug = epc_ecomae_platform_match_path('/legal/data-protection');
check('match /legal/data-protection', is_array($mSlug) && ($mSlug['params']['slug'] ?? '') === 'data-protection');

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
