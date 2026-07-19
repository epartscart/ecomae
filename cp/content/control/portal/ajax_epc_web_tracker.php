<?php
/**
 * CP AJAX — website tracker dashboard + session detail.
 */
define('_ASTEXE_', 1);
header('Content-Type: application/json; charset=utf-8');
if (ob_get_level()) {
	ob_end_clean();
}

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once $docRoot . '/content/general_pages/epc_portal.php';
if (function_exists('epc_portal_apply_config')) {
	epc_portal_apply_config($DP_Config);
}

$dbHost = trim((string) $DP_Config->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
global $db_link;
try {
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	http_response_code(503);
	exit(json_encode(array('ok' => false, 'error' => 'db')));
}

require_once $docRoot . '/content/users/dp_user.php';
require_once $docRoot . '/content/general_pages/epc_web_tracker.php';
// Ensure platform PDO helper is loaded (epc_portal.php does not always pull tenant helpers).
if (!function_exists('epc_portal_platform_pdo') && is_file($docRoot . '/content/general_pages/epc_portal_tenant.php')) {
	require_once $docRoot . '/content/general_pages/epc_portal_tenant.php';
}

if ((int) DP_User::getAdminId() <= 0) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'forbidden')));
}

/**
 * Tracker rows live on the platform DB (ecomae). Tenant CP DB (docpart) may only have empty schema.
 * Prefer the connection that actually has sessions.
 */
$pdoPlatform = null;
if (function_exists('epc_portal_platform_pdo')) {
	$pdoPlatform = epc_portal_platform_pdo();
}
$pdo = $db_link;
$dbLabel = 'tenant';
try {
	$platCount = ($pdoPlatform instanceof PDO)
		? (int) $pdoPlatform->query('SELECT COUNT(*) FROM `epc_web_tracker_sessions`')->fetchColumn()
		: -1;
	$tenCount = (int) $db_link->query('SELECT COUNT(*) FROM `epc_web_tracker_sessions`')->fetchColumn();
	if ($pdoPlatform instanceof PDO && $platCount >= $tenCount) {
		$pdo = $pdoPlatform;
		$dbLabel = 'platform';
	}
} catch (Throwable $e) {
	if ($pdoPlatform instanceof PDO) {
		$pdo = $pdoPlatform;
		$dbLabel = 'platform';
	}
}

$isSuper = function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator();
if (!$isSuper && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
	$isSuper = true;
}

$action = (string) ($_GET['action'] ?? $_POST['action'] ?? 'dashboard');
$siteKey = preg_replace('/[^a-z0-9_\-]/', '', strtolower((string) ($_GET['site_key'] ?? $_POST['site_key'] ?? '')));
$range = epc_web_tracker_range_from_request();

if (!$isSuper) {
	$own = epc_web_tracker_resolve_site_key();
	if ($siteKey === '' || $siteKey === '_all') {
		$siteKey = $own;
	}
	if ($siteKey !== $own) {
		http_response_code(403);
		exit(json_encode(array('ok' => false, 'error' => 'tenant_scope')));
	}
}
if ($siteKey === '') {
	$siteKey = '_all';
}

try {
	if ($action === 'session') {
		$id = (int) ($_GET['id'] ?? $_POST['id'] ?? 0);
		$detail = epc_web_tracker_session_detail($pdo, $id, $isSuper ? '' : $siteKey, $isSuper && $siteKey === '_all');
		if (!$detail['session'] && $isSuper && $siteKey !== '_all') {
			$detail = epc_web_tracker_session_detail($pdo, $id, $siteKey, false);
		}
		exit(json_encode(array('ok' => true, 'detail' => $detail)));
	}

	$all = ($isSuper && ($siteKey === '_all' || $siteKey === ''));
	$data = epc_web_tracker_dashboard($pdo, $all ? '_all' : $siteKey, $range['from'], $range['to'], $all);
	exit(json_encode(array(
		'ok' => true,
		'site_key' => $all ? '_all' : $siteKey,
		'from' => $range['from'],
		'to' => $range['to'],
		'is_super' => $isSuper,
		'db' => $dbLabel,
		'data' => $data,
	)));
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'query_failed', 'message' => $e->getMessage())));
}
