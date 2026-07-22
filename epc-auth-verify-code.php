<?php
/**
 * POST JSON: { "email", "code", "tenant_key", "context": "cp"|"storefront", "return_url" }
 */
declare(strict_types=1);

define('_ASTEXE_', 1);

// Prevent stray PHP notices / mailer debug from breaking JSON clients.
ob_start();

try {
	require_once __DIR__ . '/content/general_pages/epc_auth_email_otp.php';

	$input = epc_auth_read_json_body();
	$mode = epc_auth_normalize_mode((string) ($input['context'] ?? 'cp'));
	$hints = array(
		'tenant_key' => (string) ($input['tenant_key'] ?? $input['site_key'] ?? ''),
	);
	$ctx = epc_auth_resolve_for_mode($mode, $hints);
	if (empty($ctx['ok'])) {
		while (ob_get_level() > 0) {
			ob_end_clean();
		}
		epc_auth_json_response(array('ok' => false, 'message' => (string) ($ctx['message'] ?? 'Unknown tenant')), 400);
	}
	$ctx['auth_mode'] = $mode;
	if (!empty($input['return_url'])) {
		$ctx['return_url'] = (string) $input['return_url'];
	}

	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
	$platformPdo = epc_portal_platform_pdo();
	if (!$platformPdo instanceof PDO) {
		$cfg = epc_auth_bootstrap_config();
		$platformPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	}

	$email = (string) ($input['email'] ?? '');
	$code = (string) ($input['code'] ?? '');
	$result = epc_auth_otp_verify($platformPdo, $email, $code, $ctx);
	while (ob_get_level() > 0) {
		ob_end_clean();
	}
	epc_auth_json_response($result, !empty($result['ok']) ? 200 : 400);
} catch (Throwable $e) {
	while (ob_get_level() > 0) {
		ob_end_clean();
	}
	error_log('epc-auth-verify-code: ' . $e->getMessage());
	if (!headers_sent()) {
		http_response_code(500);
		header('Content-Type: application/json; charset=utf-8');
		header('Cache-Control: no-store');
	}
	echo json_encode(array(
		'ok' => false,
		'message' => 'Verification temporarily unavailable — please retry',
	));
	exit;
}
