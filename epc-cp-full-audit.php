<?php
/**
 * Comprehensive CP / Super CP / ERP / storefront audit — single JSON report.
 * GET https://www.ecomae.com/epc-cp-full-audit.php?token=epartscart-deploy-2026
 * Optional: site_key=epartscart, apply=1 (run inline setup fixes)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(300);

$token = epc_deploy_token();
$apply = !empty($_GET['apply']);
$filterSite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
$tenants = array('epartscart', 'electronicae', 'taxofinca', 'stylenlook', 'thejewellerytrend');
$platformBase = 'https://www.ecomae.com';

function epc_cfa_curl(string $url, int $timeout = 60): array
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
	$ms = (int) round((float) curl_getinfo($ch, CURLINFO_TOTAL_TIME) * 1000);
	curl_close($ch);
	return array('http' => $http, 'body' => $body, 'ms' => $ms);
}

function epc_cfa_json(string $url): array
{
	$r = epc_cfa_curl($url);
	$j = json_decode($r['body'], true);
	return array('http' => $r['http'], 'ms' => $r['ms'], 'json' => is_array($j) ? $j : null, 'raw' => $r['body']);
}

function epc_cfa_issue(array &$issues, string $area, string $issue, string $status, string $fixFile = ''): void
{
	$issues[] = array(
		'area' => $area,
		'issue' => $issue,
		'status' => $status,
		'fix_file' => $fixFile,
	);
}

$report = array(
	'ok' => true,
	'generated_at' => gmdate('c'),
	'apply' => $apply,
	'issues' => array(),
	'tenants' => array(),
	'performance' => array(),
	'deploy_manifest' => array(),
	'manual_urls' => array(),
);

// --- Sub-probes (delegate to existing verify scripts) ---
$probes = array(
	'portal_menu' => $platformBase . '/epc-cp-portal-menu-verify.php?token=' . urlencode($token),
	'orders' => $platformBase . '/epc-cp-orders-verify.php?token=' . urlencode($token),
	'apai' => $platformBase . '/epc-apai-cp-tenant-verify.php?token=' . urlencode($token),
	'demo_hub' => $platformBase . '/epc-demo-hub-verify.php?token=' . urlencode($token),
	'storefront' => $platformBase . '/epc-platform-fix-epartscart-storefront.php?token=' . urlencode($token),
	'stock' => 'https://www.epartscart.com/epc-epartscart-stock-probe.php?token=' . urlencode($token) . '&brand=toyota&article=1780131090',
	'supercp_links' => $platformBase . '/epc-supercp-link-audit.php?token=' . urlencode($token) . '&format=json',
);

$probeResults = array();
foreach ($probes as $key => $url) {
	$probeResults[$key] = epc_cfa_json($url);
}

// Portal menu — POS + Visual editor
$pm = $probeResults['portal_menu']['json'] ?? array();
foreach ($tenants as $sk) {
	if ($filterSite !== '' && $sk !== $filterSite) {
		continue;
	}
	$row = $pm['tenants'][$sk] ?? null;
	$pass = is_array($row) && !empty($row['ok']);
	$report['tenants'][$sk] = array(
		'portal_menu' => $pass ? 'PASS' : 'FAIL',
		'orders' => 'PENDING',
		'prices_route' => 'PENDING',
		'document_control' => 'PENDING',
		'erp_shell' => 'PENDING',
		'apai' => 'PENDING',
		'db' => 'PENDING',
		'overall' => $pass ? 'PASS' : 'FAIL',
	);
	if (!$pass) {
		$report['ok'] = false;
		epc_cfa_issue($report['issues'], 'Tenant CP / ' . $sk, 'Portal menu missing POS or Visual editor', 'FAIL', 'epc-cp-portal-menu-setup-all.php');
	}
}

// Orders
$ord = $probeResults['orders']['json'] ?? array();
foreach ($tenants as $sk) {
	if ($filterSite !== '' && $sk !== $filterSite) {
		continue;
	}
	$t = $ord['tenants'][$sk] ?? null;
	$dbOk = is_array($t) && !empty($t['db_ok']);
	$visible = is_array($t) && (int) ($t['orders_visible_admin'] ?? 0) > 0;
	$pass = $dbOk && $visible;
	if (!isset($report['tenants'][$sk])) {
		$report['tenants'][$sk] = array();
	}
	$report['tenants'][$sk]['orders'] = $pass ? 'PASS' : 'FAIL';
	$report['tenants'][$sk]['db'] = $dbOk ? 'PASS' : 'FAIL';
	if (!$pass) {
		$report['ok'] = false;
		$report['tenants'][$sk]['overall'] = 'FAIL';
		epc_cfa_issue($report['issues'], 'Tenant CP / ' . $sk, 'Orders not visible in CP', 'FAIL', 'content/general_pages/epc_portal.php');
	}
}

// APAI visibility
$apai = $probeResults['apai']['json'] ?? array();
foreach ($tenants as $sk) {
	if ($filterSite !== '' && $sk !== $filterSite) {
		continue;
	}
	$pass = !empty($apai['tenants'][$sk]['pass']);
	$report['tenants'][$sk]['apai'] = $pass ? 'PASS' : 'FAIL';
	if (!$pass) {
		$report['ok'] = false;
		$report['tenants'][$sk]['overall'] = 'FAIL';
		epc_cfa_issue($report['issues'], 'Tenant CP / ' . $sk, 'Auto Price AI not visible in Portal', 'FAIL', 'epc-auto-price-setup-all.php');
	}
}

// Demo hub (source markers — no login required)
$demoHub = $probeResults['demo_hub']['json'] ?? array();
if (empty($demoHub['ok'])) {
	$demoHub = epc_cfa_json($platformBase . '/epc-demo-hub-verify.php?token=' . urlencode($token))['json'] ?? array();
}
if (empty($demoHub['ok'])) {
	$report['ok'] = false;
	epc_cfa_issue($report['issues'], 'Super CP', 'Demo tenants tab missing password/CP login columns', 'FAIL', 'cp/content/control/portal/epc_demo_tenants_manage.php');
} else {
	epc_cfa_issue($report['issues'], 'Super CP', 'Demo hub tab markers', 'PASS', 'epc-demo-hub-verify.php');
}

// Storefront + warehouse stock (re-probe if batch timed out)
$sfBody = $probeResults['storefront']['body'] ?? '';
if (!preg_match('/summary:\s*pass=/', $sfBody)) {
	$sfBody = epc_cfa_curl($probes['storefront'], 90)['body'];
}
$sfPass = preg_match('/summary:\s*pass=(\d+)\s*fail=(\d+)/', $sfBody, $sm) && (int) $sm[2] === 0;
$stockBody = $probeResults['stock']['body'] ?? '';
if (!preg_match('/brand_match_rows=\d+/', $stockBody)) {
	$stockBody = epc_cfa_curl($probes['stock'], 60)['body'];
}
$stockRows = 0;
if (preg_match('/brand_match_rows=(\d+)/', $stockBody, $stk)) {
	$stockRows = (int) $stk[1];
}
if (!$sfPass) {
	$report['ok'] = false;
	epc_cfa_issue($report['issues'], 'Storefront / epartscart', 'Storefront smoke failed', 'FAIL', 'epc-platform-fix-epartscart-storefront.php');
} else {
	epc_cfa_issue($report['issues'], 'Storefront / epartscart', 'Storefront smoke', 'PASS', '');
}
if ($stockRows <= 0) {
	$report['ok'] = false;
	epc_cfa_issue($report['issues'], 'Storefront / epartscart', 'Warehouse stock probe empty', 'FAIL', 'content/shop/docpart/part_search_page.php');
} else {
	epc_cfa_issue($report['issues'], 'Storefront / epartscart', 'Warehouse stock probe rows=' . $stockRows, 'PASS', '');
}

// GUT21 parts page (HTML markers)
$gut21 = epc_cfa_curl('https://www.epartscart.com/en/parts/GMB/GUT21', 45);
$gut21Ok = $gut21['http'] === 200
	&& stripos($gut21['body'], 'onGetStoragesData') !== false
	&& !preg_match('/Fatal error|Parse error/i', $gut21['body']);
if (!$gut21Ok) {
	$report['ok'] = false;
	epc_cfa_issue($report['issues'], 'Storefront / epartscart', 'GUT21 parts page missing warehouse JS', 'FAIL', 'content/shop/docpart/part_search_page.php');
} else {
	epc_cfa_issue($report['issues'], 'Storefront / epartscart', 'GUT21 warehouse spinner JS present', 'PASS', '');
}

// ERP shell source markers (local docroot on platform host)
$erpShellPhp = __DIR__ . '/content/shop/finance/epc_erp_cp_shell.php';
$erpNavPhp = __DIR__ . '/cp/content/shop/finance/erp/erp_nav_areas.php';
$erpShellSrc = is_file($erpShellPhp) ? (string) file_get_contents($erpShellPhp) : '';
$erpNavSrc = is_file($erpNavPhp) ? (string) file_get_contents($erpNavPhp) : '';
$erpShellOk = stripos($erpShellSrc, 'epc_erp_shell_url_query') !== false
	&& stripos($erpNavSrc, 'epc_erp_shell_url_query') !== false;
if (!$erpShellOk) {
	$report['ok'] = false;
	epc_cfa_issue($report['issues'], 'ERP shell', 'Shell navigation helpers missing', 'FAIL', 'content/shop/finance/epc_erp_cp_shell.php');
} else {
	epc_cfa_issue($report['issues'], 'ERP shell', 'Shell URL query append in nav', 'PASS', '');
}

// Agent page raw CSS fix (external asset map)
$assetsPhp = __DIR__ . '/content/general_pages/epc_cp_page_assets.php';
$assetsSrc = is_file($assetsPhp) ? (string) file_get_contents($assetsPhp) : '';
$agentAssetsOk = stripos($assetsSrc, 'shop/parts_agent_chats') !== false
	&& stripos($assetsSrc, 'epc_agent_cp_css.php') !== false;
if (!$agentAssetsOk) {
	$report['ok'] = false;
	epc_cfa_issue($report['issues'], 'Super CP', 'Agent page CSS not in head assets map', 'FAIL', 'content/general_pages/epc_cp_page_assets.php');
} else {
	epc_cfa_issue($report['issues'], 'Super CP', 'Agent page external CSS map', 'PASS', '');
}

// Prices route DB check on platform PDO
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$tenantDbMap = array();
$overrideFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($overrideFile)) {
	require $overrideFile;
	if (!empty($epc_tenant_host_db) && is_array($epc_tenant_host_db)) {
		$tenantDbMap = $epc_tenant_host_db;
	}
}

function epc_cfa_tenant_cfg(string $host, array $tenantDbMap): DP_Config
{
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);
	$tcfg = new DP_Config();
	epc_portal_apply_config($tcfg);
	$hk = strtolower(trim($host));
	if (isset($tenantDbMap[$hk]) && is_array($tenantDbMap[$hk])) {
		foreach (array('db', 'user', 'password') as $tk) {
			if (!empty($tenantDbMap[$hk][$tk])) {
				$tcfg->$tk = $tenantDbMap[$hk][$tk];
			}
		}
	}
	return $tcfg;
}

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$backend = trim((string) $cfg->backend_dir, '/') ?: 'cp';
$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__), '/');
$docProbeFile = __DIR__ . '/content/shop/document_control/epc_document_control_cp_install.php';
if (is_file($docProbeFile)) {
	require_once $docProbeFile;
}

foreach ($tenants as $sk) {
	if ($filterSite !== '' && $sk !== $filterSite) {
		continue;
	}
	$host = 'www.' . $sk . '.com';
	$tcfg = epc_cfa_tenant_cfg($host, $tenantDbMap);
	try {
		$pdo = new PDO(
			'mysql:host=' . $tcfg->host . ';dbname=' . $tcfg->db . ';charset=utf8',
			$tcfg->user,
			$tcfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$st = $pdo->prepare('SELECT `url`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
		$st->execute(array('shop/prices'));
		$pricesRow = $st->fetch(PDO::FETCH_ASSOC);
		$pricesOk = is_array($pricesRow)
			&& (string) ($pricesRow['url'] ?? '') === 'shop/prices'
			&& stripos((string) ($pricesRow['content'] ?? ''), 'prices_manager.php') !== false
			&& stripos((string) ($pricesRow['url'] ?? ''), '.php') === false;
		$report['tenants'][$sk]['prices_route'] = $pricesOk ? 'PASS' : 'FAIL';
		if (!$pricesOk) {
			$report['ok'] = false;
			$report['tenants'][$sk]['overall'] = 'FAIL';
			epc_cfa_issue($report['issues'], 'Tenant CP / ' . $sk, 'Prices content route misconfigured (PHP in URL risk)', 'FAIL', 'epc-epartscart-prices-cp-fix.php');
		}

		$docOk = false;
		if (function_exists('epc_document_control_cp_probe')) {
			$docProbe = epc_document_control_cp_probe($pdo, $docRoot, $backend);
			$docMain = $docProbe['content']['shop/document_control/document_control'] ?? null;
			$docOk = is_array($docMain) && (int) ($docMain['published_flag'] ?? 0) === 1;
		} else {
			$st->execute(array('shop/document_control/document_control'));
			$docRow = $st->fetch(PDO::FETCH_ASSOC);
			$docOk = is_array($docRow) && (int) ($docRow['published_flag'] ?? 0) === 1;
		}
		$report['tenants'][$sk]['document_control'] = $docOk ? 'PASS' : 'FAIL';
		if (!$docOk) {
			$report['ok'] = false;
			$report['tenants'][$sk]['overall'] = 'FAIL';
			epc_cfa_issue($report['issues'], 'Tenant CP / ' . $sk, 'Document control route missing/unpublished', 'FAIL', 'epc-document-control-cp-setup-all.php');
		}

		$report['tenants'][$sk]['erp_shell'] = is_file(__DIR__ . '/content/shop/finance/epc_erp_cp_shell.php') ? 'PASS' : 'FAIL';
		if (!isset($report['tenants'][$sk]['overall']) || $report['tenants'][$sk]['overall'] !== 'FAIL') {
			$report['tenants'][$sk]['overall'] = 'PASS';
		}
	} catch (Throwable $e) {
		$report['ok'] = false;
		$report['tenants'][$sk]['db'] = 'FAIL';
		$report['tenants'][$sk]['overall'] = 'FAIL';
		epc_cfa_issue($report['issues'], 'Tenant CP / ' . $sk, 'DB connect: ' . $e->getMessage(), 'FAIL', 'config.tenant-host-db.php');
	}
}

// Performance notes
$report['performance'] = array(
	'probe_ms' => array(
		'portal_menu' => $probeResults['portal_menu']['ms'] ?? null,
		'orders' => $probeResults['orders']['ms'] ?? null,
		'apai' => $probeResults['apai']['ms'] ?? null,
		'storefront' => $probeResults['storefront']['ms'] ?? null,
	),
	'notes' => array(
		'PDO singleton via epc_portal_tenant.php reduces duplicate connections',
		'APAI lazy tab config inlined in epc_cp_page_assets.php',
		'Prices manager uses epc_prices_manager_perf.php throttling',
	),
);

// Apply inline setups when requested
if ($apply) {
	$setups = array(
		'epc-cp-portal-menu-setup-all.php?apply=1',
		'epc-document-control-cp-setup-all.php?apply=1',
		'epc-auto-price-setup-all.php?apply=1',
		'ecomae-super-cp-setup.php',
	);
	foreach ($setups as $script) {
		$url = $platformBase . '/' . $script . (strpos($script, '?') !== false ? '&' : '?') . 'token=' . urlencode($token);
		$r = epc_cfa_curl($url, 120);
		$report['deploy_manifest'][] = array(
			'script' => $script,
			'http' => $r['http'],
			'ok' => $r['http'] >= 200 && $r['http'] < 400,
			'tail' => substr(trim($r['body']), 0, 200),
		);
	}
}

$report['manual_urls'] = array(
	'Super CP' => $platformBase . '/cp/',
	'Tenant hub demos' => $platformBase . '/cp/shop/tenant_hub/tenant_hub?tab=demos',
	'ERP shell' => 'https://www.epartscart.com/cp/shop/finance/erp?epc_erp_shell=1',
	'APAI Guide' => 'https://www.epartscart.com/cp/control/portal/epc_auto_price_engine?tab=guide&site_key=epartscart',
	'Orders' => 'https://www.epartscart.com/cp/shop/orders/orders',
	'Prices' => 'https://www.epartscart.com/cp/shop/prices',
	'Document control' => 'https://www.epartscart.com/cp/shop/document_control/document_control',
	'Agent chats' => $platformBase . '/cp/shop/parts_agent_chats',
	'GUT21 parts' => 'https://www.epartscart.com/en/parts/GMB/GUT21',
	'Demo creation' => $platformBase . '/platform/demo',
);

echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
