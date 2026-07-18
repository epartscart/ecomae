<?php
/**
 * CP AJAX — website tracker dashboard + session detail.
 */
header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (function_exists('epc_portal_apply_config')) {
	epc_portal_apply_config($DP_Config);
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_web_tracker.php';

if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo json_encode(array('ok' => false, 'error' => 'forbidden'));
	exit;
}

$pdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
if (!$pdo instanceof PDO) {
	try {
		$pdo = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		http_response_code(503);
		echo json_encode(array('ok' => false, 'error' => 'db'));
		exit;
	}
}

$isSuper = function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator();
if (!$isSuper && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
	// Super host with admin session — allow all-tenant view.
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
		echo json_encode(array('ok' => false, 'error' => 'tenant_scope'));
		exit;
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
		echo json_encode(array('ok' => true, 'detail' => $detail));
		exit;
	}

	$all = ($isSuper && ($siteKey === '_all' || $siteKey === ''));
	$data = epc_web_tracker_dashboard($pdo, $all ? '_all' : $siteKey, $range['from'], $range['to'], $all);
	echo json_encode(array(
		'ok' => true,
		'site_key' => $all ? '_all' : $siteKey,
		'from' => $range['from'],
		'to' => $range['to'],
		'is_super' => $isSuper,
		'data' => $data,
	));
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => 'query_failed', 'message' => $e->getMessage()));
}
