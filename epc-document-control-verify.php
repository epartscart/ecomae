<?php
/**
 * Document Control — read-only verify: files on disk, page render smoke, no inline CSS/JS leak.
 *
 * GET https://www.epartscart.com/epc-document-control-verify.php?token=epartscart-deploy-2026
 * Optional: &host=www.epartscart.com&render=1&apply=0
 */
declare(strict_types=1);

ob_start();
require_once __DIR__ . '/epc_deploy_auth.php';

$token = (string) ($_GET['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	ob_end_clean();
	http_response_code(403);
	header('Content-Type: application/json; charset=utf-8');
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/document_control/epc_document_control_cp_install.php';
require_once __DIR__ . '/content/shop/document_control/epc_document_control_helpers.php';

$host = trim((string) ($_GET['host'] ?? $_SERVER['HTTP_HOST'] ?? 'www.epartscart.com'));
if ($host !== '') {
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
}

$doRender = !isset($_GET['render']) || (string) $_GET['render'] !== '0';
$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';

$DP_Config = new DP_Config();
epc_portal_apply_config($DP_Config);
$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($backend === '') {
	$backend = 'cp';
}
$docRoot = $_SERVER['DOCUMENT_ROOT'] ?? __DIR__;

$result = array(
	'ok' => true,
	'probe' => 'epc-document-control-verify',
	'ts' => gmdate('c'),
	'host' => $host,
	'db' => (string) ($DP_Config->db ?? ''),
	'public_url' => epc_document_control_cp_public_url($host, $backend),
	'files' => array(),
	'route' => null,
	'render' => array('skipped' => !$doRender),
	'apply' => null,
);

$requiredFiles = array(
	'main_page' => $docRoot . '/' . $backend . '/content/shop/document_control/document_control_main_page.php',
	'main' => $docRoot . '/' . $backend . '/content/shop/document_control/document_control_main.php',
	'hub_page' => $docRoot . '/' . $backend . '/content/shop/document_control/document_control_hub_page.php',
	'ajax_endpoint' => $docRoot . '/' . $backend . '/content/shop/document_control/ajax_document_control_endpoint.php',
	'ajax_handler' => $docRoot . '/' . $backend . '/content/shop/document_control/ajax_document_control.php',
	'css' => $docRoot . '/' . $backend . '/content/shop/document_control/epc_document_control.css',
	'js' => $docRoot . '/' . $backend . '/content/shop/document_control/epc_document_control.js',
	'js_config' => $docRoot . '/' . $backend . '/content/shop/document_control/epc_document_control_config.php',
	'css_proxy' => $docRoot . '/content/general_pages/epc_document_control_cp_css.php',
	'page_assets' => $docRoot . '/content/general_pages/epc_cp_page_assets.php',
	'helpers' => $docRoot . '/content/shop/document_control/epc_document_control_helpers.php',
	'schema' => $docRoot . '/content/shop/document_control/epc_document_control_schema.php',
	'print' => $docRoot . '/content/shop/document_control/service/print.php',
);

foreach ($requiredFiles as $key => $path) {
	$bytes = is_file($path) ? (int) filesize($path) : 0;
	$result['files'][$key] = array(
		'exists' => is_file($path),
		'bytes' => $bytes,
		'path' => $path,
	);
	if (!$result['files'][$key]['exists'] || $bytes < 16) {
		$result['ok'] = false;
	}
}

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	ob_end_clean();
	http_response_code(500);
	header('Content-Type: application/json; charset=utf-8');
	exit(json_encode(array('ok' => false, 'message' => 'DB: ' . $e->getMessage())));
}

$probe = epc_document_control_cp_probe($pdo, $docRoot, $backend);
$result['probe_summary'] = $probe;
$row = $probe['content']['shop/document_control/document_control'] ?? null;
$result['route'] = $row ? array(
	'id' => (int) ($row['id'] ?? 0),
	'published' => (int) ($row['published_flag'] ?? 0),
	'content' => (string) ($row['content'] ?? ''),
) : null;

if (!$result['route'] || (int) ($result['route']['published'] ?? 0) !== 1) {
	$result['ok'] = false;
}

if ($apply && function_exists('epc_document_control_cp_install')) {
	$result['apply'] = epc_document_control_cp_install($pdo, $backend);
}

$pageAssetsPath = $requiredFiles['page_assets'];
if (is_file($pageAssetsPath)) {
	require_once $pageAssetsPath;
	$assets = function_exists('epc_cp_page_assets_for_url')
		? epc_cp_page_assets_for_url('shop/document_control/document_control')
		: array('css' => array(), 'js' => array());
	$result['page_assets'] = array(
		'css_count' => count($assets['css'] ?? array()),
		'js_count' => count($assets['js'] ?? array()),
		'css' => array_keys($assets['css'] ?? array()),
		'js' => array_keys($assets['js'] ?? array()),
	);
	if (count($assets['css'] ?? array()) < 2 || count($assets['js'] ?? array()) < 2) {
		$result['ok'] = false;
	}
}

if ($doRender) {
	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id) { return (string) $id; }
	}
	if (!function_exists('translate_str_by_key')) {
		function translate_str_by_key($key) { return (string) $key; }
	}

	$GLOBALS['DP_Config'] = $DP_Config;
	$GLOBALS['DP_Template'] = (object) array('name' => 'bootstrap_admin');
	$db_link = $pdo;
	if (function_exists('epc_dc_ensure_db_link')) {
		epc_dc_ensure_db_link();
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = array('csrf_guard_key' => 'verify');
	if (class_exists('DP_User')) {
		try {
			$user_session = DP_User::getAdminSession();
		} catch (Throwable $e) {
			// stub session for smoke render
		}
	}
	$GLOBALS['user_session'] = $user_session;

	@ini_set('memory_limit', '512M');
	@set_time_limit(120);

	ob_start();
	try {
		include $requiredFiles['main'];
		$html = (string) ob_get_clean();
		$result['render'] = array(
			'include_ok' => true,
			'html_length' => strlen($html),
			'has_hero' => stripos($html, 'epc-dc-hero') !== false,
			'has_dashboard_kpi' => stripos($html, 'epc-dc-kpi') !== false,
			'has_document_control_title' => stripos($html, 'Document Control System') !== false,
			'inline_style_removed' => stripos($html, '<style') === false,
			'inline_script_removed' => stripos($html, '<script') === false,
			'inline_link_removed' => stripos($html, '<link') === false,
			'php_fatal' => (bool) preg_match('/Fatal error|Parse error|Uncaught (?:Error|Exception|Throwable)/i', $html),
		);
		if (!$result['render']['has_hero']
			|| !$result['render']['has_document_control_title']
			|| !$result['render']['inline_style_removed']
			|| !$result['render']['inline_script_removed']
			|| $result['render']['php_fatal']) {
			$result['ok'] = false;
		}
	} catch (Throwable $e) {
		ob_end_clean();
		$result['render'] = array(
			'include_ok' => false,
			'error' => $e->getMessage(),
		);
		$result['ok'] = false;
	}
}

ob_end_clean();
header('Content-Type: application/json; charset=utf-8');
echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
