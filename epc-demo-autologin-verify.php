<?php
/**
 * Read-only demo CP autologin diagnostics (deploy token; no CP session required).
 * GET ?token=epartscart-deploy-2026&site_key=demo_260607_ap&uid=5
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? '')));
$operatorId = (int) ($_GET['uid'] ?? 5);
if ($siteKey === '') {
	http_response_code(400);
	echo json_encode(array('ok' => false, 'message' => 'site_key required'));
	exit;
}

$pdo = epc_portal_demo_platform_pdo();
if (!$pdo instanceof PDO) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'message' => 'Platform DB unavailable'));
	exit;
}

epc_portal_demo_ensure_schema($pdo);
epc_portal_tenant_control_ensure_schema($pdo);
$row = epc_portal_tenant_control_get_row($pdo, $siteKey);

$out = array(
	'ok' => false,
	'site_key' => $siteKey,
	'operator_uid_param' => $operatorId,
	'ts_now' => time(),
	'token_ttl_seconds' => 60,
	'checks' => array(),
);

if ($row === null || empty($row['is_demo'])) {
	$out['checks']['registry_demo'] = false;
	$out['message'] = 'Demo tenant not found';
	echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

$out['checks']['registry_demo'] = true;
$out['checks']['registry_active'] = epc_portal_tenant_control_row_is_active($row);
$out['checks']['has_operator_temp_password'] = trim((string) ($row['operator_temp_password'] ?? '')) !== '';
$out['registry'] = array(
	'db_name' => (string) ($row['db_name'] ?? ''),
	'demo_contact_email' => (string) ($row['demo_contact_email'] ?? ''),
	'has_operator_temp_password' => $out['checks']['has_operator_temp_password'],
	'is_active' => (int) ($row['is_active'] ?? 1),
);

$email = strtolower(trim((string) ($row['demo_contact_email'] ?? '')));
if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
	$email = epc_portal_tenant_control_admin_email($row);
}
$out['registry']['cp_email'] = $email;

$tenantPdo = epc_portal_demo_tenant_pdo($row);
$out['checks']['tenant_pdo'] = $tenantPdo instanceof PDO;
if (!$tenantPdo instanceof PDO) {
	$out['message'] = 'Tenant DB unavailable';
	echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

try {
	$backendGroups = epc_portal_demo_cp_backend_group_ids($tenantPdo);
	$out['checks']['backend_groups_exist'] = $backendGroups !== array();
	$out['tenant']['backend_group_ids'] = $backendGroups;

	$st = $tenantPdo->prepare('SELECT `user_id`, `email_confirmed`, `unlocked` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($email));
	$user = $st->fetch(PDO::FETCH_ASSOC);
	$out['checks']['cp_user_exists'] = is_array($user);
	if (is_array($user)) {
		$uid = (int) $user['user_id'];
		$out['tenant']['user_id'] = $uid;
		$gidSt = $tenantPdo->prepare(
			'SELECT g.`id`, g.`for_backend` FROM `users_groups_bind` b
			 INNER JOIN `groups` g ON g.`id` = b.`group_id`
			 WHERE b.`user_id` = ?'
		);
		$gidSt->execute(array($uid));
		$binds = $gidSt->fetchAll(PDO::FETCH_ASSOC);
		$out['tenant']['group_binds'] = $binds;
		$out['checks']['has_backend_group_bind'] = false;
		foreach ($binds as $g) {
			if (!empty($g['for_backend'])) {
				$out['checks']['has_backend_group_bind'] = true;
				break;
			}
		}
	}
} catch (Throwable $e) {
	$out['tenant_error'] = $e->getMessage();
}

$ts = time();
$sig = epc_portal_demo_cp_autologin_sign($siteKey, $ts, $operatorId);
$out['checks']['sig_sample_valid'] = hash_equals(
	$sig,
	epc_portal_demo_cp_autologin_sign($siteKey, $ts, $operatorId)
);
$out['sample_autologin_url'] = epc_portal_demo_cp_autologin_url($siteKey, $operatorId);
$out['post_login_url'] = epc_portal_demo_cp_post_login_url($siteKey, $row);

$out['checks']['autologin_endpoint'] = is_file(__DIR__ . '/epc-demo-cp-autologin.php');
$out['checks']['platform_operator_session_helper'] = function_exists('epc_portal_platform_operator_session_valid');
$out['checks']['demo_cp_nav_scope_helper'] = function_exists('epc_portal_demo_cp_scope_cp_path');
$out['checks']['demo_cp_url_rewrite_helper'] = function_exists('epc_portal_demo_cp_rewrite_nav_urls');
$out['checks']['demo_cp_bare_redirect_helper'] = function_exists('epc_portal_demo_cp_maybe_redirect_bare_path');
$scopedOrders = function_exists('epc_portal_demo_cp_scope_cp_path')
	? epc_portal_demo_cp_scope_cp_path('/cp/shop/orders', $siteKey)
	: '';
$out['checks']['scoped_orders_path_ok'] = $scopedOrders === '/cp/demo/' . $siteKey . '/shop/orders';
$out['scoped_orders_path'] = $scopedOrders;

$out['ok'] = !empty($out['checks']['registry_demo'])
	&& !empty($out['checks']['registry_active'])
	&& !empty($out['checks']['tenant_pdo'])
	&& !empty($out['checks']['backend_groups_exist'])
	&& !empty($out['checks']['cp_user_exists'])
	&& !empty($out['checks']['has_backend_group_bind'])
	&& !empty($out['checks']['has_operator_temp_password'])
	&& !empty($out['checks']['autologin_endpoint'])
	&& !empty($out['checks']['platform_operator_session_helper'])
	&& !empty($out['checks']['demo_cp_nav_scope_helper'])
	&& !empty($out['checks']['scoped_orders_path_ok']);

$out['message'] = $out['ok']
	? 'Demo autologin prerequisites OK — click CP from Tenant control while logged into Super CP'
	: 'One or more autologin prerequisites failed — see checks';

if (empty($out['checks']['has_backend_group_bind'])) {
	$out['repair_hint'] = 'Run epc-demo-cp-reset-login.php?token=...&site_key=' . $siteKey . '&apply=1';
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
