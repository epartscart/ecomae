<?php
/**
 * eParts Cart — full CP audit + apply (routes, packs, probes, opcache).
 * https://www.epartscart.com/epc-epartscart-cp-fix.php?token=epartscart-deploy-2026&key=TECH_KEY
 * Apply DB/menu: &apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	header('Content-Type: application/json; charset=utf-8');
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$cfg = new DP_Config();
if ((string)($_GET['key'] ?? '') !== $cfg->tech_key) {
	header('Content-Type: application/json; charset=utf-8');
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

$hostname = strtolower(trim((string)($_GET['host'] ?? $_SERVER['HTTP_HOST'] ?? 'www.epartscart.com')));
$_SERVER['HTTP_HOST'] = $hostname;
$bare = preg_replace('/^www\./', '', $hostname);
if (strpos($hostname, 'www.') !== 0 && strpos($hostname, '.') !== false) {
	$hostname = 'www.' . $bare;
}

$overrideFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($overrideFile)) {
	$epc_tenant_host_db = null;
	require $overrideFile;
	foreach (array($hostname, $bare) as $hk) {
		if (isset($epc_tenant_host_db[$hk]) && is_array($epc_tenant_host_db[$hk])) {
			foreach (array('db', 'user', 'password', 'host') as $tk) {
				if (!empty($epc_tenant_host_db[$hk][$tk])) {
					$cfg->$tk = $epc_tenant_host_db[$hk][$tk];
				}
			}
		}
	}
}
if (function_exists('epc_portal_runtime_host_db')) {
	$runtimeDb = epc_portal_runtime_host_db($hostname);
	if ($runtimeDb === null && $bare !== $hostname) {
		$runtimeDb = epc_portal_runtime_host_db($bare);
	}
	if (is_array($runtimeDb)) {
		$cfg->db = $runtimeDb['db'];
		$cfg->user = $runtimeDb['user'];
		$cfg->password = $runtimeDb['password'];
		if (!empty($runtimeDb['host'])) {
			$cfg->host = $runtimeDb['host'];
		}
	}
}

$apply = !empty($_GET['apply']);
$backend = trim((string)($cfg->backend_dir ?? 'cp'), '/');
$base = 'https://' . $hostname;

header('Content-Type: application/json; charset=utf-8');

$report = array(
	'ok' => true,
	'hostname' => $hostname,
	'db' => $cfg->db,
	'apply' => $apply,
	'changes' => array(),
	'probes' => array(),
	'setups' => array(),
);

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

epc_portal_db_ensure($pdo);
$_SERVER['HTTP_HOST'] = $hostname;

$requiredPacks = array('core', 'commerce', 'auto_parts', 'logistics', 'erp', 'professional', 'marketing');
$settings = epc_portal_load_site_settings($pdo);
$packsBefore = $settings['enabled_packs'] ?? array();
$report['packs_before'] = $packsBefore;

if ($apply) {
	$merged = array_values(array_unique(array_merge($packsBefore, $requiredPacks)));
	epc_portal_save_site_settings($pdo, array('enabled_packs' => $merged, 'access_mode' => 'professional'));
	$report['changes'][] = 'portal: enabled packs ' . implode(',', $merged);
}

$settingsAfter = epc_portal_load_site_settings($pdo);
$report['packs_after'] = $settingsAfter['enabled_packs'] ?? array();
$report['db_expected'] = 'docpart';
if ($cfg->db !== 'docpart') {
	$report['ok'] = false;
	$report['db_warning'] = 'Tenant DB is not docpart — check registry / config.tenant-host-db.php';
}

function epc_ep_cp_probe(string $url): array
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 25,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_HEADER => true,
		CURLOPT_NOBODY => false,
	));
	$start = microtime(true);
	$raw = (string)curl_exec($ch);
	$ms = (int)round((microtime(true) - $start) * 1000);
	$code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$err = curl_error($ch);
	curl_close($ch);
	$body = $raw;
	if (($pos = strpos($raw, "\r\n\r\n")) !== false) {
		$body = substr($raw, $pos + 4);
	}
	$login = (bool)preg_match('/password|log in|Войти|backend_dir/i', $body);
	$danger = (bool)preg_match('/alert-danger|Fatal error|module not found|Database connection failed/i', $body);
	return array(
		'url' => $url,
		'http' => $code,
		'ms' => $ms,
		'bytes' => strlen($body),
		'login_page' => $login,
		'error_snippet' => $danger,
		'ok' => $code > 0 && $code < 500 && $ms < 10000,
		'curl_error' => $err !== '' ? $err : null,
	);
}

$report['setups'] = array();
if ($apply) {
	$_GET['token'] = epc_deploy_token();
	$_GET['apply'] = '1';
	$_GET['key'] = $cfg->tech_key;
	foreach (array('epc-customer-mgmt-cp-setup.php', 'epc-epartscart-prices-cp-fix.php') as $script) {
		$path = __DIR__ . '/' . $script;
		if (!is_file($path)) {
			$report['setups'][] = array('script' => $script, 'ok' => false, 'error' => 'missing');
			continue;
		}
		ob_start();
		try {
			include $path;
			$out = ob_get_clean();
			$json = json_decode($out, true);
			$okSetup = true;
			if (is_array($json)) {
				$okSetup = !empty($json['ok']) || !empty($json['status']);
			}
			$report['setups'][] = array(
				'script' => $script,
				'ok' => $okSetup,
				'body_preview' => substr($out, 0, 400),
				'json' => is_array($json) ? $json : null,
			);
			if (!$okSetup) {
				$report['ok'] = false;
			}
		} catch (Throwable $e) {
			ob_end_clean();
			$report['setups'][] = array('script' => $script, 'ok' => false, 'error' => $e->getMessage());
			$report['ok'] = false;
		}
	}
	if (function_exists('opcache_reset')) {
		@opcache_reset();
		$report['changes'][] = 'opcache_reset';
	}
}

$contentRoutes = array(
	'shop/customer_mgmt/customer_mgmt' => 'content/shop/customer_mgmt/customer_mgmt_main_page.php',
	'shop/prices' => 'content/shop/prices_upload/prices_manager.php',
	'shop/document_control/document_control' => 'content/shop/document_control/document_control_main_page.php',
);
$report['content_routes'] = array();
foreach ($contentRoutes as $url => $relPhp) {
	$st = $pdo->prepare('SELECT `id`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($url));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$php = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/' . $relPhp;
	if (!is_file($php)) {
		$php = $_SERVER['DOCUMENT_ROOT'] . '/cp/' . preg_replace('#^content/#', 'content/', $relPhp);
	}
	$report['content_routes'][$url] = array(
		'db' => $row ?: null,
		'file_exists' => is_file($php),
		'path_checked' => $php,
	);
	if (!$row || !(int)$row['published_flag'] || !is_file($php)) {
		$report['ok'] = false;
	}
}

$paths = array(
	'/' . $backend . '/',
	'/' . $backend . '/shop/prices',
	'/' . $backend . '/shop/customer_mgmt/customer_mgmt?tab=customers',
	'/' . $backend . '/shop/orders',
	'/' . $backend . '/shop/finance/erp',
	'/' . $backend . '/shop/document_control/document_control',
);
foreach ($paths as $path) {
	$probe = epc_ep_cp_probe($base . $path);
	$report['probes'][] = $probe;
	if (!$probe['ok']) {
		$report['ok'] = false;
	}
}

$report['prices_visible'] = epc_portal_cp_item_visible('/cp/shop/prices');
$report['customer_mgmt_visible'] = epc_portal_cp_item_visible('/cp/shop/customer_mgmt/customer_mgmt');
$report['document_control_visible'] = epc_portal_cp_item_visible('/cp/shop/document_control/document_control');
$report['hint'] = $apply
	? 'Applied. Hard-refresh CP (Ctrl+F5) and re-login if sidebar still stale.'
	: 'Dry run — add apply=1 to register routes, packs, and reset opcache.';

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
