<?php
/**
 * Full CP + Super CP fatal scan — all Model C tenants + ecomae Super CP.
 * GET https://www.ecomae.com/epc-cp-fatal-scan.php?token=epartscart-deploy-2026
 * Optional: site_key=epartscart (single tenant), http=1 (HTTP probes), files=1 (filesystem)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(300);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
$doHttp = !isset($_GET['http']) || (string) $_GET['http'] !== '0';
$doFiles = !isset($_GET['files']) || (string) $_GET['files'] !== '0';

$tenants = array(
	'ecomae' => array('host' => 'www.ecomae.com', 'super_cp' => true, 'docroot' => '/home/ecomae/htdocs/www.ecomae.com/'),
	'epartscart' => array('host' => 'www.epartscart.com', 'super_cp' => false, 'docroot' => '/home/epartscart/htdocs/www.epartscart.com/'),
	'electronicae' => array('host' => 'www.electronicae.com', 'super_cp' => false, 'docroot' => '/home/electronicae/htdocs/www.electronicae.com/'),
	'taxofinca' => array('host' => 'www.taxofinca.com', 'super_cp' => false, 'docroot' => '/home/taxofinca/htdocs/www.taxofinca.com/'),
	'stylenlook' => array('host' => 'www.stylenlook.com', 'super_cp' => false, 'docroot' => '/home/stylenlook/htdocs/www.stylenlook.com/'),
	'thejewellerytrend' => array('host' => 'www.thejewellerytrend.com', 'super_cp' => false, 'docroot' => '/home/thejewellerytrend/htdocs/www.thejewellerytrend.com/'),
);

$cpRoutes = array(
	'prices' => '/cp/shop/prices',
	'orders' => '/cp/shop/orders/orders',
	'orders_items' => '/cp/shop/orders/items',
	'logistics' => '/cp/shop/logistics',
	'logistics_storage' => '/cp/shop/logistics/storage',
	'finance_erp' => '/cp/shop/finance/erp?epc_erp_shell=1',
	'document_control' => '/cp/shop/document_control/document_control',
	'apai' => '/cp/control/portal/epc_auto_price_engine',
	'agent' => '/cp/shop/parts_agent_chats',
	'portal_settings' => '/cp/control/portal/industry_settings',
);

$superCpRoutes = array(
	'tenant_hub' => '/cp/shop/tenant_hub/tenant_hub?tab=onboard',
	'demo_hub' => '/cp/shop/tenant_hub/tenant_hub?tab=demos',
	'visual_editor' => '/cp/control/portal/epc_visual_page_editor',
	'cp_guideline' => '/cp/control/cp-guideline',
);

$fatalPatterns = array(
	'php_fatal' => '/Fatal error|Parse error|Uncaught (?:Error|Exception|Throwable)/i',
	'eval_dir' => '/__DIR__\s*\.\s*[\'"]/i',
	'empty_include' => '/include(?:_once)?\s*\(\s*[\'"]\s*[\'"]\s*\)/i',
);

$tenantDbMap = array();
$overrideFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($overrideFile)) {
	require $overrideFile;
	if (!empty($epc_tenant_host_db) && is_array($epc_tenant_host_db)) {
		$tenantDbMap = $epc_tenant_host_db;
	}
}

function epc_cfs_cfg(string $host, array $tenantDbMap): DP_Config
{
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link'], $GLOBALS['db_link']);
	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$hk = strtolower(trim($host));
	if (isset($tenantDbMap[$hk]) && is_array($tenantDbMap[$hk])) {
		foreach (array('db', 'user', 'password') as $tk) {
			if (!empty($tenantDbMap[$hk][$tk])) {
				$cfg->$tk = $tenantDbMap[$hk][$tk];
			}
		}
	}
	return $cfg;
}

function epc_cfs_curl(string $url, string $host, string $cookie = ''): array
{
	$ch = curl_init($url);
	$headers = array('Host: ' . $host);
	if ($cookie !== '') {
		$headers[] = 'Cookie: ' . $cookie;
	}
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 25,
		CURLOPT_CONNECTTIMEOUT => 10,
		CURLOPT_HTTPHEADER => $headers,
	));
	$body = (string) curl_exec($ch);
	$http = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	return array('http' => $http, 'body' => $body, 'bytes' => strlen($body));
}

function epc_cfs_login_cookie(string $host): string
{
	$cfg = epc_cfs_cfg($host, $GLOBALS['tenantDbMap'] ?? array());
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$st = $pdo->query(
			'SELECT `session`, `2fa_session`, `user_id` FROM `sessions` WHERE `type` = 1 ORDER BY `last_activiti_time` DESC LIMIT 1'
		);
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			return '';
		}
		$cookie = 'admin_session=' . urlencode((string) $row['session']) . '; admin_u_id=' . (int) $row['user_id'];
		if (!empty($row['2fa_session'])) {
			$cookie .= '; 2fa=' . urlencode((string) $row['2fa_session']);
		}
		return $cookie;
	} catch (Throwable $e) {
		return '';
	}
}

function epc_cfs_analyze_html(string $html, array $fatalPatterns): array
{
	$issues = array();
	foreach ($fatalPatterns as $key => $pattern) {
		if (preg_match($pattern, $html)) {
			$issues[] = $key;
		}
	}
	$loginPage = stripos($html, 'login_form') !== false || stripos($html, 'Log in form') !== false;
	$truncated = strlen($html) > 0 && strlen($html) < 800 && !$loginPage;
	return array(
		'issues' => $issues,
		'login_page' => $loginPage,
		'truncated' => $truncated,
	);
}

function epc_cfs_check_php_file(string $path, string $fileKey = ''): array
{
	$issues = array();
	if (!is_file($path)) {
		return array('ok' => false, 'issues' => array('missing_file'), 'bytes' => 0);
	}
	$bytes = (int) filesize($path);
	if ($bytes < 32) {
		$issues[] = 'empty_file';
	}
	$src = (string) file_get_contents($path);
	$isAjaxEndpoint = (strpos($fileKey, 'ajax') !== false || strpos(basename($path), 'ajax_') === 0);
	if (!$isAjaxEndpoint && preg_match('/(?:require|include)(?:_once)?\s*\(?\s*__DIR__\s*\./i', $src)) {
		$issues[] = 'eval_unsafe_dir';
	}
	return array('ok' => $issues === array(), 'issues' => $issues, 'bytes' => $bytes);
}

$GLOBALS['tenantDbMap'] = $tenantDbMap;

$report = array(
	'ok' => true,
	'generated_at' => gmdate('c'),
	'http_probes' => $doHttp,
	'file_probes' => $doFiles,
	'tenants' => array(),
	'summary' => array(),
);

$routeKeys = array_keys($cpRoutes);
foreach ($tenants as $siteKey => $meta) {
	if ($onlySite !== '' && $siteKey !== $onlySite) {
		continue;
	}
	$host = (string) $meta['host'];
	$cfg = epc_cfs_cfg($host, $tenantDbMap);
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/') ?: 'cp';
	$docRoot = rtrim((string) ($meta['docroot'] ?? ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__)), '/\\');
	$cookie = $doHttp ? epc_cfs_login_cookie($host) : '';

	$tenantReport = array(
		'site_key' => $siteKey,
		'host' => $host,
		'super_cp' => !empty($meta['super_cp']),
		'db' => (string) ($cfg->db ?? ''),
		'cookie_ok' => $cookie !== '',
		'routes' => array(),
		'files' => array(),
		'overall' => 'PASS',
	);

	$routesToProbe = $cpRoutes;
	if (!empty($meta['super_cp'])) {
		$routesToProbe = array_merge($routesToProbe, $superCpRoutes);
	}

	if ($doHttp) {
		foreach ($routesToProbe as $routeKey => $path) {
			$path = str_replace('/cp/', '/' . $backend . '/', $path);
			$url = 'https://' . $host . $path;
			$fetch = epc_cfs_curl($url, $host, $cookie);
			$analysis = epc_cfs_analyze_html($fetch['body'], $fatalPatterns);
			$pass = $fetch['http'] < 500
				&& $fetch['http'] > 0
				&& $analysis['issues'] === array()
				&& !$analysis['truncated']
				&& !$analysis['login_page'];
			if (!$pass) {
				$tenantReport['overall'] = 'FAIL';
				$report['ok'] = false;
			}
			$tenantReport['routes'][$routeKey] = array(
				'url' => $url,
				'http' => $fetch['http'],
				'bytes' => $fetch['bytes'],
				'pass' => $pass,
				'login_page' => $analysis['login_page'],
				'truncated' => $analysis['truncated'],
				'issues' => $analysis['issues'],
			);
		}
	}

	$localDocRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__), '/\\');
	$canCheckLocalFiles = ($docRoot === $localDocRoot || is_dir($docRoot . '/cp'));

	if ($doFiles && $canCheckLocalFiles) {
		$criticalFiles = array(
			'prices_manager' => $backend . '/content/shop/prices_upload/prices_manager.php',
			'storefront_panel' => $backend . '/content/shop/prices_upload/epc_storefront_storage_panel.php',
			'storefront_toggle_ajax' => $backend . '/content/shop/prices_upload/ajax_epc_storefront_storage_toggle.php',
			'storefront_flags' => 'content/shop/docpart/epc_storefront_storage_flags.php',
			'orders' => $backend . '/content/shop/order_process/orders.php',
			'orders_helpers' => $backend . '/content/shop/order_process/epc_orders_workspace_helpers.php',
			'document_control' => $backend . '/content/shop/document_control/document_control_main.php',
			'erp_main_page' => $backend . '/content/shop/finance/erp/erp_main_page.php',
			'apai_engine' => $backend . '/content/control/portal/epc_auto_price_engine.php',
			'agent_chats' => $backend . '/content/shop/parts_agent/parts_agent_chats.php',
			'tenant_hub' => $backend . '/content/shop/tenant_hub/tenant_hub_main.php',
			'logistics_storage' => $backend . '/content/shop/logistics/storage.php',
			'cp_page_assets' => 'content/general_pages/epc_cp_page_assets.php',
		);
		foreach ($criticalFiles as $fileKey => $rel) {
			$path = $docRoot . '/' . ltrim(str_replace('\\', '/', $rel), '/');
			$check = epc_cfs_check_php_file($path, $fileKey);
			if (!$check['ok']) {
				$tenantReport['overall'] = 'FAIL';
				$report['ok'] = false;
			}
			$tenantReport['files'][$fileKey] = array(
				'path' => $path,
				'pass' => $check['ok'],
				'bytes' => $check['bytes'],
				'issues' => $check['issues'],
			);
		}

	} elseif ($doFiles) {
		$tenantReport['files'] = array('skipped' => 'docroot_not_local');
	}

	if ($doFiles && $canCheckLocalFiles) {
		try {
			$pdo = new PDO(
				'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
				$cfg->user,
				$cfg->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			$criticalUrls = array(
				'shop/prices', 'shop/orders/orders', 'shop/logistics', 'shop/logistics/storage',
				'shop/finance/erp', 'shop/document_control/document_control',
				'control/portal/epc_auto_price_engine', 'shop/parts_agent_chats',
				'shop/tenant_hub/tenant_hub', 'control/portal/industry_settings',
			);
			$ph = implode(',', array_fill(0, count($criticalUrls), '?'));
			$st = $pdo->prepare(
				"SELECT `url`, `content`, `published_flag` FROM `content`
				 WHERE `is_frontend` = 0 AND `url` IN ($ph) ORDER BY `url`"
			);
			$st->execute($criticalUrls);
			$contentIssues = array();
			while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
				$contentPath = (string) ($row['content'] ?? '');
				if ($contentPath === '' || stripos($contentPath, '.php') === false) {
					continue;
				}
				$resolved = str_replace(array('<backend_dir>', '<backend>'), $backend, $contentPath);
				if ($resolved[0] !== '/') {
					$resolved = '/' . $resolved;
				}
				$full = $docRoot . $resolved;
				if (!is_file($full) || (int) filesize($full) < 32) {
					$contentIssues[] = array(
						'url' => (string) ($row['url'] ?? ''),
						'content' => $contentPath,
						'resolved' => $full,
						'issue' => is_file($full) ? 'empty_file' : 'missing_file',
					);
				}
			}
			if ($contentIssues !== array()) {
				$tenantReport['content_route_issues'] = $contentIssues;
				$tenantReport['overall'] = 'FAIL';
				$report['ok'] = false;
			}
		} catch (Throwable $e) {
			$tenantReport['db_error'] = $e->getMessage();
		}
	}

	$report['tenants'][$siteKey] = $tenantReport;
	$report['summary'][$siteKey] = $tenantReport['overall'];
}

echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
