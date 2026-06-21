<?php
/**
 * Integrations AJAX — mobile config, feature flags, tenant SMTP.
 */
define('_ASTEXE_', 1);
header('Content-Type: application/json; charset=utf-8');

if (ob_get_level()) {
	ob_end_clean();
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

$dbHost = trim((string) $DP_Config->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
try {
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	exit(json_encode(array('status' => false, 'message' => 'Database connection failed')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_integrations_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_smtp.php';

if ((int) DP_User::getAdminId() <= 0) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Admin login required')));
}

$pdo = $db_link;
$action = (string) ($_POST['action'] ?? '');

if ($action === 'save_mobile') {
	$integrations = epc_integrations_load_tenant_config($pdo);
	$integrations['mobile'] = array(
		'enabled' => !empty($_POST['enabled']),
		'app_name' => substr(trim((string) ($_POST['app_name'] ?? '')), 0, 120),
		'bundle_id' => substr(trim((string) ($_POST['bundle_id'] ?? '')), 0, 120),
		'deep_link_scheme' => substr(trim((string) ($_POST['deep_link_scheme'] ?? '')), 0, 64),
		'deep_link_domain' => substr(trim((string) ($_POST['deep_link_domain'] ?? '')), 0, 120),
		'api_base_url' => substr(trim((string) ($_POST['api_base_url'] ?? '')), 0, 255),
		'play_store_url' => substr(trim((string) ($_POST['play_store_url'] ?? '')), 0, 255),
		'app_store_url' => substr(trim((string) ($_POST['app_store_url'] ?? '')), 0, 255),
		'pwa_enabled' => !empty($_POST['pwa_enabled']),
		'firebase_project_id' => substr(trim((string) ($_POST['firebase_project_id'] ?? '')), 0, 120),
		'push_enabled' => !empty($_POST['push_enabled']),
	);
	epc_integrations_save_tenant_config($pdo, $integrations);
	exit(json_encode(array('status' => true, 'message' => 'Mobile settings saved.')));
}

if ($action === 'save_feature_flags') {
	if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
		exit(json_encode(array('status' => false, 'message' => 'Super CP only')));
	}
	$platformPdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : $pdo;
	$siteKey = preg_replace('/[^a-z0-9_-]/', '', (string) ($_POST['site_key'] ?? ''));
	if ($siteKey === '') {
		exit(json_encode(array('status' => false, 'message' => 'Invalid site_key')));
	}
	$posted = isset($_POST['features']) && is_array($_POST['features']) ? $_POST['features'] : array();
	$flags = array();
	foreach (epc_integrations_catalog() as $key => $meta) {
		if ($key === 'tenant_registry') {
			continue;
		}
		$flags[$key] = !empty($posted[$key]);
	}
	$res = epc_integrations_save_feature_flags($platformPdo, $siteKey, $flags);
	exit(json_encode(array('status' => true, 'message' => 'Saved ' . (int) ($res['saved'] ?? 0) . ' feature flags.')));
}

if ($action === 'save_tenant_smtp') {
	$integrations = epc_integrations_load_tenant_config($pdo);
	$existing = isset($integrations['smtp']) && is_array($integrations['smtp']) ? $integrations['smtp'] : array();
	$pass = trim((string) ($_POST['smtp_password'] ?? ''));
	$smtp = array(
		'use_tenant_smtp' => !empty($_POST['use_tenant_smtp']),
		'smtp_host' => trim((string) ($_POST['smtp_host'] ?? '')),
		'smtp_port' => trim((string) ($_POST['smtp_port'] ?? '587')),
		'smtp_encryption' => trim((string) ($_POST['smtp_encryption'] ?? 'tls')),
		'smtp_username' => trim((string) ($_POST['smtp_username'] ?? '')),
		'from_name' => trim((string) ($_POST['from_name'] ?? '')),
		'from_email' => trim((string) ($_POST['from_email'] ?? '')),
	);
	if ($pass !== '') {
		$smtp['smtp_password'] = $pass;
	} elseif (!empty($existing['smtp_password'])) {
		$smtp['smtp_password'] = (string) $existing['smtp_password'];
	}
	$integrations['smtp'] = $smtp;
	epc_integrations_save_tenant_config($pdo, $integrations);
	exit(json_encode(array('status' => true, 'message' => 'SMTP settings saved.')));
}

if ($action === 'test_tenant_smtp') {
	$to = trim((string) ($_POST['test_to'] ?? ''));
	if ($to === '' || !filter_var($to, FILTER_VALIDATE_EMAIL)) {
		exit(json_encode(array('status' => false, 'message' => 'Valid test email required')));
	}
	$subject = 'ECOM AE SMTP test — ' . date('Y-m-d H:i');
	$html = '<p>This is a test message from tenant CP SMTP settings.</p>';
	$result = epc_auth_smtp_send_html($to, $subject, $html);
	if (!empty($result['ok'])) {
		exit(json_encode(array('status' => true, 'message' => 'Test email sent to ' . $to)));
	}
	exit(json_encode(array('status' => false, 'message' => (string) ($result['message'] ?? 'Send failed'))));
}

exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
