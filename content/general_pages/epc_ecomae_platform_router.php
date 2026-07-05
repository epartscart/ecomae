<?php
/**
 * URL routing for ecomae.com platform marketing pages.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_data.php';

/** Hostnames that serve platform marketing at / (no tenant storefront). */
function epc_ecomae_marketing_hostnames(): array
{
	return array('www.ecomae.com', 'ecomae.com');
}

function epc_ecomae_marketing_request_host(): string
{
	$host = '';
	if (!empty($_SERVER['HTTP_HOST'])) {
		$host = strtolower((string) $_SERVER['HTTP_HOST']);
	}
	if ($host !== '' && strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if ($host === '' && !empty($_SERVER['SERVER_NAME'])) {
		$host = strtolower((string) $_SERVER['SERVER_NAME']);
	}
	return $host;
}

function epc_ecomae_is_marketing_platform_host(string $host = null): bool
{
	if ($host === null) {
		$host = epc_ecomae_marketing_request_host();
	}
	if (in_array($host, epc_ecomae_marketing_hostnames(), true)) {
		return true;
	}
	// Industry wildcard subdomains also serve marketing
	if (function_exists('epc_is_industry_subdomain') && epc_is_industry_subdomain()) {
		return true;
	}
	return false;
}

function epc_ecomae_platform_route_state()
{
	static $state = null;
	if ($state === null) {
		$state = array('page' => null, 'params' => array(), 'handled' => false, 'demo_flash' => null);
	}
	return $state;
}

/**
 * Bare GET / on www.ecomae.com — must render marketing before ERP router (erp_only redirect).
 */
function epc_is_ecomae_marketing_root_request()
{
	if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET') {
		return false;
	}
	if (!epc_ecomae_is_marketing_platform_host()) {
		return false;
	}
	$path = epc_ecomae_platform_normalize_path($_SERVER['REQUEST_URI'] ?? '/');
	if ($path === '/cp' || strpos($path, '/cp/') === 0) {
		return false;
	}
	if (preg_match('#^/demo/[a-z0-9_]+(?:/|$)#', $path)) {
		return false;
	}
	return $path === '/' || $path === '/index.php';
}

/**
 * Standalone marketing homepage — no ERP, no dp_core, no-store (avoid CF 301 cache).
 */
function epc_render_ecomae_marketing_home_and_exit()
{
	epc_ecomae_platform_send_marketing_headers();
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_pages.php';
	echo epc_ecomae_platform_render_page('home', array());
	exit;
}

/**
 * Serve /sitemap.xml and /robots.txt for the marketing host. Returns true if it
 * emitted a response (caller should exit). Lightweight: no MySQL needed.
 */
