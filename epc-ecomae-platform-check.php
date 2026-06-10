<?php
/**
 * Independent ecomae platform audit — run on www.ecomae.com or cp.ecomae.com.
 * https://www.ecomae.com/epc-ecomae-platform-check.php?token=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$host = epc_portal_host();
$report = array(
	'host' => $host,
	'timestamp' => date('c'),
	'platform_ip' => epc_portal_platform_ip(),
	'checks' => array(),
	'ok' => true,
);

function ecomae_check(array &$report, string $id, bool $pass, string $detail = ''): void
{
	$report['checks'][$id] = array('ok' => $pass, 'detail' => $detail);
	if (!$pass) {
		$report['ok'] = false;
	}
}

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

ecomae_check($report, 'is_platform_host', epc_portal_is_platform_hostname($host), $host);
ecomae_check($report, 'config_local', is_file(__DIR__ . '/config.local.php'), __DIR__ . '/config.local.php');
ecomae_check($report, 'dp_core_fix', is_file(__DIR__ . '/core/dp_core.php') && strpos((string) file_get_contents(__DIR__ . '/core/dp_core.php'), 'epc_portal_apply_config') !== false, 'dp_core.php');
ecomae_check($report, 'portal_tenant_lib', is_file(__DIR__ . '/content/general_pages/epc_portal_tenant.php'), 'epc_portal_tenant.php');
ecomae_check($report, 'platform_home', is_file(__DIR__ . '/content/general_pages/epc_ecomae_platform_home.php'), 'marketing home');
ecomae_check($report, 'tenant_hub_helpers', is_file(__DIR__ . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php'), 'helpers');
ecomae_check($report, 'super_cp_setup', is_file(__DIR__ . '/ecomae-super-cp-setup.php'), 'setup script');

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	ecomae_check($report, 'platform_db_connect', true, $cfg->db);
	epc_portal_db_ensure($pdo);
	$tables = array('epc_portal_industry', 'epc_portal_site_settings', 'epc_portal_deploy_targets', 'epc_portal_tenants');
	foreach ($tables as $tbl) {
		$n = (int) $pdo->query("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = " . $pdo->quote($cfg->db) . " AND table_name = " . $pdo->quote($tbl))->fetchColumn();
		ecomae_check($report, 'table_' . $tbl, $n === 1, $tbl);
	}
	$ind = (int) $pdo->query('SELECT COUNT(*) FROM `epc_portal_industry`')->fetchColumn();
	ecomae_check($report, 'industries_count', $ind >= 8, (string) $ind);
	$tenants = (int) $pdo->query('SELECT COUNT(*) FROM `epc_portal_tenants`')->fetchColumn();
	ecomae_check($report, 'tenants_registry', true, (string) $tenants . ' registered');
	$st = $pdo->prepare('SELECT 1 FROM `epc_portal_site_settings` WHERE `host` = ? LIMIT 1');
	foreach (array('www.ecomae.com', 'cp.ecomae.com') as $h) {
		$st->execute(array($h));
		ecomae_check($report, 'site_settings_' . str_replace('.', '_', $h), (bool) $st->fetchColumn(), $h);
	}
	$targets = $pdo->query('SELECT hostname FROM `epc_portal_deploy_targets` WHERE active = 1')->fetchAll(PDO::FETCH_COLUMN);
	$onlyEcomae = count($targets) <= 1 && (count($targets) === 0 || in_array('www.ecomae.com', $targets, true));
	ecomae_check($report, 'deploy_targets_platform_only', $onlyEcomae, implode(', ', $targets ?: array('none')));
	$lang = (int) $pdo->query('SELECT COUNT(*) FROM `lang_languages`')->fetchColumn();
	ecomae_check($report, 'lang_languages', $lang > 0, (string) $lang);
} catch (Exception $e) {
	ecomae_check($report, 'platform_db_connect', false, $e->getMessage());
}

$urls = array(
	'www_home' => 'https://www.ecomae.com/',
	'cp_root' => 'https://cp.ecomae.com/',
	'cp_tenant_hub' => 'https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub',
	'cp_login' => 'https://cp.ecomae.com/cp/',
);
foreach ($urls as $key => $url) {
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$snippet = $body !== false ? substr(preg_replace('/\s+/', ' ', strip_tags((string) $body)), 0, 80) : '';
	$pass = $code >= 200 && $code < 500 && stripos($snippet, 'License error') === false && stripos($snippet, 'No DB connect') === false;
	if ($key === 'cp_root' && $code === 302) {
		$pass = true;
		$snippet = 'redirect OK';
	}
	ecomae_check($report, 'http_' . $key, $pass, 'HTTP ' . $code . ' ' . $snippet);
}

echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
