<?php
/**
 * Standalone ERP portal — /erp, /shop/erp (no storefront header/footer).
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array{page:string,lang_prefix:string,path_after:string}|null
 */
function epc_erp_portal_match_request($requestUri = null)
{
	$path = parse_url($requestUri !== null ? $requestUri : ($_SERVER['REQUEST_URI'] ?? '/'), PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		$path = '/';
	}
	$path = '/' . trim(str_replace('\\', '/', $path), '/');
	if ($path === '//') {
		$path = '/';
	}

	$langPrefix = '';
	if (preg_match('#^/(en|ru|ar)(/|$)#i', $path, $lm)) {
		$langPrefix = '/' . strtolower($lm[1]);
		$path = substr($path, strlen($langPrefix));
		if ($path === '') {
			$path = '/';
		}
	}

	if ($path === '/erp/ajax' || $path === '/erp/ajax/') {
		return array('page' => 'ajax', 'lang_prefix' => $langPrefix, 'path_after' => '');
	}
	if ($path === '/erp/guide' || $path === '/erp/guide/') {
		return array('page' => 'guide', 'lang_prefix' => $langPrefix, 'path_after' => 'guide');
	}
	if ($path === '/erp' || $path === '/erp/') {
		return array('page' => 'main', 'lang_prefix' => $langPrefix, 'path_after' => '');
	}
	if ($path === '/shop/erp/guide' || $path === '/shop/erp/guide/') {
		return array('page' => 'guide', 'lang_prefix' => $langPrefix, 'path_after' => 'guide');
	}
	if ($path === '/shop/erp' || $path === '/shop/erp/') {
		return array('page' => 'main', 'lang_prefix' => $langPrefix, 'path_after' => '');
	}
	if ($path === '/erp-demo' || $path === '/erp-demo/'
		|| $path === '/shop/erp-demo' || $path === '/shop/erp-demo/') {
		return array('page' => 'demo', 'lang_prefix' => $langPrefix, 'path_after' => '');
	}

	return null;
}

function epc_erp_portal_canonical_base($langPrefix = '')
{
	global $DP_Config;
	$base = rtrim((string) ($DP_Config->domain_path ?? ''), '/');
	return $base . ($langPrefix !== '' ? $langPrefix : '') . '/erp';
}

function epc_erp_portal_bootstrap_pdo()
{
	global $DP_Config;
	if (!isset($DP_Config) || !($DP_Config instanceof DP_Config)) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$DP_Config = new DP_Config();
		$GLOBALS['DP_Config'] = $DP_Config;
	}
	if (function_exists('epc_portal_apply_config')) {
		epc_portal_apply_config($DP_Config);
	}
	return new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

function epc_erp_portal_handle_ajax(PDO $db_link)
{
	if (ob_get_level()) {
		ob_end_clean();
	}
	header('Content-Type: application/json; charset=utf-8');
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
	if (!epc_erp_user_can_access($db_link)) {
		echo json_encode(array('status' => false, 'message' => 'Access denied'));
		exit;
	}
	$endpoint = $_SERVER['DOCUMENT_ROOT'] . '/' . (isset($GLOBALS['DP_Config']->backend_dir) ? $GLOBALS['DP_Config']->backend_dir : 'cp')
		. '/content/shop/finance/erp/ajax_erp_endpoint.php';
	if (is_file($endpoint)) {
		$GLOBALS['db_link'] = $db_link;
		require $endpoint;
		exit;
	}
	require $_SERVER['DOCUMENT_ROOT'] . '/' . (isset($GLOBALS['DP_Config']->backend_dir) ? $GLOBALS['DP_Config']->backend_dir : 'cp')
		. '/content/shop/finance/erp/ajax_erp.php';
	exit;
}

