<?php
/**
 * Full ecomae platform setup — portal DB, industry settings, Super CP tenant hub.
 * https://www.ecomae.com/epc-ecomae-setup.php?token=epartscart-deploy-2026
 */
header('Content-Type: text/plain; charset=utf-8');
error_reporting(E_ALL);
ini_set('display_errors', '1');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

if (!empty($_GET['fix_cp_delegate'])) {
	header('Content-Type: application/json; charset=utf-8');
	require_once __DIR__ . '/ecomae-super-cp-setup.php';
	echo json_encode(ecomae_fix_cp_delegate_index(__DIR__), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

if (!empty($_GET['fix_cp_empty'])) {
	require __DIR__ . '/ecomae-fix-cp-empty.php';
	exit;
}

if (!empty($_GET['run_cp_hotfix'])) {
	require __DIR__ . '/ecomae-cp-hotfix-mini.php';
	exit;
}

$clpPassFix = trim((string) ($_GET['clp_pass'] ?? ''));
if (!empty($_GET['fix_epartscart']) && $clpPassFix !== '') {
	require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
	set_time_limit(0);
	$cookie = '';
	if (empty(epc_clp_web_login('admin', $clpPassFix, $cookie)['ok'])) {
		exit("CloudPanel login failed\n");
	}
	echo "CloudPanel login OK\n";
	$hostname = 'www.epartscart.com';
	$platformSite = 'www.ecomae.com';
	$docroot = '/home/ecomae/htdocs/www.ecomae.com';
	$panel = epc_clp_panel_url();
	$vhHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(), $cookie);
	$vhToken = '';
	$vhost = '';
	if (preg_match('/name="token" value="([^"]+)"/', $vhHtml, $vt)) {
		$vhToken = $vt[1];
	}
	if (preg_match('/<textarea[^>]*name="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $vhHtml, $vm)) {
		$vhost = html_entity_decode($vm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
	}
	foreach (array_unique(array($hostname, preg_replace('/^www\./', '', $hostname))) as $aliasHost) {
		if ($aliasHost === '' || $vhost === '' || stripos($vhost, $aliasHost) !== false) {
			continue;
		}
		if (preg_match('/^\s*server_name\s+(.+);/m', $vhost, $sm)) {
			$vhost = preg_replace('/^\s*server_name\s+.+;/m', '  server_name ' . trim($sm[1]) . ' ' . $aliasHost . ';', $vhost, 1);
			echo "Added vhost alias {$aliasHost}\n";
		}
	}
	if ($vhost !== '' && $vhToken !== '' && stripos($vhHtml, $hostname) === false) {
		epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(
			'method' => 'POST',
			'body' => http_build_query(array(
				'vhost-update' => '1',
				'vhost-template' => $vhost,
				'token' => $vhToken,
			)),
		), $cookie);
		echo "Saved ecomae vhost\n";
	}
	$del = epc_clp_web_delete_site($cookie, $hostname);
	echo 'Delete standalone site: ' . implode(' ', $del['log']) . "\n";
	exec('chmod -R o+rX ' . escapeshellarg($docroot) . ' 2>&1', $chmodOut, $chmodCode);
	echo "chmod code={$chmodCode}\n";
	epc_clp_web_install_ssl($cookie, $platformSite);
	foreach (array('https://www.epartscart.com/', 'https://www.epartscart.com/cp/') as $url) {
		@file_get_contents($url, false, stream_context_create(array(
			'http' => array('timeout' => 25, 'ignore_errors' => true),
			'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
		)));
		$code = 0;
		if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
			$code = (int) $m[1];
		}
		echo "Probe {$url} HTTP {$code}\n";
	}
	exit("\nfix_epartscart done.\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

echo "=== ecomae platform setup ===\n";
echo 'Host: ' . epc_portal_host() . "\n";
echo 'Database: ' . $cfg->db . "\n\n";

if (!empty($_GET['unlink_tmp_zip'])) {
	@unlink('/tmp/docpart-epartscart-site.zip');
	@unlink(__DIR__ . '/tmp/ecomae-deploy.zip');
	echo "Removed stale deploy zips\n\n";
}

if (is_file(__DIR__ . '/ecomae-super-cp-setup.php')) {
	require_once __DIR__ . '/ecomae-super-cp-setup.php';
	if (function_exists('ecomae_super_cp_materialize_files')) {
		$written = ecomae_super_cp_materialize_files(__DIR__);
		if ($written !== array()) {
			echo 'Materialized tenant hub files: ' . implode(', ', $written) . "\n\n";
		}
	}
}

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	echo 'DB connection FAILED: ' . $e->getMessage() . "\n";
	exit(1);
}

epc_portal_db_ensure($pdo);
echo "Portal DB tables: OK\n";

$now = time();
$sort = 0;
foreach (epc_portal_industries() as $row) {
	$sort += 10;
	$stmt = $pdo->prepare(
		'INSERT INTO `epc_portal_industry` (`code`, `name`, `theme_json`, `active`, `sort_order`, `updated_at`)
		VALUES (?, ?, ?, 1, ?, ?)
		ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `theme_json` = VALUES(`theme_json`), `updated_at` = VALUES(`updated_at`)'
	);
	$stmt->execute(array(
		$row['code'],
		$row['name'],
		json_encode(isset($row['theme']) ? $row['theme'] : array()),
		$sort,
		$now,
	));
}
echo 'Industries synced: ' . count(epc_portal_industries()) . "\n";

$www = epc_portal_default_site_settings('www.ecomae.com');
$www['host'] = 'www.ecomae.com';
epc_portal_save_site_settings($pdo, $www);
echo "Site settings: www.ecomae.com\n";

$cp = epc_portal_default_site_settings('cp.ecomae.com');
$cp['host'] = 'cp.ecomae.com';
if (!in_array('super_platform', $cp['enabled_packs'], true)) {
	$cp['enabled_packs'][] = 'super_platform';
}
epc_portal_save_site_settings($pdo, $cp);
echo "Site settings: cp.ecomae.com (super_platform pack)\n";

require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
epc_portal_db_ensure($pdo);
$tenantCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_portal_tenants`')->fetchColumn();
if (!empty($_GET['seed_epartscart']) || $tenantCount === 0) {
	$docCfg = epc_portal_docpart_config();
	$seed = epc_portal_save_tenant($pdo, array(
		'site_key' => 'epartscart',
		'hostname' => 'www.epartscart.com',
		'industry_code' => 'auto_parts',
		'status' => 'live',
		'trade_name' => 'eParts Cart',
		'hub_name' => 'Electronic World Group',
		'from_email' => 'partsdoc2025@gmail.com',
		'db_name' => $docCfg->db,
		'db_user' => $docCfg->user,
		'db_password' => $docCfg->password,
		'notes' => 'Seeded from epc-ecomae-setup.php',
	));
	echo 'Seed epartscart tenant: ' . ($seed['message'] ?? '') . "\n";
	$tenantCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_portal_tenants`')->fetchColumn();
} elseif (!empty($_GET['fix_epartscart_tenant'])) {
	$docCfg = epc_portal_docpart_config();
	$seed = epc_portal_save_tenant($pdo, array(
		'site_key' => 'epartscart',
		'hostname' => 'www.epartscart.com',
		'industry_code' => 'auto_parts',
		'status' => 'live',
		'trade_name' => 'eParts Cart',
		'hub_name' => 'Electronic World Group',
		'from_email' => 'partsdoc2025@gmail.com',
		'db_name' => $docCfg->db,
		'db_user' => $docCfg->user,
		'db_password' => $docCfg->password,
		'notes' => 'Repaired via epc-ecomae-setup.php fix_epartscart_tenant',
	));
	echo 'Fix epartscart tenant: ' . ($seed['message'] ?? '') . "\n";
}
echo "Tenant registry: {$tenantCount} registered (DNS-only — no separate hosting per client)\n";
echo "Platform IP for GoDaddy A records: " . epc_portal_platform_ip() . "\n";

$targets = epc_portal_deploy_targets($pdo);
echo 'Deploy targets (platform only): ' . count($targets) . "\n";

$token = epc_deploy_token();
$subSetups = array(
	'/epc-portal-cp-setup.php',
	'/ecomae-super-cp-setup.php',
);
foreach ($subSetups as $path) {
	$url = 'https://' . epc_portal_host() . $path . '?token=' . urlencode($token);
	$resp = @file_get_contents($url, false, stream_context_create(array(
		'http' => array('timeout' => 120),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	)));
	echo "\n--- {$path} ---\n";
	echo substr((string) $resp, 0, 1200) . "\n";
}

echo "\nDone.\n";
echo "Marketing: https://www.ecomae.com/\n";
echo "Super CP:  https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub\n";
