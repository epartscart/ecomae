<?php
/**
 * Read-only client ERP login diagnostics (deploy token; no CP session required).
 * GET ?token=epartscart-deploy-2026&site_key=asapcustom
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_shared_erp.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';
require_once __DIR__ . '/content/general_pages/epc_client_erp_router.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
if ($siteKey === '') {
	http_response_code(400);
	echo json_encode(array('ok' => false, 'message' => 'site_key required'));
	exit;
}

$out = array(
	'ok' => false,
	'site_key' => $siteKey,
	'checks' => array(),
);

$pdo = epc_portal_platform_pdo();
$out['checks']['platform_pdo'] = $pdo instanceof PDO;
$row = epc_portal_shared_erp_load_by_site_key($siteKey, $pdo);
$out['checks']['registry_shared_erp'] = $row !== null;
if ($row === null) {
	$out['message'] = 'Shared ERP tenant not found or inactive';
	echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

$out['registry'] = array(
	'db_name' => (string) ($row['db_name'] ?? ''),
	'trade_name' => (string) ($row['trade_name'] ?? ''),
	'erp_only_shared' => (int) ($row['erp_only_shared'] ?? 0),
	'hostname' => (string) ($row['hostname'] ?? ''),
);

$tenantPdo = epc_portal_shared_erp_tenant_pdo($row);
$out['checks']['tenant_pdo'] = $tenantPdo instanceof PDO;
if (!$tenantPdo instanceof PDO) {
	$out['message'] = 'Tenant DB unavailable';
	echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

try {
	$tables = (int) $tenantPdo->query('SHOW TABLES')->rowCount();
	$out['tenant']['tables'] = $tables;
	$out['checks']['has_tables'] = $tables > 0;

	require_once __DIR__ . '/content/shop/finance/epc_erp_schema.php';
	epc_erp_ensure_schema($tenantPdo);
	$out['checks']['erp_schema'] = true;

	$hasPriceSettings = false;
	try {
		$tenantPdo->query('SELECT 1 FROM `epc_price_settings` LIMIT 1');
		$hasPriceSettings = true;
	} catch (Throwable $e) {
		$hasPriceSettings = false;
	}
	$out['checks']['epc_price_settings'] = $hasPriceSettings;

	$email = epc_portal_tenant_control_admin_email($row);
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		$email = trim((string) ($row['from_email'] ?? ''));
	}
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		$email = $siteKey . '_admin@ecomae.com';
	}
	$out['registry']['cp_email'] = $email;
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

$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['REQUEST_URI'] = '/cp/client-erp/' . $siteKey . '/';
unset($GLOBALS['epc_client_erp_context'], $GLOBALS['epc_client_erp_site_key'], $GLOBALS['epc_client_erp_tenant_row']);
epc_client_erp_bootstrap();
$simCfg = new DP_Config();
if (is_array($row) && function_exists('epc_portal_shared_erp_apply_row_config')) {
	epc_portal_shared_erp_apply_row_config($simCfg, $row);
}
$out['checks']['client_erp_bootstrap'] = array(
	'context_active' => epc_client_erp_is_active(),
	'config_db' => (string) ($simCfg->db ?? ''),
	'expected_db' => (string) ($row['db_name'] ?? ''),
	'match' => strcasecmp((string) ($simCfg->db ?? ''), (string) ($row['db_name'] ?? '')) === 0,
);
$out['login_url'] = 'https://www.ecomae.com' . epc_client_erp_login_url($siteKey);
$out['shell_url'] = 'https://www.ecomae.com' . epc_client_erp_shell_url($siteKey);

$ctx = stream_context_create(array(
	'http' => array('timeout' => 20, 'ignore_errors' => true),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
));
$probeBody = @file_get_contents($out['login_url'], false, $ctx);
$probeCode = 0;
if (isset($http_response_header) && is_array($http_response_header)) {
	foreach ($http_response_header as $h) {
		if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
			$probeCode = (int) $m[1];
		}
	}
}
$probeText = is_string($probeBody) ? $probeBody : '';
$out['live_probe'] = array(
	'http' => $probeCode,
	'bytes' => strlen($probeText),
	'has_login_form' => stripos($probeText, 'authentication') !== false || stripos($probeText, 'login') !== false,
	'php_fatal' => (bool) preg_match('/Fatal error|Parse error|Uncaught (?:Error|Exception)/i', $probeText),
);

$out['ok'] = !empty($out['checks']['registry_shared_erp'])
	&& !empty($out['checks']['tenant_pdo'])
	&& !empty($out['checks']['has_tables'])
	&& !empty($out['checks']['erp_schema'])
	&& !empty($out['checks']['cp_user_exists'])
	&& !empty($out['checks']['has_backend_group_bind'])
	&& !empty($out['checks']['client_erp_bootstrap']['match'])
	&& empty($out['live_probe']['php_fatal'])
	&& (int) ($out['live_probe']['http'] ?? 0) < 500;

$out['message'] = $out['ok']
	? 'Client ERP login prerequisites OK'
	: 'One or more client ERP prerequisites failed — see checks';

if (empty($out['checks']['cp_user_exists']) || empty($out['checks']['has_backend_group_bind'])) {
	$out['repair_hint'] = 'Run epc-erp-tenant-provision.php?token=...&site_key=' . $siteKey . '&apply=1';
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