function epc_erp_portal_render_shell($innerCallback, array $opts = array())
{
	global $DP_Config;
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
	$pageTitle = isset($opts['title']) ? (string) $opts['title'] : 'ERP Finance';
	$bodyClass = isset($opts['body_class']) ? (string) $opts['body_class'] : 'epc-erp-standalone';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
	$brand = epc_brand_cp_context();
	$backend = htmlspecialchars((string) ($DP_Config->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');
	$langHref = function_exists('epc_erp_lang_href') ? epc_erp_lang_href() : '';
	$erpBase = htmlspecialchars(epc_erp_portal_canonical_base($langHref), ENT_QUOTES, 'UTF-8');
	$guideUrl = $erpBase . '/guide';
	$cpUrl = '/' . $backend . '/shop/finance/erp?epc_erp_shell=1';
	$showStore = function_exists('epc_portal_storefront_enabled') && epc_portal_storefront_enabled();
	$platformHost = function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname();
	$cssVer = '20260530a';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
	?>
<!DOCTYPE html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title><?php echo htmlspecialchars($pageTitle . ' — ' . ($brand['product_name'] ?? 'ERP'), ENT_QUOTES, 'UTF-8'); ?></title>
	<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css" />
	<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.4.1/css/bootstrap.min.css" />
	<link rel="stylesheet" href="/<?php echo $backend; ?>/templates/bootstrap_admin/css/epc_cp_ui.css?v=<?php echo $cssVer; ?>" />
	<link rel="stylesheet" href="/content/shop/finance/epc_erp_portal.css?v=<?php echo $cssVer; ?>" />
	<link rel="stylesheet" href="/content/shop/finance/epc_erp_professional.css?v=<?php echo $cssVer; ?>" />
	<?php epc_ecomae_hub_logo_enqueue(); ?>
</head>
<body class="<?php echo htmlspecialchars($bodyClass, ENT_QUOTES, 'UTF-8'); ?>">
<header class="epc-erp-topbar">
	<div class="epc-erp-topbar__inner">
		<a class="epc-erp-topbar__brand epc-erp-topbar__brand--hub" href="<?php echo $erpBase; ?>">
			<?php echo epc_ecomae_static_logo('compact', array('show_title' => false, 'show_tagline' => false, 'aria_label' => 'ECOM AE')); ?>
			<span class="epc-erp-topbar__brand-text">
				<strong><?php echo htmlspecialchars((string) ($brand['product_name'] ?? 'ERP'), ENT_QUOTES, 'UTF-8'); ?></strong>
				<small><?php echo htmlspecialchars((string) ($brand['hub_tagline'] ?? 'Finance & operations'), ENT_QUOTES, 'UTF-8'); ?></small>
			</span>
		</a>
		<nav class="epc-erp-topbar__nav">
			<a href="<?php echo $erpBase; ?>"><i class="fa fa-dashboard"></i> ERP</a>
			<a href="<?php echo htmlspecialchars($guideUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-book"></i> Guide</a>
			<a href="<?php echo htmlspecialchars($cpUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-cog"></i> Control panel (advanced)</a>
			<?php if ($showStore): ?>
			<a href="/"><i class="fa fa-globe"></i> Website</a>
			<?php endif; ?>
			<?php if ($platformHost): ?>
			<a href="https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub"><i class="fa fa-cloud"></i> Super CP</a>
			<?php endif; ?>
		</nav>
	</div>
</header>
<main class="epc-erp-main">
	<?php
	if (is_callable($innerCallback)) {
		$innerCallback();
	}
	?>
</main>
<footer class="epc-erp-foot">
	<?php
	$hostedBy = epc_brand_hosted_by_html();
	echo $hostedBy;
	if ($platformHost && trim(strip_tags($hostedBy)) === '') {
		echo '<span class="epc-erp-platform-foot">&copy; ' . date('Y') . ' Electronic World Group &middot; '
			. '<a href="https://www.ecomae.com/platform">ECOM AE platform</a> &middot; Dubai, UAE</span>';
	}
	?>
</footer>
<script src="https://cdnjs.cloudflare.com/ajax/libs/jquery/3.6.0/jquery.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.4.1/js/bootstrap.min.js"></script>
</body>
</html>
	<?php
}

function epc_erp_portal_render_page(PDO $db_link, $page)
{
	global $DP_Config;
	$GLOBALS['db_link'] = $db_link;
	$epc_erp_portal = 'frontend';
	$epc_erp_standalone = true;

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
	epc_erp_portal_ensure_guest_session($db_link);
	epc_erp_portal_handle_auth_post($db_link);
	if (!isset($multilang_params) || !is_array($multilang_params)) {
		$multilang_params = multilang_init();
	}

	if ($page === 'demo') {
		epc_erp_portal_render_shell(function () {
			require $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/erp_demo_dashboard.php';
		}, array('title' => 'Live ERP demo', 'body_class' => 'epc-erp-standalone epc-erp-standalone--demo'));
		return;
	}

	$title = ($page === 'guide') ? 'ERP guide' : 'ERP Finance';

	epc_erp_portal_render_shell(function () use ($db_link, $page, $DP_Config) {
		if ($page === 'guide') {
			require $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_portal_guide.php';
			return;
		}
		require $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_portal_body.php';
	}, array('title' => $title, 'body_class' => 'epc-erp-standalone' . ($page === 'guide' ? ' epc-erp-standalone--guide' : '')));
}

/**
 * Try to serve standalone ERP. Returns true if request was handled.
 */
function epc_erp_portal_is_platform_marketing_root()
{
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return false;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}
	$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		$path = '/';
	}
	$path = '/' . trim(str_replace('\\', '/', $path), '/');
	if ($path === '//') {
		$path = '/';
	}
	return $path === '/' || $path === '/index.php' || preg_match('#^/(en|ru|ar)/?$#i', $path);
}