function epc_ecomae_marketing_serve_seo_file()
{
	if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET' || !epc_ecomae_is_marketing_platform_host()) {
		return false;
	}
	$path = epc_ecomae_platform_normalize_path($_SERVER['REQUEST_URI'] ?? '/');
	$base = rtrim(epc_ecomae_platform_base_url(), '/');

	if ($path === '/robots.txt') {
		if (function_exists('epc_ecomae_platform_send_marketing_headers')) {
			epc_ecomae_platform_send_marketing_headers();
		}
		header('Content-Type: text/plain; charset=utf-8');
		echo "User-agent: *\nAllow: /\nDisallow: /cp/\nDisallow: /erp/\n\nSitemap: " . $base . "/sitemap.xml\n";
		return true;
	}

	if ($path === '/sitemap.xml') {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_marketing_content.php';
		// core marketing pages with crawl priority + change cadence
		$urls = array(
			array('/', '1.0', 'daily'),
			array('/platform', '0.9', 'weekly'),
			array('/platform/capabilities', '0.8', 'weekly'),
			array('/platform/free-tools', '0.9', 'weekly'),
			array('/platform/industries', '0.8', 'weekly'),
			array('/platform/pricing', '0.8', 'weekly'),
			array('/platform/demo', '0.8', 'weekly'),
			array('/platform/customer-results', '0.7', 'weekly'),
			array('/platform/about', '0.6', 'monthly'),
			array('/platform/contact', '0.6', 'monthly'),
			array('/platform/business-continuity', '0.6', 'monthly'),
			array('/platform/platform-guides', '0.6', 'monthly'),
			array('/platform/api-documentation', '0.6', 'monthly'),
			array('/platform/api-services', '0.6', 'monthly'),
			array('/platform/auto-price-ai', '0.6', 'monthly'),
			array('/platform/faq', '0.7', 'weekly'),
			array('/documentation', '0.7', 'weekly'),
			array('/compare', '0.7', 'weekly'),
			array('/bos', '0.7', 'weekly'),
			array('/solutions', '0.7', 'weekly'),
		);
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_free_tools.php';
		foreach (array_keys(epc_free_tools_catalog()) as $tk) { $urls[] = array('/platform/free-tools/' . $tk, '0.8', 'monthly'); }
		foreach (array_keys(epc_ecomae_docs_catalog()) as $s) { $urls[] = array('/documentation/' . $s, '0.6', 'monthly'); }
		foreach (array_keys(epc_ecomae_compare_catalog()) as $s) { $urls[] = array('/compare/' . $s, '0.6', 'monthly'); }
		foreach (array_keys(epc_ecomae_bos_articles_catalog()) as $s) { $urls[] = array('/bos/' . $s, '0.6', 'monthly'); }
		foreach (array_keys(epc_ecomae_solutions_catalog()) as $s) { $urls[] = array('/solutions/' . $s, '0.7', 'monthly'); }
		if (function_exists('epc_ecomae_platform_industry_marketing')) {
			foreach (array_keys(epc_ecomae_platform_industry_marketing()) as $code) {
				$urls[] = array('/platform/industry/' . $code, '0.7', 'monthly');
			}
		}
		if (function_exists('epc_ecomae_platform_send_marketing_headers')) {
			epc_ecomae_platform_send_marketing_headers();
		}
		header('Content-Type: application/xml; charset=utf-8');
		$now = date('Y-m-d');
		echo '<?xml version="1.0" encoding="UTF-8"?>' . "\n";
		echo '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">' . "\n";
		foreach ($urls as $u) {
			echo '<url><loc>' . htmlspecialchars($base . $u[0], ENT_QUOTES) . '</loc>'
				. '<lastmod>' . $now . '</lastmod>'
				. '<changefreq>' . $u[2] . '</changefreq>'
				. '<priority>' . $u[1] . '</priority></url>' . "\n";
		}
		echo '</urlset>' . "\n";
		return true;
	}

	return false;
}

function epc_ecomae_platform_normalize_path($path)
{
	$path = (string) $path;
	$path = parse_url($path, PHP_URL_PATH);
	if ($path === false || $path === null) {
		$path = '/';
	}
	$path = '/' . trim(str_replace('\\', '/', $path), '/');
	if ($path === '//') {
		$path = '/';
	}
	return $path;
}

