<?php
/**
 * Verify CP top connectivity alert helpers (deploy smoke).
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'forbidden')));
}

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_cp_professional_shell.php';
require_once __DIR__ . '/content/general_pages/epc_cp_top_alerts.php';

echo json_encode(array(
	'ok' => true,
	'host' => $_SERVER['HTTP_HOST'] ?? '',
	'https_redirect_configured' => epc_cp_https_redirect_is_configured($DP_Config),
	'marker_file' => is_file(__DIR__ . '/content/epc_https_redirect.ok'),
	'show_in_header' => epc_cp_top_alerts_show_in_header(),
	'professional_shell' => epc_cp_top_alerts_use_professional_header(),
	'suppress_email_sms' => epc_cp_top_alerts_suppress_email_sms(),
	'css_version' => function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : null,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
