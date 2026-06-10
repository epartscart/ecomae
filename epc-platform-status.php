<?php
/**
 * Platform failover status API (JSON).
 * GET  — public status (reads mode file + static JSON mirror)
 * POST — set mode (deploy token or Super CP session)
 *
 * https://www.ecomae.com/epc-platform-status.php
 * https://www.ecomae.com/epc-platform-status.json (static mirror)
 */
declare(strict_types=1);

require_once __DIR__ . '/content/general_pages/epc_platform_failover.php';

header('Cache-Control: no-store, no-cache, must-revalidate');
header('Pragma: no-cache');

if (!empty($_GET['ping'])) {
	header('Content-Type: application/json; charset=utf-8');
	echo json_encode(epc_failover_build_status('primary_ok', array('ping' => true)), JSON_UNESCAPED_SLASHES);
	exit;
}

$method = strtoupper($_SERVER['REQUEST_METHOD'] ?? 'GET');

if ($method === 'POST') {
	require_once __DIR__ . '/epc_deploy_auth.php';
	$authorized = false;
	$token = (string) ($_POST['token'] ?? $_GET['token'] ?? '');
	if ($token !== '' && hash_equals(epc_deploy_token(), $token)) {
		$authorized = true;
	}
	if (!$authorized && session_status() !== PHP_SESSION_ACTIVE) {
		@session_start();
	}
	if (!$authorized && !empty($_SESSION['user_id'])) {
		require_once __DIR__ . '/config.php';
		require_once __DIR__ . '/content/users/dp_user.php';
		require_once __DIR__ . '/content/general_pages/epc_portal.php';
		$admin = DP_User::getAdminSession();
		if (!empty($admin) && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
			$authorized = true;
		}
	}
	if (!$authorized) {
		http_response_code(403);
		header('Content-Type: application/json; charset=utf-8');
		echo json_encode(array('ok' => false, 'message' => 'Forbidden'));
		exit;
	}
	$mode = trim((string) ($_POST['mode'] ?? ''));
	if (!in_array($mode, epc_failover_valid_modes(), true)) {
		http_response_code(400);
		header('Content-Type: application/json; charset=utf-8');
		echo json_encode(array('ok' => false, 'message' => 'Invalid mode', 'modes' => epc_failover_valid_modes()));
		exit;
	}
	$extra = array();
	if ($mode === 'failback_redirect') {
		$extra['redirect_seconds'] = max(3, min(120, (int) ($_POST['redirect_seconds'] ?? 12)));
	}
	if (!epc_failover_write_mode_file($mode)) {
		http_response_code(500);
		header('Content-Type: application/json; charset=utf-8');
		echo json_encode(array('ok' => false, 'message' => 'Could not write mode file'));
		exit;
	}
	$status = epc_failover_build_status($mode, $extra);
	header('Content-Type: application/json; charset=utf-8');
	echo json_encode(array('ok' => true, 'written' => true, 'status' => $status), JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
	exit;
}

$autoProbe = !empty($_GET['probe']);
$previewMode = trim((string) ($_GET['mode'] ?? ''));
if ($previewMode !== '' && in_array($previewMode, epc_failover_valid_modes(), true)) {
	$status = epc_failover_build_status($previewMode);
} else {
	$status = epc_failover_current_status($autoProbe);
}

header('Content-Type: application/json; charset=utf-8');
echo json_encode($status, JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