function epc_ecomae_platform_match_path($path)
{
	$path = epc_ecomae_platform_normalize_path($path);
	if ($path === '/' || $path === '/index.php') {
		return array('page' => 'home', 'params' => array());
	}
	// Marketing / SEO + AI-visibility content (top-level, not under /platform).
	if (preg_match('#^/documentation(?:/([a-z0-9\-]+))?$#', $path, $m)) {
		return array('page' => 'docs', 'params' => array('slug' => $m[1] ?? ''));
	}
	if (preg_match('#^/compare(?:/([a-z0-9\-]+))?$#', $path, $m)) {
		return array('page' => 'compare', 'params' => array('slug' => $m[1] ?? ''));
	}
	if (preg_match('#^/bos(?:/([a-z0-9\-]+))?$#', $path, $m)) {
		return array('page' => 'bos', 'params' => array('slug' => $m[1] ?? ''));
	}
	if (preg_match('#^/solutions(?:/([a-z0-9\-]+))?$#', $path, $m)) {
		return array('page' => 'solution', 'params' => array('slug' => $m[1] ?? ''));
	}
	if (!preg_match('#^/platform(?:/|$)#', $path)) {
		return null;
	}
	$rest = trim(substr($path, strlen('/platform')), '/');
	if ($rest === '') {
		return array('page' => 'platform', 'params' => array());
	}
	if ($rest === 'pricing') {
		return array('page' => 'pricing', 'params' => array());
	}
	if ($rest === 'customer-results' || $rest === 'customer-testimonials') {
		return array('page' => 'customer_results', 'params' => array());
	}
	if ($rest === 'demo') {
		return array('page' => 'demo', 'params' => array());
	}
	if ($rest === 'contact') {
		return array('page' => 'contact', 'params' => array());
	}
	if ($rest === 'about') {
		return array('page' => 'about', 'params' => array());
	}
	if ($rest === 'industries') {
		return array('page' => 'industries', 'params' => array());
	}
	if ($rest === 'business-continuity') {
		return array('page' => 'business_continuity', 'params' => array());
	}
	if ($rest === 'platform-guides') {
		return array('page' => 'platform_guides', 'params' => array());
	}
	if ($rest === 'capabilities') {
		return array('page' => 'capabilities', 'params' => array());
	}
	if ($rest === 'api-documentation') {
		return array('page' => 'api_documentation', 'params' => array());
	}
	if ($rest === 'auto-price-ai') {
		return array('page' => 'auto_price_ai', 'params' => array());
	}
	if ($rest === 'faq') {
		return array('page' => 'faq', 'params' => array());
	}
	if ($rest === 'api-services' || $rest === 'catalog-api' || $rest === 'price-pro-api') {
		$focus = 'overview';
		if ($rest === 'catalog-api') {
			$focus = 'catalog';
		} elseif ($rest === 'price-pro-api') {
			$focus = 'price_pro';
		}
		return array('page' => 'api_services', 'params' => array('focus' => $focus));
	}
	if ($rest === 'free-tools' || $rest === 'tools') {
		return array('page' => 'free_tools', 'params' => array());
	}
	if (preg_match('#^free-tools/([a-z]+)$#', $rest, $m)) {
		return array('page' => 'free_tools', 'params' => array('tool' => $m[1]));
	}
	if (preg_match('#^industry/([a-z0-9_]+)$#', $rest, $m)) {
		return array('page' => 'industry', 'params' => array('code' => $m[1]));
	}
	return null;
}

function epc_ecomae_platform_init_route()
{
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return null;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return null;
	}
	$state = epc_ecomae_platform_route_state();
	$match = epc_ecomae_platform_match_path($_SERVER['REQUEST_URI'] ?? '/');
	if ($match !== null) {
		$state['page'] = $match['page'];
		$state['params'] = $match['params'];
	}
	return $state['page'];
}

function epc_ecomae_platform_current_page()
{
	$state = epc_ecomae_platform_route_state();
	return $state['page'];
}

function epc_ecomae_platform_should_render()
{
	$page = epc_ecomae_platform_current_page();
	return $page !== null && $page !== 'home';
}

/**
 * Called from dp_core when CMS has no page — attach platform marketing instead of 404.
 */
