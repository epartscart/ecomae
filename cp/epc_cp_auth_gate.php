<?php
/**
 * Super CP — enforce login before operator pages (tenant hub, platform deploy, etc.).
 * ERP-only client tenants — redirect bare /cp/ to ERP professional shell after login.
 * Does not set global $db_link (dp_core license bootstrap is sensitive to that).
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_auth_gate_pdo()
{
	global $DP_Config;
	static $gateDb = null;
	if ($gateDb instanceof PDO) {
		return $gateDb;
	}
	if (!isset($DP_Config) || !is_object($DP_Config)) {
		return null;
	}
	try {
		$gateDb = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		return null;
	}
	return $gateDb;
}

function epc_cp_auth_gate_is_admin()
{
	$session = isset($_COOKIE['admin_session']) ? (string) $_COOKIE['admin_session'] : '';
	$userId = isset($_COOKIE['admin_u_id']) ? (int) $_COOKIE['admin_u_id'] : 0;
	if ($session === '' || $userId <= 0) {
		return false;
	}
	$pdo = epc_cp_auth_gate_pdo();
	if (!$pdo instanceof PDO) {
		return false;
	}
	try {
		$st = $pdo->prepare('SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?');
		$st->execute(array($session, $userId));
		return ((int) $st->fetchColumn()) === 1;
	} catch (Exception $e) {
		return false;
	}
}

function epc_cp_auth_gate_erp_only_landing()
{
	if (!function_exists('epc_portal_is_erp_only_tenant') || !epc_portal_is_erp_only_tenant()) {
		return '';
	}
	if (function_exists('epc_portal_erp_cp_shell_url')) {
		return epc_portal_erp_cp_shell_url();
	}
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp';
	return '/' . $backend . '/shop/finance/erp?epc_erp_shell=1';
}

function epc_cp_auth_gate_run()
{
	global $DP_Config;
	$backend = trim((string) $DP_Config->backend_dir, '/');
	if ($backend === '') {
		$backend = 'cp';
	}
	$cpBase = '/' . $backend;
	$requestUri = isset($_SERVER['REQUEST_URI']) ? $_SERVER['REQUEST_URI'] : '/';
	$path = parse_url($requestUri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		$path = '/';
	}

	// Handle MFA AJAX requests
	if (isset($_GET['epc_mfa_ajax'])) {
		epc_cp_auth_gate_mfa_ajax();
		exit;
	}

	$requestMethod = isset($_SERVER['REQUEST_METHOD']) ? $_SERVER['REQUEST_METHOD'] : 'GET';
	if ($requestMethod === 'POST' && !empty($_POST['authentication'])) {
		return;
	}

	$isAdmin = epc_cp_auth_gate_is_admin();
	$isDemoCp = function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context();
	$demoLoginRoot = false;
	if ($isDemoCp && function_exists('epc_portal_demo_parse_cp_path')) {
		$demoParsed = epc_portal_demo_parse_cp_path();
		$demoLoginRoot = is_array($demoParsed) && !empty($demoParsed['is_login_root']);
	}

	$isCpRoot = ($path === $cpBase || $path === $cpBase . '/' || $path === $cpBase . '/index.php');
	$cpControlUrl = function_exists('epc_cp_control_url') ? epc_cp_control_url($backend) : ($cpBase . '/control');

	if (($isCpRoot || $demoLoginRoot) && $requestMethod === 'GET') {
		if (!$isAdmin) {
			return;
		}
		if ($isDemoCp) {
			if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
				&& function_exists('epc_portal_demo_erp_shell_url') && function_exists('epc_portal_demo_cp_site_key')) {
				$key = epc_portal_demo_cp_site_key();
				if ($key !== '') {
					header('Location: ' . epc_portal_demo_erp_shell_url($key), true, 302);
					exit;
				}
			}
			if ($demoLoginRoot && function_exists('epc_portal_demo_cp_site_key') && function_exists('epc_portal_demo_cp_post_login_url')) {
				$key = epc_portal_demo_cp_site_key();
				if ($key !== '') {
					header('Location: ' . epc_portal_demo_cp_post_login_url($key), true, 302);
					exit;
				}
			}
			return;
		}
		if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()
			&& function_exists('epc_platform_erp_shell_url')) {
			header('Location: ' . epc_platform_erp_shell_url(), true, 302);
			exit;
		}
		if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
			$key = function_exists('epc_client_erp_site_key') ? epc_client_erp_site_key() : '';
			if ($key !== '' && function_exists('epc_client_erp_shell_url')) {
				header('Location: ' . epc_client_erp_shell_url($key), true, 302);
				exit;
			}
		}
		if (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()) {
			return;
		}
		$erpLanding = epc_cp_auth_gate_erp_only_landing();
		if ($erpLanding !== '' && (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname())) {
			header('Location: ' . $erpLanding, true, 302);
			exit;
		}
		header('Location: ' . $cpControlUrl, true, 302);
		exit;
	}

	if (!function_exists('epc_portal_is_platform_operator') || !epc_portal_is_platform_operator()) {
		return;
	}

	if ($isDemoCp) {
		return;
	}

	if ($isAdmin) {
		return;
	}

	$operatorPrefixes = array(
		$cpBase . '/shop/tenant_hub/',
		$cpBase . '/control/portal/industry_settings',
		$cpBase . '/control/portal/epc_platform_failover_guide',
		$cpBase . '/control/portal/epc_platform_health_checkup',
		$cpBase . '/control/portal/epc_platform_governance',
		$cpBase . '/control/portal/epc_custom_shipping_guide',
		$cpBase . '/control/portal/epc_erp_only_onboard_guide',
		$cpBase . '/control/portal/epc_cp_auth_settings',
		$cpBase . '/control/portal/epc_tenant_control_center',
	);
	foreach ($operatorPrefixes as $prefix) {
		if (strpos($path, $prefix) === 0) {
			header('Location: ' . $cpControlUrl, true, 302);
			exit;
		}
	}
}

/* ─────────────────── MFA Route Guard ─────────────────── */

