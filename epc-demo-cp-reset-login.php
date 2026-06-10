<?php
/**
 * Reset demo CP operator login — registry + tenant users table.
 * GET/POST: token=epartscart-deploy-2026&site_key=demo_260602_ap_13&password=ea22Demo69!&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');

$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	echo json_encode(array('ok' => false, 'message' => 'Forbidden'));
	exit;
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? $_POST['site_key'] ?? '')));
$password = (string) ($_GET['password'] ?? $_POST['password'] ?? 'ea22Demo69!');
$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$emailOverride = strtolower(trim((string) ($_GET['email'] ?? $_POST['email'] ?? '')));

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
if ($row === null || empty($row['is_demo'])) {
	http_response_code(404);
	echo json_encode(array('ok' => false, 'message' => 'Demo tenant not found: ' . $siteKey));
	exit;
}

$email = $emailOverride !== '' ? $emailOverride : strtolower(trim((string) ($row['demo_contact_email'] ?? '')));
if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
	$email = epc_portal_tenant_control_admin_email($row);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$cfg = new DP_Config();
$expectedHash = md5($password . $cfg->secret_succession);

$out = array(
	'ok' => true,
	'apply' => $apply,
	'site_key' => $siteKey,
	'email' => $email,
	'is_active' => (int) ($row['is_active'] ?? 1),
	'registry_operator_temp_password' => (string) ($row['operator_temp_password'] ?? ''),
	'registry_demo_contact_email' => (string) ($row['demo_contact_email'] ?? ''),
	'db_name' => (string) ($row['db_name'] ?? ''),
);

$tenantPdo = epc_portal_demo_tenant_pdo($row);
if (!$tenantPdo instanceof PDO) {
	$out['ok'] = false;
	$out['tenant_pdo'] = 'fail';
	echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}
$out['tenant_pdo'] = 'ok';

try {
	$st = $tenantPdo->prepare('SELECT `user_id`, `email`, `email_confirmed`, `unlocked`, `password` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($email));
	$user = $st->fetch(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$user = false;
	$out['user_query_error'] = $e->getMessage();
}

if (!$user) {
	$out['user_exists'] = false;
} else {
	$out['user_exists'] = true;
	$out['user_id'] = (int) $user['user_id'];
	$out['email_confirmed'] = (int) $user['email_confirmed'];
	$out['unlocked'] = (int) $user['unlocked'];
	$out['password_matches_given'] = hash_equals((string) $user['password'], $expectedHash);
	$out['password_matches_registry_temp'] = ($out['registry_operator_temp_password'] !== '')
		&& hash_equals((string) $user['password'], md5((string) $out['registry_operator_temp_password'] . $cfg->secret_succession));

	$gidSt = $tenantPdo->prepare(
		'SELECT g.`id`, g.`value`, g.`for_backend` FROM `users_groups_bind` b
		 INNER JOIN `groups` g ON g.`id` = b.`group_id`
		 WHERE b.`user_id` = ?'
	);
	$gidSt->execute(array((int) $user['user_id']));
	$out['groups'] = $gidSt->fetchAll(PDO::FETCH_ASSOC);
	$out['has_backend_group'] = false;
	foreach ($out['groups'] as $g) {
		if (!empty($g['for_backend'])) {
			$out['has_backend_group'] = true;
			break;
		}
	}
}

$out['tenant_only_block_risk'] = false;
if (function_exists('epc_portal_shared_erp_email_is_tenant_only')) {
	require_once __DIR__ . '/content/general_pages/epc_portal_shared_erp.php';
	$out['tenant_only_block_risk'] = epc_portal_shared_erp_email_is_tenant_only($email, 'email');
}

if (!$apply) {
	$out['message'] = 'Dry run — pass apply=1 to reset password and update registry';
	echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

$name = trim((string) ($row['trade_name'] ?? 'Demo Admin'));
$userResult = epc_portal_demo_create_cp_user($tenantPdo, $email, $password, $name);
if (!empty($userResult['user_id'])) {
	$uid = (int) $userResult['user_id'];
	$out['backend_group_bound'] = epc_portal_demo_ensure_cp_user_backend_groups($tenantPdo, $uid);
}
$pdo->prepare(
	'UPDATE `epc_portal_tenants` SET `operator_temp_password` = ?, `demo_contact_email` = ?, `updated_at` = ? WHERE `site_key` = ?'
)->execute(array($password, $email, time(), $siteKey));

$verifySt = $tenantPdo->prepare('SELECT `password` FROM `users` WHERE `email` = ? LIMIT 1');
$verifySt->execute(array($email));
$stored = (string) ($verifySt->fetchColumn() ?: '');

$out['reset'] = $userResult;
$out['password_applied'] = hash_equals($stored, $expectedHash);
$out['registry_operator_temp_password'] = $password;
$out['working_credentials'] = array('email' => $email, 'password' => $password);
$out['cp_url'] = 'https://www.ecomae.com/cp/demo/' . $siteKey . '/';
$out['message'] = $out['password_applied']
	? 'Demo CP password reset OK'
	: 'Reset ran but password verify failed';

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
