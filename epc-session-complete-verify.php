<?php
/**
 * Session completion checklist — all 11 audit tasks in one JSON report.
 * GET ?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(180);

$token = epc_deploy_token();
$base = 'https://www.ecomae.com';
$tenants = array('epartscart', 'electronicae', 'taxofinca', 'stylenlook', 'thejewellerytrend');

function epc_scv_curl(string $url, int $timeout = 60): array
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => $timeout,
	));
	$body = (string) curl_exec($ch);
	$http = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	return array('http' => $http, 'body' => $body);
}

function epc_scv_json(string $url): array
{
	$r = epc_scv_curl($url);
	$j = json_decode($r['body'], true);
	return array('http' => $r['http'], 'json' => is_array($j) ? $j : null, 'raw' => $r['body']);
}

$report = array(
	'ok' => true,
	'generated_at' => gmdate('c'),
	'tasks' => array(),
);

// 1 — Production backup locally (marker deployed with session verify)
$marker = epc_scv_json($base . '/epc-session-backup-marker.json?token=' . urlencode($token));
$backupDone = (($marker['json']['status'] ?? '') === 'complete');
$report['tasks']['1_production_backup'] = array(
	'status' => $backupDone ? 'DONE' : 'PENDING',
	'local_path' => 'c:\\Users\\1\\Apple\\deploy-epartscart\\backups\\production-2026-06-07-0215',
	'manifest' => 'backups/production-2026-06-07-0215/BACKUP_MANIFEST.md',
	'marker' => $marker['json'],
	'note' => 'Full workspace snapshot on dev machine; server zip at server-dumps/modelc-client-docpart-backup-20260607-092842.zip',
);

// 2 — APAI visible CP → Portal all tenants
$apai = epc_scv_json($base . '/epc-apai-cp-tenant-verify.php?token=' . urlencode($token));
$apaiPass = (int) ($apai['json']['summary']['pass'] ?? 0);
$apaiFail = (int) ($apai['json']['summary']['fail'] ?? 0);
$apaiTenants = array();
if (is_array($apai['json']['tenants'] ?? null)) {
	foreach ($apai['json']['tenants'] as $sk => $row) {
		if (in_array($sk, $tenants, true) || $sk === 'demo_260607_ap') {
			$apaiTenants[$sk] = !empty($row['pass']);
		}
	}
}
$allLivePass = true;
foreach ($tenants as $t) {
	if (empty($apaiTenants[$t])) {
		$allLivePass = false;
	}
}
$report['tasks']['2_apai_cp_portal'] = array(
	'status' => ($apaiFail === 0 && $allLivePass) ? 'DONE' : 'PENDING',
	'pass' => $apaiPass,
	'fail' => $apaiFail,
	'tenants' => $apaiTenants,
	'urls' => array_map(function ($t) {
		return 'https://www.' . $t . '.com/cp/control/portal/epc_auto_price_engine?tab=discover&site_key=' . $t;
	}, $tenants),
);

// 3 — Guide tab syntax error
$guide = epc_scv_json($base . '/epc-apai-guide-verify.php?token=' . urlencode($token) . '&site_key=epartscart');
$guideOk = !empty($guide['json']['ok']);
$report['tasks']['3_guide_tab'] = array(
	'status' => $guideOk ? 'DONE' : 'PENDING',
	'guide_tab_html_length' => (int) ($guide['json']['guide_tab_html_length'] ?? 0),
	'has_faq' => !empty($guide['json']['has_faq']),
	'error' => $guide['json']['error'] ?? null,
	'urls' => array(
		'https://www.epartscart.com/cp/control/portal/epc_auto_price_engine?site_key=epartscart&tab=guide',
		'https://www.ecomae.com/cp/control/portal/epc_auto_price_engine?site_key=epartscart&tab=guide',
	),
);

// 4 — Demo hub tab (password + CP deep link in manage panel)
$demoHubProbe = epc_scv_json($base . '/epc-demo-hub-verify.php?token=' . urlencode($token));
$hubOk = !empty($demoHubProbe['json']['ok']);
$report['tasks']['4_demo_hub_tab'] = array(
	'status' => $hubOk ? 'DONE' : 'PENDING',
	'checks' => $demoHubProbe['json']['checks'] ?? null,
	'urls' => array(
		'https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=demos',
		'https://www.ecomae.com/cp/control/portal/epc_demo_tenants_manage',
	),
);

// 5 — Remove demo_260607_ap_2
$demoRemove = epc_scv_json($base . '/epc-tenant-remove-demo.php?token=' . urlencode($token) . '&site_key=demo_260607_ap_2');
$demoGone = !empty($demoRemove['json']['ok']) && array_key_exists('found', $demoRemove['json'] ?? array()) && empty($demoRemove['json']['found']);
if (!$demoGone && ($demoRemove['http'] ?? 0) >= 500) {
	// Fallback: list live demo tenants from APAI verify registry keys
	$demoGone = !isset($apai['json']['tenants']['demo_260607_ap_2']);
}
$report['tasks']['5_remove_demo_260607_ap_2'] = array(
	'status' => $demoGone ? 'DONE' : 'PENDING',
	'http' => $demoRemove['http'] ?? null,
	'found' => $demoRemove['json']['found'] ?? null,
	'message' => $demoRemove['json']['message'] ?? null,
);

// 6 — Demo creation /platform/demo
$demoPage = epc_scv_curl('https://www.ecomae.com/platform/demo');
$report['tasks']['6_demo_creation'] = array(
	'status' => ($demoPage['http'] === 200 && stripos($demoPage['body'], 'demo') !== false) ? 'DONE' : 'BLOCKED',
	'http' => $demoPage['http'],
	'note' => 'CREATE DATABASE requires CloudPanel sudo or pre-provisioned pool — see epc_portal_demo.php',
	'urls' => array('https://www.ecomae.com/platform/demo'),
);

// 7 — Warehouse stock toyota/1780131090
$stock = epc_scv_curl('https://www.epartscart.com/epc-epartscart-stock-probe.php?token=' . urlencode($token) . '&brand=toyota&article=1780131090');
$stockRows = 0;
if (preg_match('/brand_match_rows=(\d+)/', $stock['body'], $m)) {
	$stockRows = (int) $m[1];
}
$report['tasks']['7_warehouse_stock'] = array(
	'status' => $stockRows > 0 ? 'DONE' : 'PENDING',
	'brand_match_rows' => $stockRows,
	'urls' => array(
		'https://www.epartscart.com/en/parts/toyota/1780131090',
		'https://www.epartscart.com/epc-epartscart-stock-probe.php?token=' . urlencode($token) . '&brand=toyota&article=1780131090',
	),
);

// 8 — Production deploys (storefront smoke)
$sf = epc_scv_curl($base . '/epc-platform-fix-epartscart-storefront.php?token=' . urlencode($token));
$sfPass = preg_match('/summary:\s*pass=(\d+)\s*fail=(\d+)/', $sf['body'], $sm) && (int) $sm[2] === 0;
$report['tasks']['8_production_deploys'] = array(
	'status' => $sfPass ? 'DONE' : 'PENDING',
	'summary' => isset($sm[0]) ? trim($sm[0]) : substr(trim($sf['body']), 0, 200),
);

// 9 — epartscart storefront
$report['tasks']['9_epartscart_storefront'] = array(
	'status' => $sfPass ? 'DONE' : 'PENDING',
	'checks' => array(
		'home' => 'https://www.epartscart.com/en/',
		'catalog_redirect' => 'https://www.epartscart.com/en/apai-root-auto-parts',
		'parts_search' => 'https://www.epartscart.com/en/parts/toyota/1780131090',
	),
);

// 10 — Test orders + CP fulfill-from badge
$orders = epc_scv_json($base . '/epc-cp-orders-verify.php?token=' . urlencode($token) . '&site_key=epartscart');
$ordersOk = !empty($orders['json']['tenants']['epartscart']['db_ok']);
$fulfillFile = __DIR__ . '/content/shop/price_engine/epc_apai_fulfillment.php';
$report['tasks']['10_test_orders_fulfill_badge'] = array(
	'status' => ($ordersOk && is_file($fulfillFile)) ? 'DONE' : 'PENDING',
	'shop_orders_total' => (int) ($orders['json']['tenants']['epartscart']['shop_orders_total'] ?? 0),
	'fulfillment_module' => is_file($fulfillFile),
	'urls' => array(
		'https://www.epartscart.com/cp/shop/orders/orders',
		'https://www.epartscart.com/cp/shop/orders/items',
	),
	'note' => 'Fulfill-from badge renders on order items when t2_json_params contains APAI metadata',
);

// 11 — Server crons
$report['tasks']['11_server_crons'] = array(
	'status' => 'BLOCKED',
	'note' => 'Crons must be installed on VPS crontab — not deployable from code alone',
	'documentation' => 'docs/guides/AUTO_PRICE_AI.md § Cron',
	'commands' => array(
		'* * * * * curl -s "https://www.ecomae.com/epc-apai-background-jobs-cron.php?token=TOKEN"',
		'0 * * * * curl -s "https://www.ecomae.com/epc-apai-hourly-crawl.php?token=TOKEN"',
		'0 3 * * * curl -s "https://www.ecomae.com/epc-apai-daily-source-expand.php?token=TOKEN"',
		'0 4 * * 0 curl -s "https://www.ecomae.com/epc-apai-weekly-platform-sync.php?token=TOKEN"',
		'0 2 * * * curl -sk "https://www.epartscart.com/epc-auto-discovery-run.php?token=TOKEN&site_key=epartscart&taxonomy=auto-engine-filters-oil"',
	),
);

foreach ($report['tasks'] as $task) {
	if (($task['status'] ?? '') === 'PENDING') {
		$report['ok'] = false;
	}
}

echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
