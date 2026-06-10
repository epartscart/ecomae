<?php
/**
 * Probe tenant demo access load/save (token auth, no CP session).
 * https://www.ecomae.com/epc-demo-access-probe.php?token=...&site_key=asap&save=1&email=...&password=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

try {
	$pdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'step' => 'platform_pdo', 'message' => $e->getMessage())));
}

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? 'asap')));
$row = epc_portal_tenant_control_get_row($pdo, $siteKey);
if ($row === null) {
	exit(json_encode(array('ok' => false, 'step' => 'registry', 'message' => 'Tenant not found')));
}

$tenantPdo = epc_portal_tenant_control_tenant_pdo($row);
$probe = array(
	'ok' => true,
	'site_key' => $siteKey,
	'registry' => array(
		'db_name' => (string) ($row['db_name'] ?? ''),
		'db_user' => (string) ($row['db_user'] ?? ''),
		'db_password_len' => strlen((string) ($row['db_password'] ?? '')),
		'is_demo' => (int) (!empty($row['is_demo'])),
		'erp_only_shared' => (int) (!empty($row['erp_only_shared'])),
		'login_email' => epc_portal_tenant_control_admin_email($row),
	),
	'tenant_db_ok' => $tenantPdo instanceof PDO,
	'tenant_type' => epc_portal_tenant_control_tenant_type($row),
);

if ($tenantPdo instanceof PDO) {
	try {
		$n = (int) $tenantPdo->query('SELECT COUNT(*) FROM `users`')->fetchColumn();
		$probe['users_count'] = $n;
		$probe['cp_users'] = epc_portal_tenant_control_list_cp_backend_users($tenantPdo);
	} catch (Throwable $e) {
		$probe['users_error'] = $e->getMessage();
	}
} else {
	// Direct mysqli probe with registry creds
	$db = trim((string) ($row['db_name'] ?? ''));
	$user = trim((string) ($row['db_user'] ?? ''));
	$pass = (string) ($row['db_password'] ?? '');
	$m = @new mysqli('127.0.0.1', $user, $pass, $db);
	$probe['direct_mysqli'] = $m->connect_errno ? $m->connect_error : 'OK';
}

if (!empty($_GET['save'])) {
	$email = strtolower(trim((string) ($_GET['email'] ?? $probe['registry']['login_email'])));
	$password = isset($_GET['password']) ? (string) $_GET['password'] : null;
	$isDemo = array_key_exists('is_demo', $_GET)
		? (!empty($_GET['is_demo']) && (string) $_GET['is_demo'] !== '0' ? 1 : 0)
		: null;
	try {
		$result = epc_portal_tenant_control_demo_access_save($pdo, $siteKey, $email, $password, $isDemo);
		$probe['save'] = $result;
	} catch (Throwable $e) {
		$probe['save'] = array('ok' => false, 'exception' => $e->getMessage(), 'file' => $e->getFile(), 'line' => $e->getLine());
	}
}

echo json_encode($probe, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
