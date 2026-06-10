<?php
/**
 * Rewrite cloned epartscart.com / bare /en/ links in demo tenant DB content.
 * GET: token=epartscart-deploy-2026&apply=1&site_key=demo_260601_ap_1
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
$pdo = epc_portal_demo_platform_pdo();
if (!$pdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'Platform DB unavailable')));
}

epc_portal_demo_ensure_schema($pdo);
$siteKeyFilter = preg_replace('/[^a-z0-9_]/', '', (string) ($_GET['site_key'] ?? $_POST['site_key'] ?? ''));
$st = $pdo->query('SELECT * FROM `epc_portal_tenants` WHERE `is_demo` = 1 ORDER BY `id` DESC');
$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();

$result = array('ok' => true, 'apply' => $apply, 'updated' => array(), 'skipped' => array());

foreach ($rows as $row) {
	if ($siteKeyFilter !== '' && (string) ($row['site_key'] ?? '') !== $siteKeyFilter) {
		continue;
	}
	$siteKey = (string) ($row['site_key'] ?? '');
	$entry = array('site_key' => $siteKey);
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$tenantPdo instanceof PDO) {
		$entry['error'] = 'Tenant DB connect failed';
		$result['skipped'][] = $entry;
		continue;
	}
	if (!$apply) {
		$entry['dry_run'] = true;
		$result['updated'][] = $entry;
		continue;
	}
	$entry['rewrite'] = epc_portal_demo_rewrite_tenant_content_urls($tenantPdo, $siteKey);
	$result['updated'][] = $entry;
}

$result['message'] = $apply ? 'Demo content URLs rewritten' : 'Dry run — pass apply=1';
echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
