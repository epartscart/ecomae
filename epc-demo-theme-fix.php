<?php
/**
 * Re-apply automotive_spareparts_pro / fashion_retail_namshi theme to existing demo tenants.
 * Full docpart clone (content, modules, templates, lang strings, shop settings) + URL rewrite.
 *
 * GET: token=epartscart-deploy-2026&apply=1
 * Optional: site_key=demo_260602_ap_13, industry=auto_parts, force_clone=1
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
$forceClone = !empty($_GET['force_clone']) || !empty($_POST['force_clone']);
$industryFilter = preg_replace('/[^a-z0-9_]/', '', (string) ($_GET['industry'] ?? $_POST['industry'] ?? ''));
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
	'force_clone' => $forceClone,
	'industry_filter' => $industryFilter !== '' ? $industryFilter : null,
	'updated' => array(),
	'skipped' => array(),
);

foreach ($rows as $row) {
	$siteKeyFilter = preg_replace('/[^a-z0-9_]/', '', (string) ($_GET['site_key'] ?? $_POST['site_key'] ?? ''));
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
	$preset = $presets[$industry];
	$trade = (string) ($row['trade_name'] ?? 'Demo');
	$email = (string) ($row['demo_contact_email'] ?? $row['from_email'] ?? 'demo@ecomae.com');
	$entry = array(
		'site_key' => (string) $row['site_key'],
		'industry' => $industry,
		'storefront_package' => (string) $preset['storefront_package'],
		'storefront_url' => 'https://www.ecomae.com/demo/' . $row['site_key'] . '/',
		'cp_url' => 'https://www.ecomae.com/cp/demo/' . $row['site_key'] . '/',
	);
	if (!$apply) {
		$result['updated'][] = $entry;
		continue;
	}
	$repair = epc_portal_demo_repair_storefront_schema($row, $forceClone);
	$entry['schema'] = $repair;
	if (empty($repair['ok'])) {
		$result['ok'] = false;
		$result['updated'][] = $entry;
		continue;
	}
	$push = epc_portal_demo_push_tenant_settings($pdo, (string) $row['site_key'], $preset, $trade, $email);
	$entry['push'] = $push;
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if ($tenantPdo instanceof PDO) {
		$entry['url_rewrite'] = epc_portal_demo_rewrite_tenant_content_urls($tenantPdo, (string) $row['site_key']);
		if ($industry === 'auto_parts') {
			$entry['header_parity'] = epc_portal_demo_repair_header_parity($tenantPdo, $forceClone);
		}
		if (function_exists('epc_portal_demo_sync_cp_packs')) {
			$entry['cp_menu'] = epc_portal_demo_sync_cp_packs($pdo, (string) $row['site_key']);
		}
	}
	$result['updated'][] = $entry;
}

$result['message'] = $apply
	? 'Demo themes re-applied (active auto_parts demos: ' . count($result['updated']) . ')'
	: 'Dry run — pass apply=1';
echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