function epc_cp_mfa_route_guard(): void
{
	$userId = isset($_COOKIE['admin_u_id']) ? (int) $_COOKIE['admin_u_id'] : 0;
	if ($userId <= 0) {
		return;
	}
	$pdo = epc_cp_auth_gate_pdo();
	if (!$pdo instanceof PDO) {
		return;
	}
	$mfaFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_mfa.php';
	if (!is_file($mfaFile)) {
		return;
	}
	require_once $mfaFile;

	$requestUri = isset($_SERVER['REQUEST_URI']) ? $_SERVER['REQUEST_URI'] : '/';
	$path = parse_url($requestUri, PHP_URL_PATH);
	if (!is_string($path)) {
		return;
	}

	epc_mfa_enforce_route_guard($pdo, $userId, $path);
}

/* ─────────────────── MFA AJAX Handler ─────────────────── */

function epc_cp_auth_gate_mfa_ajax(): void
{
	header('Content-Type: application/json; charset=utf-8');

	$userId = isset($_COOKIE['admin_u_id']) ? (int) $_COOKIE['admin_u_id'] : 0;
	if ($userId <= 0 || !epc_cp_auth_gate_is_admin()) {
		echo json_encode(array('ok' => false, 'error' => 'Not authenticated'));
		return;
	}

	$pdo = epc_cp_auth_gate_pdo();
	if (!$pdo instanceof PDO) {
		echo json_encode(array('ok' => false, 'error' => 'Database unavailable'));
		return;
	}

	$mfaFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_mfa.php';
	if (!is_file($mfaFile)) {
		echo json_encode(array('ok' => false, 'error' => 'MFA module not available'));
		return;
	}
	require_once $mfaFile;

	$result = epc_mfa_handle_ajax($pdo, $userId);
	echo json_encode($result, JSON_UNESCAPED_UNICODE);
}
