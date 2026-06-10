<?php
/**
 * CP prices page verify: files, render smoke, price list counts.
 * GET: token=epartscart-deploy-2026&host=www.epartscart.com&render=0|1
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

$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));
if ($host !== '') {
	$_SERVER['HTTP_HOST'] = $host;
}

$DP_Config = new DP_Config();
epc_portal_apply_config($DP_Config);
$backend = (string) $DP_Config->backend_dir;
$doRender = !isset($_GET['render']) || (string) $_GET['render'] !== '0';

$result = array(
	'ok' => true,
	'host' => $host,
	'db' => $DP_Config->db,
	'files' => array(),
	'render' => array('skipped' => !$doRender),
	'price_lists_count' => 0,
	'price_lists' => array(),
);

$requiredFiles = array(
	'prices_manager' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/prices_manager.php',
	'storefront_flags' => __DIR__ . '/content/shop/docpart/epc_storefront_storage_flags.php',
	'storefront_panel' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/epc_storefront_storage_panel.php',
	'storefront_toggle_ajax' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/ajax_epc_storefront_storage_toggle.php',
	'storefront_toggle_js' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/epc_storefront_storage_toggle.js',
	'storefront_toggle_config' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/epc_storefront_storage_toggle_config.php',
	'prices_cp_css' => __DIR__ . '/content/general_pages/epc_prices_cp_css.php',
	'prices_css' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/epc_prices_cp.css',
);

foreach ($requiredFiles as $key => $path) {
	$result['files'][$key] = array(
		'exists' => is_file($path),
		'bytes' => is_file($path) ? (int) filesize($path) : 0,
	);
	if (!$result['files'][$key]['exists'] || $result['files'][$key]['bytes'] < 32) {
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

$result['price_lists'] = $pdo->query(
	'SELECT p.`id`, p.`name`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `rows`
	 FROM `shop_docpart_prices` p ORDER BY p.`id`'
)->fetchAll(PDO::FETCH_ASSOC);
$result['price_lists_count'] = count($result['price_lists']);

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

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = array('csrf_guard_key' => 'verify');
	if (class_exists('DP_User')) {
		try {
			$user_session = DP_User::getAdminSession();
		} catch (Throwable $e) {
			// keep stub session for smoke render
		}
	}

	@ini_set('memory_limit', '512M');
	@set_time_limit(120);

	$renderOb = ob_start();
	try {
		include $requiredFiles['prices_manager'];
		$html = (string) ob_get_clean();
		$result['render'] = array(
			'include_ok' => true,
			'html_length' => strlen($html),
			'has_prices_table' => stripos($html, 'id="prices_table"') !== false,
			'has_storefront_panel' => stripos($html, 'epc_storefront_storage_table') !== false,
			'has_storefront_save_btn' => stripos($html, 'epc-ssf-save-btn') !== false,
			'inline_toggle_script_removed' => stripos($html, 'ajax_epc_storefront_storage_toggle.php') === false,
			'has_s_uae' => stripos($html, 'S-UAE') !== false,
			'has_r_uae' => stripos($html, 'R-UAE') !== false,
			'row_count_in_html' => substr_count(strtolower($html), '<tr onclick="get_price_preview_'),
		);
		if (!$result['render']['has_prices_table'] || $result['render']['row_count_in_html'] < 2) {
			$result['ok'] = false;
		}
	} catch (Throwable $e) {
		if (ob_get_level() > 0) {
			ob_end_clean();
		}
		$result['ok'] = false;
		$result['render'] = array(
			'include_ok' => false,
			'error' => $e->getMessage(),
		);
	}
}

ob_end_clean();
header('Content-Type: application/json; charset=utf-8');
echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
