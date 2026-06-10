<?php
/**
 * Fix epartscart client: vhost alias on ecomae, remove broken standalone site, tenant DB docpart.
 * https://www.ecomae.com/ecomae-fix-epartscart.php?token=...&clp_pass=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}

$hostname = 'www.epartscart.com';
$platformSite = 'www.ecomae.com';
$docroot = '/home/ecomae/htdocs/www.ecomae.com';

echo "=== Fix epartscart client tenant ===\n";

$docCfg = epc_portal_docpart_config();
$ecomaeDbPass = trim((string) ($_GET['db_password'] ?? ''));
if ($ecomaeDbPass === '' && is_file(__DIR__ . '/config.local.php')) {
	$epc_config_local = null;
	require __DIR__ . '/config.local.php';
	if (isset($epc_config_local['password'])) {
		$ecomaeDbPass = (string) $epc_config_local['password'];
	}
}

try {
	$pdo = new PDO(
		'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8',
		'ecomae',
		$ecomaeDbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_portal_db_ensure($pdo);
	$platformHosts = array('www.ecomae.com', 'ecomae.com', 'cp.ecomae.com');
	foreach ($platformHosts as $ph) {
		epc_portal_save_site_settings($pdo, epc_portal_default_site_settings($ph));
	}
	echo "Reset ecomae platform site settings (marketing, not auto parts)\n";
	$save = epc_portal_save_tenant($pdo, array(
		'site_key' => 'epartscart',
		'hostname' => $hostname,
		'industry_code' => 'auto_parts',
		'status' => 'live',
		'trade_name' => 'eParts Cart',
		'hub_name' => 'Electronic World Group',
		'from_email' => 'partsdoc2025@gmail.com',
		'db_name' => $docCfg->db,
		'db_user' => $docCfg->user,
		'db_password' => $docCfg->password,
		'notes' => 'Fixed via ecomae-fix-epartscart.php',
	));
	echo 'Tenant registry: ' . ($save['message'] ?? '') . "\n";
} catch (Exception $e) {
	echo 'Tenant DB fix failed: ' . $e->getMessage() . "\n";
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

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
$bare = preg_replace('/^www\./', '', $hostname);
foreach (array_unique(array($hostname, $bare)) as $aliasHost) {
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
echo 'Remove standalone site: ' . implode(' ', $del['log']) . "\n";

exec('chmod -R o+rX ' . escapeshellarg($docroot) . ' 2>&1', $chmodOut, $chmodCode);
echo "chmod o+rX code={$chmodCode}\n";

$ssl = epc_clp_web_install_ssl($cookie, $platformSite);
echo 'SSL ecomae: ' . implode(' | ', array_slice($ssl['log'], 0, 2)) . "\n";

$token = epc_deploy_token();
@file_get_contents('https://www.ecomae.com/ecomae-super-cp-setup.php?token=' . urlencode($token), false, stream_context_create(array(
	'http' => array('timeout' => 120),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));

function ecp_probe(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$flat = $body !== false ? substr(preg_replace('/\s+/', ' ', strip_tags((string) $body)), 0, 120) : '';
	return "HTTP {$code} — {$flat}";
}

echo "\n=== Probes ===\n";
echo "ecomae /        → " . ecp_probe('https://www.ecomae.com/') . "\n";
echo "epartscart /    → " . ecp_probe('https://www.epartscart.com/') . "\n";
echo "epartscart /cp/ → " . ecp_probe('https://www.epartscart.com/cp/') . "\n";
echo "super cp        → " . ecp_probe('https://cp.ecomae.com/cp/') . "\n";
