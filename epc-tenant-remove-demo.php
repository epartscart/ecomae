<?php
/**
 * Remove a single demo tenant from Super CP registry (safe; never touches live tenants).
 * https://www.ecomae.com/epc-tenant-remove-demo.php?token=...&site_key=demo_260607_ap_2
 * Add apply=1 to execute; drop_db=1 (default) drops isolated demo MySQL DB.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(300);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

$protected = array('electronicae', 'epartscart', 'stylenlook', 'taxofinca', 'thejewellerytrend', 'ecomae');
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? $_POST['site_key'] ?? 'demo_260607_ap_2'))));
$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$dropDb = !array_key_exists('drop_db', $_GET) && !array_key_exists('drop_db', $_POST)
	? true
	: (!empty($_GET['drop_db']) || !empty($_POST['drop_db']));

$result = array('ok' => false, 'apply' => $apply, 'site_key' => $siteKey, 'drop_db' => $dropDb);

if ($siteKey === '' || in_array($siteKey, $protected, true)) {
	http_response_code(400);
	$result['message'] = 'Refusing protected or empty site_key';
	exit(json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
}

try {
	$pdo = epc_portal_demo_platform_pdo();
	if (!$pdo instanceof PDO) {
		http_response_code(500);
		$result['message'] = 'Platform DB unavailable';
		exit(json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
	}
	epc_portal_demo_ensure_schema($pdo);
} catch (Throwable $e) {
	http_response_code(500);
	$result['message'] = 'Platform DB error: ' . $e->getMessage();
	exit(json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
}

$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
$st->execute(array($siteKey));
$row = $st->fetch(PDO::FETCH_ASSOC);
if (!$row) {
	$result['ok'] = true;
	$result['found'] = false;
	$result['message'] = 'Tenant not in registry (already removed)';
	echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

$result['found'] = true;
$result['before'] = array(
	'hostname' => (string) ($row['hostname'] ?? ''),
	'industry_code' => (string) ($row['industry_code'] ?? ''),
	'status' => (string) ($row['status'] ?? ''),
	'db_name' => (string) ($row['db_name'] ?? ''),
	'is_demo' => (int) ($row['is_demo'] ?? 0),
);

$isDemo = !empty($row['is_demo']) || strncmp($siteKey, 'demo_', 5) === 0;
if (!$isDemo) {
	http_response_code(400);
	$result['message'] = 'Refusing non-demo tenant';
	exit(json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
}

if (!$apply) {
	$result['ok'] = true;
	$result['message'] = 'Dry run — add apply=1 to remove this demo from registry';
	echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

if (!empty($row['is_demo'])) {
	if (!$dropDb) {
		$pdo->prepare('DELETE FROM `epc_portal_deploy_targets` WHERE `site_key` = ?')->execute(array($siteKey));
		$pdo->prepare('DELETE FROM `epc_portal_tenants` WHERE `site_key` = ?')->execute(array($siteKey));
		$pdo->prepare('UPDATE `epc_portal_demo_requests` SET `status` = ? WHERE `site_key` = ?')->execute(array('deleted', $siteKey));
		$result['ok'] = true;
		$result['delete'] = array('ok' => true, 'message' => 'Registry row removed (DB kept)', 'db_drop' => array('ok' => false, 'skipped' => true));
	} else {
		$result['delete'] = epc_portal_demo_force_delete($pdo, $siteKey);
		$result['ok'] = !empty($result['delete']['ok']);
	}
} else {
	$drop = array('ok' => false, 'skipped' => true);
	if ($dropDb) {
		$drop = epc_portal_demo_drop_database($row);
	}
	$pdo->prepare('DELETE FROM `epc_portal_deploy_targets` WHERE `site_key` = ?')->execute(array($siteKey));
	$pdo->prepare('DELETE FROM `epc_portal_tenants` WHERE `site_key` = ?')->execute(array($siteKey));
	$pdo->prepare('UPDATE `epc_portal_demo_requests` SET `status` = ? WHERE `site_key` = ?')->execute(array('deleted', $siteKey));
	$dbName = trim((string) ($row['db_name'] ?? ''));
	if ($dbName !== '') {
		$pdo->prepare(
			'UPDATE `epc_portal_demo_db_pool` SET `status` = ?, `claimed_by_site_key` = ?, `claimed_at` = 0 WHERE `db_name` = ?'
		)->execute(array('ready', '', $dbName));
	}
	$result['ok'] = true;
	$result['delete'] = array('ok' => true, 'message' => 'Demo registry purge (non-is_demo flag)', 'db_drop' => $drop);
}

$st2 = $pdo->prepare('SELECT COUNT(*) FROM `epc_portal_tenants` WHERE `site_key` = ?');
$st2->execute(array($siteKey));
$result['registry_remaining'] = (int) $st2->fetchColumn();
$result['message'] = $result['ok']
	? 'Demo tenant removed from registry'
	: (string) ($result['delete']['message'] ?? 'Delete failed');

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
