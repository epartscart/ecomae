<?php
/**
 * Delete wrong demo tenants and reprovision auto_parts sandbox with automotive_spareparts_pro theme.
 * GET: token=epartscart-deploy-2026&apply=1&email=test@example.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');
set_time_limit(300);

$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$email = strtolower(trim((string) ($_GET['email'] ?? $_POST['email'] ?? 'demo-reprovision@ecomae.com')));
$name = trim((string) ($_GET['name'] ?? $_POST['name'] ?? 'Demo Reprovision'));
$company = trim((string) ($_GET['company'] ?? $_POST['company'] ?? 'ECOM AE QA Demo'));

$pdo = epc_portal_demo_platform_pdo();
if (!$pdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'Platform DB unavailable')));
}

epc_portal_demo_ensure_schema($pdo);
$st = $pdo->query('SELECT `site_key`, `industry_code`, `is_demo`, `demo_contact_email`, `db_name` FROM `epc_portal_tenants` WHERE `is_demo` = 1 ORDER BY `id` DESC');
$demos = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();

$result = array(
	'ok' => true,
	'apply' => $apply,
	'existing_demos' => $demos,
	'deleted' => array(),
	'provision' => null,
);

if (!$apply) {
	$result['message'] = 'Dry run — pass apply=1 to delete all active demos and reprovision auto_parts';
	echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

foreach ($demos as $row) {
	$siteKey = (string) ($row['site_key'] ?? '');
	if ($siteKey === '') {
		continue;
	}
	try {
		$pdo->prepare('DELETE FROM `epc_portal_tenants` WHERE `site_key` = ?')->execute(array($siteKey));
		$dbName = (string) ($row['db_name'] ?? '');
		if ($dbName !== '') {
			try {
				$pdo->exec('DROP DATABASE IF EXISTS `' . str_replace('`', '', $dbName) . '`');
			} catch (Throwable $e) {
			}
		}
		$result['deleted'][] = $siteKey;
	} catch (Throwable $e) {
		$result['deleted'][] = $siteKey . ' (error: ' . $e->getMessage() . ')';
	}
}

$prov = epc_portal_demo_provision($pdo, array(
	'contact_name' => $name,
	'contact_email' => $email,
	'company' => $company,
	'industry_code' => 'auto_parts',
	'terms' => true,
));
$result['provision'] = $prov;
$result['ok'] = !empty($prov['ok']);
$result['message'] = $result['ok']
	? 'Reprovisioned auto_parts demo with automotive_spareparts_pro'
	: (string) ($prov['message'] ?? 'Provision failed');

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
