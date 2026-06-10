<?php
/**
 * POST JSON: { "email", "tenant_key", "context": "cp"|"storefront", "return_url" }
 */
define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_auth_email_otp.php';

$input = epc_auth_read_json_body();
$mode = epc_auth_normalize_mode((string) ($input['context'] ?? 'cp'));
$hints = array(
	'tenant_key' => (string) ($input['tenant_key'] ?? $input['site_key'] ?? ''),
);
$ctx = epc_auth_resolve_for_mode($mode, $hints);
if (empty($ctx['ok'])) {
	epc_auth_json_response(array('ok' => false, 'message' => (string) ($ctx['message'] ?? 'Unknown tenant')), 400);
}
$ctx['auth_mode'] = $mode;
if (!empty($input['return_url'])) {
	$ctx['return_url'] = (string) $input['return_url'];
}

require_once __DIR__ . '/content/general_pages/epc_portal.php';
$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	$cfg = epc_auth_bootstrap_config();
	try {
		$platformPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		epc_auth_json_response(array('ok' => false, 'message' => 'Platform database unavailable'), 503);
	}
}

$email = (string) ($input['email'] ?? '');
$result = epc_auth_otp_send($platformPdo, $email, $ctx);
epc_auth_json_response($result, !empty($result['ok']) ? 200 : 400);