function epc_ecomae_platform_absorb_route($urlRoute, $DP_Content, $isFrontMode)
{
	if (!empty($GLOBALS['epc_demo_storefront_context'])) {
		return false;
	}
	if ((int) $isFrontMode !== 1) {
		return false;
	}
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return false;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}

	$path = '/' . trim((string) $urlRoute, '/');
	if ($path === '/') {
		return false;
	}
	$match = epc_ecomae_platform_match_path($path);
	if ($match === null) {
		return false;
	}

	// BOS app: only intercept /bos (no slug) — /bos/article-slug stays as marketing content
	if ($match['page'] === 'bos' && empty($match['params']['slug'])) {
		$bosApp = $_SERVER['DOCUMENT_ROOT'] . '/bos/index.php';
		if (file_exists($bosApp)) {
			require $bosApp;
			exit;
		}
	}

	$state = epc_ecomae_platform_route_state();
	$state['page'] = $match['page'];
	$state['params'] = $match['params'];
	$state['handled'] = true;

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_pages.php';

	$titles = array(
		'platform' => 'Platform — ECOM AE',
		'platform_guides' => 'Super CP guides — ECOM AE',
		'capabilities' => 'Super CP capabilities — ECOM AE',
		'api_documentation' => 'API documentation — ECOM AE',
		'auto_price_ai' => 'Auto Price AI — discover, price, and list — ECOM AE',
		'faq' => 'FAQ — platform questions answered — ECOM AE',
		'api_services' => 'Catalog & Price API services — ECOM AE',
		'customer_results' => 'Customer results — ECOM AE',
		'business_continuity' => 'Cloud + backup continuity — ECOM AE',
		'industries' => 'Industries — ECOM AE',
		'pricing' => 'Monthly rental plans',
		'demo' => '3-day industry demo',
		'contact' => 'Contact ecomae',
		'free_tools' => 'Free business tools — ECOM AE',
		'industry' => 'Industry solution',
	);
	$title = isset($titles[$match['page']]) ? $titles[$match['page']] : 'ecomae platform';
	if ($match['page'] === 'industry') {
		$industries = epc_ecomae_platform_industry_marketing();
		$code = (string) ($match['params']['code'] ?? '');
		if (isset($industries[$code])) {
			$title = $industries[$code]['name'] . ' — ecomae';
		}
	}
	$mktDesc = '';
	if (in_array($match['page'], array('docs', 'compare', 'bos', 'solution'), true)
		&& function_exists('epc_ecomae_marketing_meta')) {
		$meta = epc_ecomae_marketing_meta($match['page'], $match['params']);
		if ($meta) {
			$title = $meta[0];
			$mktDesc = $meta[1];
		}
	}

	$DP_Content->content_type = 'text';
	$DP_Content->value = $title;
	$DP_Content->title_tag = $title;
	$DP_Content->main_flag = false;
	if ($mktDesc !== '') {
		$DP_Content->meta_description = $mktDesc;
		$DP_Content->description = $mktDesc;
	}
	$DP_Content->content = epc_ecomae_platform_render_page($match['page'], $match['params'], 'inner');
	unset($DP_Content->service_data['error_page']);
	$DP_Content->service_data['epc_platform_marketing'] = true;
	$DP_Content->alternative_flag = false;

	return true;
}

function epc_ecomae_platform_demo_flash()
{
	$state = epc_ecomae_platform_route_state();
	return $state['demo_flash'];
}

function epc_ecomae_platform_page_title($page, array $params = array())
{
	$titles = array(
		'home' => 'ECOM AE — Unified ERP & Commerce Cloud',
		'platform' => 'Platform — ECOM AE',
		'platform_guides' => 'Super CP guides — ECOM AE',
		'capabilities' => 'Super CP capabilities — ECOM AE',
		'api_documentation' => 'API documentation — ECOM AE',
		'auto_price_ai' => 'Auto Price AI — discover, price, and list — ECOM AE',
		'faq' => 'FAQ — platform questions answered — ECOM AE',
		'api_services' => 'Catalog & Price API services — ECOM AE',
		'customer_results' => 'Customer results — ECOM AE',
		'business_continuity' => 'Cloud + backup continuity — ECOM AE',
		'industries' => 'Industries — ECOM AE',
		'pricing' => 'Monthly rental plans',
		'demo' => '3-day industry demo',
		'contact' => 'Contact ecomae',
		'free_tools' => 'Free business tools — ECOM AE',
		'industry' => 'Industry solution',
	);
	$title = isset($titles[$page]) ? $titles[$page] : 'ecomae platform';
	if ($page === 'industry') {
		$industries = epc_ecomae_platform_industry_marketing();
		$code = (string) ($params['code'] ?? '');
		if (isset($industries[$code])) {
			$title = $industries[$code]['name'] . ' — ecomae';
		}
	}
	if ($page === 'free_tools') {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_free_tools.php';
		$seo = epc_free_tools_seo((string) ($params['tool'] ?? ''));
		$title = $seo['title'];
	}
	return $title;
}

function epc_ecomae_platform_send_marketing_headers()
{
	if (headers_sent()) {
		return;
	}
	header('Content-Type: text/html; charset=utf-8');
	header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
	header('Pragma: no-cache');
	header('Expires: 0');
	header('X-ECOMAE-Marketing-Home: v2');
}

/**
 * Redirect orphaned docpart CMS/shop pages on www.ecomae.com (cloned DB leftovers).
 * e.g. /en/akciya — Russian "promotions" slug showing wrong tenant storefront.
 */
