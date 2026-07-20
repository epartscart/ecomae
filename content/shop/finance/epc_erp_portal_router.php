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
	// DP_User::isAdmin() and other legacy helpers read the connection from the
	// global $db_link, so it must be set before any access check runs — otherwise
	// they call ->prepare() on null and the whole request fatals.
	$GLOBALS['db_link'] = $db_link;
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
	// The portal serves both the logged-out sign-in page and the signed-in
	// workspace from the same /erp/ URL (the response varies by auth cookie).
	// Without an explicit cache policy some browsers/proxies serve a stale copy
	// (e.g. an old unstyled sign-in page) after a deploy. Mark it non-cacheable
	// so every visit fetches the current page.
	if (!headers_sent()) {
		header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
		header('Pragma: no-cache');
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_security.php';
	epc_erp_send_security_headers(true);
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
	// ERP-only tenants must never be linked out to the CP control panel or Super
	// CP — they live entirely inside the standalone /erp/ door. Full tenants keep
	// both escape hatches.
	if (!function_exists('epc_erp_is_erp_only_context')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
	}
	$isErpOnly = function_exists('epc_erp_is_erp_only_context') && epc_erp_is_erp_only_context();
	$cssVer = '20260530a';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_topbar_brand.php';
	$epcErpTopBrand = epc_erp_topbar_brand_context();
	$pageBrandTitle = (string) ($epcErpTopBrand['title'] ?? ($brand['product_name'] ?? 'ERP'));
	?>
<!DOCTYPE html>
<html lang="en">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title><?php echo htmlspecialchars($pageTitle . ' — ' . $pageBrandTitle, ENT_QUOTES, 'UTF-8'); ?></title>
	<?php
	if (!empty($epcErpTopBrand['logo_url'])) {
		$fav = (string) $epcErpTopBrand['logo_url'];
		$favType = (substr($fav, -4) === '.svg' || strpos($fav, 'logo_svg') !== false) ? 'image/svg+xml' : 'image/png';
		echo '<link rel="icon" type="' . htmlspecialchars($favType, ENT_QUOTES, 'UTF-8') . '" href="' . htmlspecialchars($fav, ENT_QUOTES, 'UTF-8') . '" />' . "\n";
	}
	?>
	<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css" />
	<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.4.1/css/bootstrap.min.css" />
	<?php
	// The CP/ERP stylesheets are NOT served as static assets on the marketing
	// host (they 404), which left the signed-in ERP workspace unstyled. Inline
	// them from disk so the full ERP app is styled on the standalone portal.
	$epc_erp_inline_css_files = array(
		$_SERVER['DOCUMENT_ROOT'] . '/' . ($DP_Config->backend_dir ?? 'cp') . '/templates/bootstrap_admin/css/epc_cp_ui.css',
		$_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_portal.css',
		$_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_professional.css',
		$_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.css',
	);
	foreach ($epc_erp_inline_css_files as $epc_erp_css_path) {
		if (is_file($epc_erp_css_path)) {
			echo "\n<style data-erp-inline=\"" . htmlspecialchars(basename($epc_erp_css_path), ENT_QUOTES, 'UTF-8') . "\">\n";
			echo file_get_contents($epc_erp_css_path);
			echo "\n</style>\n";
		}
	}
	?>
	<?php
	// Theme tokens (Blue & White for the ERP surface). Injected inline so the
	// portal is correctly themed even though the external CSS files are not
	// served as static assets on the marketing host.
	$epc_theme_file = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_theme.php';
	if (is_file($epc_theme_file)) {
		require_once $epc_theme_file;
	}
	if (function_exists('epc_theme_style_tag_for_surface')) {
		echo epc_theme_style_tag_for_surface('erp');
	}
	// Self-contained professional stylesheet for the standalone portal shell,
	// login and home. Kept inline (always served via PHP) so the sign-in page
	// looks right regardless of static-asset availability.
	require $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_portal_inline_css.php';
	if (($epcErpTopBrand['mode'] ?? '') === 'ecomae') {
		epc_ecomae_hub_logo_enqueue();
	}
	?>
</head>
<body class="<?php echo htmlspecialchars($bodyClass, ENT_QUOTES, 'UTF-8'); ?>">
<header class="epc-erp-topbar">
	<div class="epc-erp-topbar__inner">
		<a class="epc-erp-topbar__brand epc-erp-topbar__brand--hub" href="<?php echo $erpBase; ?>" aria-label="<?php echo htmlspecialchars((string) ($epcErpTopBrand['aria'] ?? 'ERP'), ENT_QUOTES, 'UTF-8'); ?>">
			<?php echo epc_erp_topbar_brand_markup(); ?>
		</a>
		<nav class="epc-erp-topbar__nav">
			<a href="<?php echo $erpBase; ?>"><i class="fa fa-dashboard"></i> ERP</a>
			<a href="<?php echo htmlspecialchars($guideUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-book"></i> Guide</a>
			<?php if (!$isErpOnly): ?>
			<a href="<?php echo htmlspecialchars($cpUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-cog"></i> Control panel (advanced)</a>
			<?php endif; ?>
			<?php if ($showStore): ?>
			<a href="/"><i class="fa fa-globe"></i> Website</a>
			<?php endif; ?>
			<?php if ($platformHost && !$isErpOnly): ?>
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
<?php
// ERP tabs register JS via $GLOBALS['epc_cp_page_assets'] (e.g. tax compliance
// "Fetch legislation updates"). CP shells flush those assets; the standalone
// /erp portal must do the same or buttons silently do nothing.
$epcPortalPageAssets = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';
if (is_file($epcPortalPageAssets)) {
	require_once $epcPortalPageAssets;
	if (function_exists('epc_cp_page_footer_scripts')) {
		epc_cp_page_footer_scripts('shop/finance/erp');
	}
}
?>
<script>
(function(){
	/* BOS-style Matrix particle rain */
	var container = document.getElementById('erpPortalParticles');
	if (!container) return;
	var colors = [
		'rgba(14, 165, 233, .7)', 'rgba(14, 165, 233, .35)',
		'rgba(56, 189, 248, .7)', 'rgba(56, 189, 248, .4)',
		'rgba(99, 102, 241, .6)', 'rgba(99, 102, 241, .3)',
		'rgba(168, 85, 247, .5)', 'rgba(16, 185, 129, .4)',
		'rgba(255, 255, 255, .4)', 'rgba(255, 255, 255, .15)'
	];
	var anims = ['erpFloat', 'erpFloatDrift', 'erpFloatStreak'];
	var total = 180;
	for (var i = 0; i < total; i++) {
		var p = document.createElement('div');
		p.className = 'epc-erp-portal-bg__particle';
		var size = Math.random() < 0.15 ? (3 + Math.random() * 4) : (1 + Math.random() * 2.5);
		var left = Math.random() * 100;
		var dur = 4 + Math.random() * 22;
		var delay = Math.random() * -30;
		var color = colors[Math.floor(Math.random() * colors.length)];
		var anim = anims[Math.floor(Math.random() * anims.length)];
		p.style.cssText = 'width:' + size + 'px;height:' + size + 'px;left:' + left + '%;background:' + color + ';animation:' + anim + ' ' + dur + 's linear ' + delay + 's infinite;';
		container.appendChild(p);
	}

	/* Count-up stats */
	var nums = document.querySelectorAll('.epc-erp-bos-hero__stat-num[data-count]');
	if (nums.length) {
		var observer = new IntersectionObserver(function(entries) {
			entries.forEach(function(entry) {
				if (!entry.isIntersecting) return;
				var el = entry.target;
				var target = parseInt(el.getAttribute('data-count'), 10);
				var start = 0;
				var duration = 1200;
				var startTime = null;
				function step(ts) {
					if (!startTime) startTime = ts;
					var progress = Math.min((ts - startTime) / duration, 1);
					el.textContent = Math.floor(progress * target) + '+';
					if (progress < 1) requestAnimationFrame(step);
				}
				requestAnimationFrame(step);
				observer.unobserve(el);
			});
		}, {threshold: 0.3});
		nums.forEach(function(n) { observer.observe(n); });
	}
})();
</script>
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
	// erp_main.php enables its standalone (no-CP-chrome) shell mode by reading
	// $GLOBALS['epc_erp_standalone']; the local above is not in scope there.
	// Without this, erp_main calls CP-only sidebar bootstrap helpers that are
	// not loaded on the marketing host, fataling to a blank workspace.
	$GLOBALS['epc_erp_standalone'] = true;
	$GLOBALS['epc_erp_portal'] = 'frontend';

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
	epc_erp_portal_ensure_guest_session($db_link);
	if (function_exists('epc_erp_portal_bridge_cp_admin_session')) {
		epc_erp_portal_bridge_cp_admin_session($db_link);
	}
	epc_erp_portal_handle_auth_post($db_link);
	if (!isset($multilang_params) || !is_array($multilang_params)) {
		$multilang_params = multilang_init();
	}

	if ($page === 'demo') {
		// Client demo: a separate public link that mirrors the full Super ERP
		// (every module/tab) read-only, with no login. Restricted to the platform
		// marketing/demo host so it can never expose a real tenant's workspace;
		// other hosts keep the safe sample-data dashboard.
		$epcDemoMirrorHost = function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()
			&& !(function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host());
		if ($epcDemoMirrorHost) {
			$GLOBALS['epc_erp_demo_mirror'] = true;
			epc_erp_portal_render_shell(function () use ($db_link, $DP_Config) {
				$user_session = epc_erp_resolve_user_session();
				$epc_erp_portal = 'frontend';
				extract(epc_erp_configure_portal_urls('frontend'));
				$erp_include = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/finance/erp/erp_main.php';
				if (!is_file($erp_include)) {
					echo '<div class="alert alert-danger">ERP module files not found on server.</div>';
					return;
				}
				echo '<div class="epc-erp-workspace epc-erp-workspace--demo">';
				include $erp_include;
				echo '</div>';
			}, array('title' => 'Live Super ERP demo', 'body_class' => 'epc-erp-standalone epc-erp-standalone--demo epc-erp-standalone--mirror'));
			return;
		}
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
