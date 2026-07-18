<?php
/**
 * BOS AJAX / session security gate (deployable under content/).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_security_kernel.php';

/** Actions that may run without a BOS session. */
function epc_bos_public_actions(): array
{
	return array('login');
}

/** Actions that require provider (fleet) role. */
function epc_bos_provider_only_actions(): array
{
	return array(
		'tenant_list', 'tenant_info', 'switch_tenant', 'fleet_health', 'tenant_compliance',
		'system_health', 'isolation_audit', 'mfa_policy', 'mfa_stats', 'webhooks', 'events',
		'design_tokens', 'readiness_score', 'notifications', 'db_migrations', 'cp_role_home',
		'credit_limit', 'order_erp_pipeline', 'po_approval', 'rest_api_v2', 'fulfillment_queue',
		'bi_metrics', 'ai_classification', 'tenant_config', 'workflow_builder', 'inventory_forecast',
		'multi_currency_gl', 'sso_saml', 'wps_payroll', 'collections_dunning', 'warranty_rma',
		'dealer_portal', 'ai_copilot', 'nl_reporting', 'industry_packs', 'multi_entity',
		'promotions_engine', 'config_sandbox', 'import_orchestrator', 'document_vault',
		'subscription_billing', 'soc2_compliance', 'marketplace', 'ai_service', 'metabase_embed',
		'isolation_anomaly',
	);
}

function epc_bos_ajax_action_name(): string
{
	$action = isset($_POST['bos_action']) ? trim((string) $_POST['bos_action']) : '';
	if ($action === '') {
		$action = isset($_GET['bos_action']) ? trim((string) $_GET['bos_action']) : '';
	}
	if ($action === '' && isset($_POST['email'], $_POST['password'])) {
		$action = 'login';
	}
	return $action;
}

/**
 * Call from bos/index.php before loading ajax_epc_bos.php.
 * Also safe to call at the top of ajax_epc_bos.php.
 */
function epc_bos_ajax_entry_guard(): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;

	epc_sec_send_headers('DENY');

	$sessionFile = __DIR__ . '/../users/epc_session_security.php';
	if (is_file($sessionFile)) {
		require_once $sessionFile;
		if (function_exists('epc_session_harden_ini') && session_status() === PHP_SESSION_NONE) {
			epc_session_harden_ini();
		}
	}

	epc_bos_session_start();
	$action = epc_bos_ajax_action_name();

	if ($action === 'login') {
		epc_sec_require_rate_limit('bos_login', 12, 300);
		return;
	}

	if ($action === '' || $action === 'Invalid action') {
		http_response_code(400);
		header('Content-Type: application/json; charset=utf-8');
		exit(json_encode(array('ok' => false, 'error' => 'Unknown action')));
	}

	$ctx = epc_bos_context();
	$role = (string) ($ctx['role'] ?? 'guest');
	$userId = (int) ($ctx['user_id'] ?? 0);

	if ($role === 'guest' || $userId <= 0) {
		http_response_code(401);
		header('Content-Type: application/json; charset=utf-8');
		exit(json_encode(array('ok' => false, 'error' => 'Authentication required')));
	}

	if (is_file($sessionFile) && function_exists('epc_session_validate')) {
		if (!epc_session_validate(1800, 28800)) {
			http_response_code(401);
			header('Content-Type: application/json; charset=utf-8');
			exit(json_encode(array('ok' => false, 'error' => 'Session expired')));
		}
	}

	// Mutating provider actions require CSRF.
	$method = strtoupper((string) ($_SERVER['REQUEST_METHOD'] ?? 'GET'));
	$mutating = in_array($method, array('POST', 'PUT', 'PATCH', 'DELETE'), true);
	$readOnlySafe = array(
		'tenant_list', 'tenant_info', 'fleet_health', 'system_health', 'notifications',
		'readiness_score', 'mfa_stats', 'bi_metrics',
	);
	if ($mutating && !in_array($action, $readOnlySafe, true)) {
		// Prefer CSRF; allow first wave without token only for legacy GET-style reads posted as POST.
		$sub = (string) ($_POST['sub_action'] ?? '');
		$needsCsrf = ($sub !== '' && !in_array($sub, array('list', 'get', 'stats', 'run_audit', 'latest_run', 'recent_violations'), true))
			|| in_array($action, array(
				'switch_tenant', 'tenant_config', 'config_sandbox', 'import_orchestrator',
				'document_vault', 'mfa_policy', 'webhooks', 'design_tokens',
			), true);
		if ($needsCsrf && !epc_sec_csrf_validate('bos')) {
			http_response_code(403);
			header('Content-Type: application/json; charset=utf-8');
			exit(json_encode(array(
				'ok' => false,
				'error' => 'CSRF validation failed',
				'csrf_required' => true,
			)));
		}
	}

	if (in_array($action, epc_bos_provider_only_actions(), true) && $role !== 'provider') {
		http_response_code(403);
		header('Content-Type: application/json; charset=utf-8');
		exit(json_encode(array('ok' => false, 'error' => 'Provider access required')));
	}
}

/** Issue CSRF token for BOS UI (logged-in pages). */
function epc_bos_csrf_meta(): string
{
	$token = epc_sec_csrf_token('bos');
	return '<meta name="epc-bos-csrf" content="' . htmlspecialchars($token, ENT_QUOTES, 'UTF-8') . '">'
		. '<script>window.EPC_BOS_CSRF=' . json_encode($token) . ';</script>';
}