function epc_ecomae_marketing_redirect_legacy_cms_path()
{
	if (!empty($GLOBALS['epc_demo_storefront_context'])) {
		return false;
	}
	if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET') {
		return false;
	}
	if (!epc_ecomae_is_marketing_platform_host()) {
		return false;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}

	$path = epc_ecomae_platform_normalize_path($_SERVER['REQUEST_URI'] ?? '/');
	if ($path === '/' || $path === '/index.php') {
		return false;
	}
	if (preg_match('#^/platform(?:/|$)#', $path)) {
		return false;
	}
	if (preg_match('#^/cp(?:/|$)#', $path)) {
		return false;
	}
	if (preg_match('#^/demo(?:/|$)#', $path)) {
		return false;
	}
	if (preg_match('#^/epc-api(?:/|$)#', $path) || preg_match('#^/api(?:/|$)#', $path)) {
		return false;
	}
	if (preg_match('#^/(en|ru|ar)/?$#i', $path)) {
		return false;
	}

	$shouldRedirect = false;
	if (preg_match('#^/(en|ru|ar)/.+#i', $path)) {
		$shouldRedirect = true;
	}
	$legacyBareSlugs = array('akciya', 'aktsiya', 'akcia', 'promotion', 'promotions');
	$bare = strtolower(trim($path, '/'));
	if (in_array($bare, $legacyBareSlugs, true)) {
		$shouldRedirect = true;
	}
	if (preg_match('#^/(akciya|aktsiya|akcia|promotion|promotions)(?:/|$)#i', $path)) {
		$shouldRedirect = true;
	}
	if (!$shouldRedirect) {
		return false;
	}
	if (headers_sent()) {
		return false;
	}
	header('Location: /', true, 301);
	header('X-Robots-Tag: noindex');
	return true;
}

/**
 * Early exit for www.ecomae.com platform marketing — call from index.php before ERP router.
 */
function epc_ecomae_platform_try_exit_standalone($path = null)
{
	if (!empty($GLOBALS['epc_demo_storefront_context'])) {
		return false;
	}
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return false;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}
	if ($path === null) {
		$path = $_SERVER['REQUEST_URI'] ?? '/';
	}
	if (!epc_ecomae_platform_render_standalone($path)) {
		return false;
	}
	exit;
}

/**
 * Render platform marketing without storefront template (ecomae DB has no frontend skin).
 */
function epc_ecomae_platform_render_standalone($path = null)
{
	if (!empty($GLOBALS['epc_demo_storefront_context'])) {
		return false;
	}
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return false;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}
	if ($path === null) {
		$path = $_SERVER['REQUEST_URI'] ?? '/';
	}
	$match = epc_ecomae_platform_match_path($path);
	if ($match === null) {
		return false;
	}
	// BOS app: only intercept /bos (no slug) — /bos/article-slug stays as marketing content
	if ($match['page'] === 'bos' && empty($match['params']['slug'])) {
		$bosApp = $_SERVER['DOCUMENT_ROOT'] . '/bos/index.php';
		if (file_exists($bosApp)) {
			require $bosApp;
			return true;
		}
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_pages.php';
	epc_ecomae_platform_send_marketing_headers();
	echo epc_ecomae_platform_render_page($match['page'], $match['params']);
	return true;
}

function epc_ecomae_platform_process_demo_post()
{
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return null;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return null;
	}
	$state = epc_ecomae_platform_route_state();
	if ($state['demo_flash'] !== null) {
		return $state['demo_flash'];
	}
	$flash = epc_ecomae_platform_handle_demo_post();
	if ($flash !== null) {
		$state['demo_flash'] = $flash;
	}
	return $flash;
}

function epc_ecomae_platform_handle_demo_post()
{
	if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST' || empty($_POST['epc_demo_request'])) {
		return null;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	$pdo = epc_portal_demo_platform_pdo();
	if (!$pdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Platform unavailable — try again shortly.');
	}
	$params = array_merge($_POST, array('terms' => !empty($_POST['accept_terms']) || !empty($_POST['terms'])));
	$result = epc_portal_demo_provision($pdo, $params);
	if (empty($result['ok'])) {
		return array('ok' => false, 'message' => (string) ($result['message'] ?? 'Provision failed'));
	}
	return array(
		'ok' => true,
		'message' => (string) ($result['message'] ?? 'Demo provisioned — check your email.'),
		'storefront' => $result['urls']['storefront'] ?? '',
		'site_key' => $result['site_key'] ?? '',
	);
}