function epc_erp_portal_try_route()
{
	if (epc_erp_portal_is_platform_marketing_root()) {
		return false;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}
	if (($_SERVER['REQUEST_METHOD'] ?? '') === 'GET' && isset($_SERVER['REQUEST_URI'])) {
		$pathOnly = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
		if (is_string($pathOnly) && preg_match('#^/cp(?:/|$)#', $pathOnly)) {
			return false;
		}
	}

	$match = epc_erp_portal_match_request();
	if ($match === null) {
		return false;
	}

	try {
		$db_link = epc_erp_portal_bootstrap_pdo();
	} catch (Throwable $e) {
		http_response_code(500);
		header('Content-Type: text/plain; charset=utf-8');
		echo 'ERP portal: database connection failed.';
		return true;
	}

	if ($match['page'] === 'ajax') {
		if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' || isset($_GET['action'])) {
			epc_erp_portal_handle_ajax($db_link);
		}
		http_response_code(405);
		header('Content-Type: application/json; charset=utf-8');
		echo json_encode(array('status' => false, 'message' => 'POST required'));
		return true;
	}

	// Public live-demo dashboard (no login). Rendered before the /shop/erp
	// canonical redirect since /shop/erp-demo also contains "/shop/erp".
	if ($match['page'] === 'demo') {
		epc_erp_portal_render_page($db_link, 'demo');
		return true;
	}

	// Canonical short URL: redirect /shop/erp → /erp
	$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
	if (is_string($path) && (strpos($path, '/shop/erp') !== false)) {
		$target = epc_erp_portal_canonical_base($match['lang_prefix']);
		if (strpos($path, '/guide') !== false) {
			$target .= '/guide';
		}
		$qs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== '' ? ('?' . $_SERVER['QUERY_STRING']) : '';
		header('Location: ' . $target . $qs, true, 301);
		return true;
	}

	epc_erp_portal_render_page($db_link, $match['page']);
	return true;
}

/**
 * ERP-only tenants: send bare homepage to ERP portal.
 */
function epc_erp_portal_maybe_redirect_home()
{
	if (epc_erp_portal_is_platform_marketing_root()) {
		return false;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}
	if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()) {
		return false;
	}
	if (function_exists('epc_portal_home_mode') && epc_portal_home_mode() === 'platform') {
		return false;
	}
	if (!function_exists('epc_portal_storefront_enabled') || epc_portal_storefront_enabled()) {
		return false;
	}
	if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET') {
		return false;
	}
	$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
	if (!is_string($path)) {
		return false;
	}
	$path = '/' . trim($path, '/');
	if ($path !== '/' && $path !== '/index.php' && !preg_match('#^/(en|ru|ar)/?$#i', $path)) {
		return false;
	}
	$langPrefix = '';
	if (preg_match('#^/(en|ru|ar)/?$#i', parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH) ?: '/', $lm)) {
		$langPrefix = '/' . strtolower($lm[1]);
	}
	header('Location: ' . epc_erp_portal_canonical_base($langPrefix), true, 302);
	return true;
}

/**
 * Consultancy / advisory tenants: redirect commerce shop URLs to the public landing.
 */
function epc_erp_portal_block_consultancy_commerce_path()
{
	if (!function_exists('epc_portal_commerce_storefront_enabled') || epc_portal_commerce_storefront_enabled()) {
		return false;
	}
	if (!function_exists('epc_portal_storefront_enabled') || !epc_portal_storefront_enabled()) {
		return false;
	}
	$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
	if (!is_string($path)) {
		return false;
	}
	if (epc_erp_portal_match_request() !== null) {
		return false;
	}
	if (preg_match('#^/cp(?:/|$)#', $path) || preg_match('#/cp(?:/|$)#', $path)) {
		return false;
	}
	if (preg_match('#/shop/erp#', $path) || preg_match('#/erp(?:/|$)#', $path)) {
		return false;
	}
	$commercePath = preg_match('#/shop(?:/|$)#', $path)
		|| preg_match('#^/(en|ru|ar)/shop#i', $path)
		|| preg_match('#^/(en|ru|ar)/(parts|umapi_catalog|vehicle-catalog|available-brands|garazh)(?:/|$)#i', $path)
		|| preg_match('#^/(en|ru|ar)/shop/#i', $path);
	if (!$commercePath) {
		return false;
	}
	global $DP_Config;
	$base = rtrim((string) ($DP_Config->domain_path ?? ''), '/');
	$langPrefix = '';
	if (preg_match('#^/(en|ru|ar)(?:/|$)#i', $path, $lm)) {
		$langPrefix = '/' . strtolower($lm[1]);
	}
	header('Location: ' . $base . $langPrefix . '/', true, 302);
	return true;
}

/**
 * Block public shop URLs when storefront is disabled.
 */
function epc_erp_portal_block_storefront_path()
{
	if (!function_exists('epc_portal_storefront_enabled') || epc_portal_storefront_enabled()) {
		return false;
	}
	$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
	if (!is_string($path)) {
		return false;
	}
	if (epc_erp_portal_match_request() !== null) {
		return false;
	}
	if (preg_match('#^/cp(?:/|$)#', $path) || preg_match('#/cp(?:/|$)#', $path)) {
		return false;
	}
	if (preg_match('#/shop/erp#', $path) || preg_match('#/erp(?:/|$)#', $path)) {
		return false;
	}
	if (preg_match('#/shop(?:/|$)#', $path) || preg_match('#^/(en|ru|ar)/shop#i', $path)) {
		header('Location: ' . epc_erp_portal_canonical_base(''), true, 302);
		return true;
	}
	return false;
}
