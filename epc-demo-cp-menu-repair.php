<?php
/**
 * Repair demo tenant CP sidebar — commerce packs, control_items clone, login reset option.
 *
 * GET: token=epartscart-deploy-2026&apply=1
 * Optional: site_key=demo_260602_ap_13, industry=auto_parts, force_menu=1, reset_login=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');

$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$forceMenu = !empty($_GET['force_menu']) || !empty($_POST['force_menu']);
$resetLogin = !empty($_GET['reset_login']) || !empty($_POST['reset_login']);
$industryFilter = preg_replace('/[^a-z0-9_]/', '', (string) ($_GET['industry'] ?? $_POST['industry'] ?? ''));
$siteKeyFilter = preg_replace('/[^a-z0-9_]/', '', (string) ($_GET['site_key'] ?? $_POST['site_key'] ?? ''));
$password = (string) ($_GET['password'] ?? $_POST['password'] ?? 'ea22Demo69!');

$pdo = epc_portal_demo_platform_pdo();
if (!$pdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'Platform DB unavailable')));
}

epc_portal_demo_ensure_schema($pdo);
$presets = epc_portal_demo_industry_presets();
$now = time();
$st = $pdo->query(
	'SELECT * FROM `epc_portal_tenants` WHERE `is_demo` = 1
	 AND `status` IN (\'dns_pending\', \'live\')
	 AND (`demo_expires_at` = 0 OR `demo_expires_at` > ' . (int) $now . ')
	 ORDER BY `id` DESC'
);
$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();

$result = array(
	'ok' => true,
	'apply' => $apply,
	'force_menu' => $forceMenu,
	'repaired' => array(),
	'skipped' => array(),
);

foreach ($rows as $row) {
	if ($siteKeyFilter !== '' && (string) ($row['site_key'] ?? '') !== $siteKeyFilter) {
		continue;
	}
	$industry = (string) ($row['industry_code'] ?? '');
	if ($industryFilter !== '' && $industry !== $industryFilter) {
		$result['skipped'][] = array('site_key' => $row['site_key'], 'reason' => 'industry filter');
		continue;
	}
	if (!isset($presets[$industry])) {
		$result['skipped'][] = array('site_key' => $row['site_key'], 'reason' => 'unsupported industry');
		continue;
	}
	$siteKey = (string) $row['site_key'];
	$entry = array(
		'site_key' => $siteKey,
		'industry' => $industry,
		'db_name' => (string) ($row['db_name'] ?? ''),
		'cp_url' => 'https://www.ecomae.com/cp/demo/' . $siteKey . '/',
	);
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$tenantPdo instanceof PDO) {
		$entry['ok'] = false;
		$entry['message'] = 'Tenant DB connect failed';
		$result['ok'] = false;
		$result['repaired'][] = $entry;
		continue;
	}
	try {
		$entry['counts_before'] = array(
			'control_items' => (int) $tenantPdo->query('SELECT COUNT(*) FROM `control_items`')->fetchColumn(),
			'control_groups' => (int) $tenantPdo->query('SELECT COUNT(*) FROM `control_groups`')->fetchColumn(),
		);
		$stSet = $tenantPdo->prepare('SELECT `industry_code`, `enabled_packs_json` FROM `epc_portal_site_settings` WHERE `host` IN (\'www.ecomae.com\', \'ecomae.com\') LIMIT 1');
		$stSet->execute();
		$entry['settings_before'] = $stSet->fetch(PDO::FETCH_ASSOC) ?: null;
	} catch (Throwable $e) {
		$entry['probe_error'] = $e->getMessage();
	}
	if (!$apply) {
		$result['repaired'][] = $entry;
		continue;
	}
	$preset = $presets[$industry];
	$entry['sync'] = epc_portal_demo_sync_cp_packs($pdo, $siteKey, $preset);
	if ($forceMenu) {
		$entry['menu_force'] = epc_portal_demo_repair_cp_menu($tenantPdo, true);
	}
	if ($resetLogin) {
		$email = strtolower(trim((string) ($row['demo_contact_email'] ?? '')));
		if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
			$email = 'myicdxb84@gmail.com';
		}
		$entry['login_reset'] = epc_portal_demo_create_cp_user(
			$tenantPdo,
			$email,
			$password,
			(string) ($row['trade_name'] ?? 'Demo Admin')
		);
	}
	try {
		$entry['counts_after'] = array(
			'control_items' => (int) $tenantPdo->query('SELECT COUNT(*) FROM `control_items`')->fetchColumn(),
			'control_groups' => (int) $tenantPdo->query('SELECT COUNT(*) FROM `control_groups`')->fetchColumn(),
		);
		$stAfter = $tenantPdo->prepare('SELECT `industry_code`, `enabled_packs_json` FROM `epc_portal_site_settings` WHERE `host` IN (\'www.ecomae.com\', \'ecomae.com\') LIMIT 1');
		$stAfter->execute();
		$entry['settings_after'] = $stAfter->fetch(PDO::FETCH_ASSOC) ?: null;
	} catch (Throwable $e) {
		$entry['probe_after_error'] = $e->getMessage();
	}
	$result['repaired'][] = $entry;
}

$result['message'] = $apply
	? 'Demo CP menu repair applied (' . count($result['repaired']) . ' tenants)'
	: 'Dry run — pass apply=1';
echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
