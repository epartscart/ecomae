<?php
/**
 * Super CP one-click demo CP login — HMAC token + platform operator session.
 * GET: site_key=demo_260602_ap_13&ts=...&uid=...&sig=...
 */
declare(strict_types=1);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';
require_once __DIR__ . '/content/users/dp_user.php';

function epc_demo_cp_autologin_fail(int $code, string $message): void
{
	http_response_code($code);
	header('Content-Type: text/html; charset=utf-8');
	echo '<!DOCTYPE html><html><head><meta charset="utf-8"><title>Demo CP auto-login</title></head><body style="font-family:sans-serif;padding:24px">';
	echo '<h1>Demo CP auto-login</h1><p>' . htmlspecialchars($message, ENT_QUOTES, 'UTF-8') . '</p>';
	echo '<p><a href="/cp/control/portal/epc_tenant_control_center">Tenant control center</a></p></body></html>';
	exit;
}

if (!function_exists('epc_portal_platform_operator_session_valid') || !epc_portal_platform_operator_session_valid()) {
	epc_demo_cp_autologin_fail(403, 'Super CP operator session required. Log in at /cp/ first.');
}

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? '')));
$ts = (int) ($_GET['ts'] ?? 0);
$uid = (int) ($_GET['uid'] ?? 0);
$sig = (string) ($_GET['sig'] ?? '');

if ($siteKey === '' || $ts <= 0 || $uid <= 0 || $sig === '') {
	epc_demo_cp_autologin_fail(400, 'Missing or invalid token parameters.');
}

if (abs(time() - $ts) > 60) {
	epc_demo_cp_autologin_fail(403, 'Token expired (60s). Click CP again from Tenant control center.');
}

$expectedSig = epc_portal_demo_cp_autologin_sign($siteKey, $ts, $uid);
if (!hash_equals($expectedSig, $sig)) {
	epc_demo_cp_autologin_fail(403, 'Invalid token signature.');
}

if (!epc_portal_platform_operator_session_valid($uid)) {
	epc_demo_cp_autologin_fail(403, 'Token operator mismatch — refresh Tenant control center and try again.');
}

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	epc_demo_cp_autologin_fail(500, 'Platform database unavailable.');
}

epc_portal_demo_ensure_schema($pdo);
epc_portal_tenant_control_ensure_schema($pdo);
$row = epc_portal_tenant_control_get_row($pdo, $siteKey);
if ($row === null || empty($row['is_demo'])) {
	epc_demo_cp_autologin_fail(404, 'Demo tenant not found: ' . $siteKey);
}
if (!epc_portal_tenant_control_row_is_active($row)) {
	epc_demo_cp_autologin_fail(503, 'Demo sandbox is disabled.');
}

$email = strtolower(trim((string) ($row['demo_contact_email'] ?? '')));
if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
	$email = epc_portal_tenant_control_admin_email($row);
}
$password = trim((string) ($row['operator_temp_password'] ?? ''));
if ($password === '') {
	epc_demo_cp_autologin_fail(503, 'No operator password on registry row — reset password in Tenant control center.');
}

$tenantPdo = epc_portal_demo_tenant_pdo($row);
if (!$tenantPdo instanceof PDO) {
	epc_demo_cp_autologin_fail(503, 'Demo tenant database unavailable.');
}

$name = trim((string) ($row['trade_name'] ?? 'Demo Admin'));
$userResult = epc_portal_demo_create_cp_user($tenantPdo, $email, $password, $name);
$userId = (int) ($userResult['user_id'] ?? 0);
if ($userId <= 0) {
	epc_demo_cp_autologin_fail(500, 'Could not resolve demo CP user.');
}

if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
	require_once __DIR__ . '/content/general_pages/epc_portal_shared_erp.php';
	epc_portal_shared_erp_clear_tenant_cookie();
}
if (function_exists('epc_platform_erp_clear_cookie')) {
	require_once __DIR__ . '/content/general_pages/epc_platform_erp_router.php';
	epc_platform_erp_clear_cookie();
}

$GLOBALS['epc_demo_cp_site_key'] = $siteKey;
epc_portal_demo_cp_establish_session($tenantPdo, $userId, 'email');
epc_portal_demo_cp_set_scope_cookie($siteKey);

@error_log('[epc-demo-cp-autologin] operator=' . $uid . ' site=' . $siteKey . ' user=' . $userId);

$dest = epc_portal_demo_cp_post_login_url($siteKey, $row);
header('Location: ' . $dest, true, 302);
exit;
