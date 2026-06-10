<?php
/**
 * Portal settings AJAX + one-click deploy.
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
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_cp_menu.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_theme_templates.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
if ((int) DP_User::getAdminId() <= 0) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Admin login required')));
}

$pdo = $db_link;
$cfg = $DP_Config;

epc_portal_db_ensure($pdo);
$action = isset($_POST['action']) ? (string) $_POST['action'] : '';

if ($action === 'menu_items') {
	$groupId = (int) ($_POST['group_id'] ?? 0);
	if ($groupId <= 0) {
		exit(json_encode(array('status' => false, 'message' => 'Invalid group')));
	}
	exit(json_encode(array(
		'status' => true,
		'items' => epc_portal_cp_menu_items_for_settings($pdo, $groupId),
	)));
}

if ($action === 'save_settings') {
	$packs = isset($_POST['enabled_packs']) ? (array) $_POST['enabled_packs'] : array('core');
	$packs = array_values(array_unique(array_filter(array_map(function ($p) {
		return preg_replace('/[^a-z0-9_]/', '', strtolower((string) $p));
	}, $packs))));
	$industryCode = preg_replace('/[^a-z0-9_]/', '', (string) ($_POST['industry_code'] ?? 'auto_parts'));
	if (epc_portal_is_client_hostname()) {
		$packs = array_values(array_filter($packs, function ($p) {
			return $p !== 'super_platform';
		}));
		if ($industryCode === 'platform_host') {
			$industryCode = 'auto_parts';
		}
	}
	$erpModules = epc_portal_erp_modules_from_post($_POST);
	$accessModePost = isset($_POST['access_mode']) ? (string) $_POST['access_mode'] : 'full';
	if ($accessModePost === 'full_commerce') {
		$accessModePost = 'full';
	}
	if (count($erpModules) === 0) {
		$erpModules = epc_portal_erp_modules_default_ids($accessModePost);
	}
	$data = array(
		'industry_code' => $industryCode,
		'access_mode' => $accessModePost,
		'erp_modules' => $erpModules,
		'cp_default_lang' => isset($_POST['cp_default_lang']) ? (string) $_POST['cp_default_lang'] : 'en',
		'system_name' => isset($_POST['system_name']) ? $_POST['system_name'] : '',
		'hub_name' => isset($_POST['hub_name']) ? $_POST['hub_name'] : '',
		'tagline' => isset($_POST['tagline']) ? $_POST['tagline'] : '',
		'domain_path' => isset($_POST['domain_path']) ? $_POST['domain_path'] : '',
		'contact' => array(
			'trade_name' => isset($_POST['contact_trade_name']) ? (string) $_POST['contact_trade_name'] : '',
			'from_name' => isset($_POST['contact_trade_name']) ? (string) $_POST['contact_trade_name'] : '',
			'from_email' => isset($_POST['contact_from_email']) ? (string) $_POST['contact_from_email'] : '',
			'admin_email' => isset($_POST['contact_admin_email']) ? (string) $_POST['contact_admin_email'] : '',
			'contact_phone' => isset($_POST['contact_phone']) ? (string) $_POST['contact_phone'] : '',
			'whatsapp_number' => isset($_POST['contact_phone']) ? (string) $_POST['contact_phone'] : '',
			'head_office_address' => isset($_POST['contact_head_office_address']) ? (string) $_POST['contact_head_office_address'] : '',
			'head_office_email' => isset($_POST['contact_from_email']) ? (string) $_POST['contact_from_email'] : '',
			'city' => isset($_POST['contact_city']) ? (string) $_POST['contact_city'] : '',
			'country' => isset($_POST['contact_country']) ? (string) $_POST['contact_country'] : '',
		),
		'enabled_packs' => $packs,
		'theme_template' => isset($_POST['theme_template']) ? (string) $_POST['theme_template'] : 'classic',
		'cp_menu' => array(
			'hidden_groups' => isset($_POST['hidden_groups']) ? (array) $_POST['hidden_groups'] : array(),
			'hidden_items' => isset($_POST['hidden_items']) ? (array) $_POST['hidden_items'] : array(),
		),
	);
	epc_portal_save_site_settings($pdo, $data);
	$extra = '';
	$targetHost = strtolower(trim((string) ($_POST['target_host'] ?? '')));
	if ($targetHost !== '' && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		$push = epc_portal_push_settings_to_tenant_host($pdo, $targetHost, $data);
		if (!$push['ok']) {
			exit(json_encode(array('status' => false, 'message' => 'Saved locally but client push failed: ' . $push['message'])));
		}
		$extra = ' ' . $push['message'];
	}
	exit(json_encode(array('status' => true, 'message' => 'Settings saved.' . $extra)));
}

if ($action === 'deploy_site') {
	if (!epc_portal_can_deploy_portal_package()) {
		exit(json_encode(array('status' => false, 'message' => 'Deploy is only available on the ecomae platform control panel.')));
	}
	$site_key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));
	$st = $pdo->prepare('SELECT * FROM `epc_portal_deploy_targets` WHERE `site_key` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($site_key));
	$target = $st->fetch(PDO::FETCH_ASSOC);
	if (!$target) {
		exit(json_encode(array('status' => false, 'message' => 'Unknown deploy target')));
	}

	$zipCandidates = array(
		'/tmp/docpart-epartscart-site.zip',
		$_SERVER['DOCUMENT_ROOT'] . '/../docpart-site.zip',
	);
	$zipPath = '';
	foreach ($zipCandidates as $candidate) {
		if (is_file($candidate) && filesize($candidate) > 1000) {
			$zipPath = $candidate;
			break;
		}
	}
	if ($zipPath === '') {
		exit(json_encode(array('status' => false, 'message' => 'Deploy zip not found on server. Run hotfix upload first.')));
	}

	$token = epc_deploy_token();
	$data = file_get_contents($zipPath);
	$chunkSize = 150000;
	$log = array();
	$idx = 0;
	for ($off = 0; $off < strlen($data); $off += $chunkSize) {
		$part = substr($data, $off, $chunkSize);
		$body = http_build_query(array(
			'token' => $token,
			'index' => (string) $idx,
			'data' => base64_encode($part),
			'final' => ($off + $chunkSize >= strlen($data)) ? '1' : '0',
		));
		$ctx = stream_context_create(array(
			'http' => array(
				'method' => 'POST',
				'header' => "Content-Type: application/x-www-form-urlencoded\r\n",
				'content' => $body,
				'timeout' => 300,
			),
			'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
		));
		$resp = @file_get_contents($target['chunk_url'], false, $ctx);
		$log[] = 'chunk ' . $idx . ': ' . trim((string) $resp);
		$idx++;
	}

	$extractUrl = $target['extract_url'];
	if (strpos($extractUrl, 'token=') === false) {
		$extractUrl .= (strpos($extractUrl, '?') !== false ? '&' : '?') . 'token=' . urlencode($token);
	}
	$extractResp = @file_get_contents($extractUrl, false, stream_context_create(array(
		'http' => array('timeout' => 300),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	)));

	$setupResp = @file_get_contents($target['setup_url'], false, stream_context_create(array(
		'http' => array('timeout' => 120),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	)));

	$ok = ($extractResp !== false);
	$status = $ok ? 'ok' : 'failed';
	$message = $ok ? 'Deploy completed for ' . $target['hostname'] : 'Extract failed — check SSL/vhost for ' . $target['hostname'];
	$pdo->prepare(
		'UPDATE `epc_portal_deploy_targets` SET `last_deploy_at` = ?, `last_deploy_status` = ?, `last_deploy_message` = ? WHERE `id` = ?'
	)->execute(array(time(), $status, $message . "\n" . substr((string) $extractResp, 0, 500), (int) $target['id']));

	exit(json_encode(array(
		'status' => $ok,
		'message' => $message,
		'log' => implode("\n", $log) . "\n\nExtract:\n" . substr((string) $extractResp, 0, 1500) . "\n\nSetup:\n" . substr((string) $setupResp, 0, 800),
	)));
}

if ($action === 'tenant_set_active' || $action === 'tenant_reset_password') {
	if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
		http_response_code(403);
		exit(json_encode(array('status' => false, 'message' => 'Super CP only')));
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));
	if ($siteKey === '') {
		exit(json_encode(array('status' => false, 'message' => 'Invalid site key')));
	}
	if ($action === 'tenant_set_active') {
		$active = !empty($_POST['active']) && (string) $_POST['active'] !== '0';
		$result = epc_portal_tenant_control_set_active($pdo, $siteKey, $active);
		exit(json_encode(array(
			'status' => !empty($result['ok']),
			'message' => (string) ($result['message'] ?? ''),
			'is_active' => (int) ($result['is_active'] ?? 0),
		)));
	}
	$result = epc_portal_tenant_control_reset_cp_password($pdo, $siteKey);
	exit(json_encode(array(
		'status' => !empty($result['ok']),
		'message' => (string) ($result['message'] ?? ''),
		'email' => (string) ($result['email'] ?? ''),
		'password' => (string) ($result['password'] ?? ''),
	)));
}

if ($action === 'tenant_demo_access_load' || $action === 'tenant_demo_access_save') {
	if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
		http_response_code(403);
		exit(json_encode(array('status' => false, 'message' => 'Super CP only')));
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));
	if ($siteKey === '') {
		exit(json_encode(array('status' => false, 'message' => 'Invalid site key')));
	}
	if ($action === 'tenant_demo_access_load') {
		try {
			$result = epc_portal_tenant_control_demo_access_load($pdo, $siteKey);
		} catch (Throwable $e) {
			exit(json_encode(array('status' => false, 'message' => 'Load failed: ' . $e->getMessage())));
		}
		exit(json_encode(array(
			'status' => !empty($result['ok']),
			'message' => (string) ($result['message'] ?? ''),
			'tenant' => $result,
		)));
	}
	$email = strtolower(trim((string) ($_POST['email'] ?? '')));
	$password = isset($_POST['password']) ? (string) $_POST['password'] : null;
	if ($password !== null && trim($password) === '') {
		$password = null;
	}
	$isDemo = null;
	if (array_key_exists('is_demo', $_POST)) {
		$isDemo = !empty($_POST['is_demo']) && (string) $_POST['is_demo'] !== '0' ? 1 : 0;
	}
	$resetOnly = !empty($_POST['reset_password']);
	if ($resetOnly && $password === null) {
		$result = epc_portal_tenant_control_reset_cp_password($pdo, $siteKey);
		if (!empty($result['ok'])) {
			epc_portal_tenant_control_audit_log($pdo, $siteKey, 'demo_access_password_reset', array('email' => (string) ($result['email'] ?? '')));
		}
		exit(json_encode(array(
			'status' => !empty($result['ok']),
			'message' => (string) ($result['message'] ?? ''),
			'email' => (string) ($result['email'] ?? ''),
			'password' => (string) ($result['password'] ?? ''),
		)));
	}
	try {
		$result = epc_portal_tenant_control_demo_access_save($pdo, $siteKey, $email, $password, $isDemo);
	} catch (Throwable $e) {
		exit(json_encode(array('status' => false, 'message' => 'Save failed: ' . $e->getMessage())));
	}
	exit(json_encode(array(
		'status' => !empty($result['ok']),
		'message' => (string) ($result['message'] ?? ''),
		'email' => (string) ($result['email'] ?? ''),
		'password' => (string) ($result['password'] ?? ''),
		'is_demo' => (int) ($result['is_demo'] ?? 0),
	)));
}

exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
