<?php
/**
 * thejewellerytrend go-live: dedicated CLP site (LE + nginx) + Model C on www.ecomae.com.
 * https://www.ecomae.com/epc-thejewellerytrend-nginx-fix.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(300);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));
$apply = !empty($_GET['apply']);
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$wwwHost = 'www.thejewellerytrend.com';
$bareHost = 'thejewellerytrend.com';
$tenantHosts = array($wwwHost, $bareHost);
$deprecatedHosts = array('www.thethejewellerytrend.com', 'thethejewellerytrend.com');
$platformIp = '31.97.216.247';
$siteUser = 'thejewellerytrend';
$siteUserPass = trim((string) ($_GET['site_user_password'] ?? getenv('EPC_SITE_USER_PASSWORD') ?: 'EpcJewellery2026!'));

$allTenantGroups = array(
	array('key' => 'thejewellerytrend', 'hosts' => array($wwwHost, $bareHost)),
	array('key' => 'epartscart', 'hosts' => array('www.epartscart.com', 'epartscart.com')),
	array('key' => 'taxofinca', 'hosts' => array('www.taxofinca.com', 'taxofinca.com')),
	array('key' => 'electronicae', 'hosts' => array('www.electronicae.com', 'electronicae.com')),
	array('key' => 'stylenlook', 'hosts' => array('www.stylenlook.com', 'stylenlook.com')),
);

function epc_tjt_probe(string $url, string $hostHeader = ''): string
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	if ($body === false && $code === 0) {
		return 'TIMEOUT';
	}
	$hint = (is_string($body) && stripos($body, 'No DB connect') !== false) ? ' [no-db]' : '';
	return "HTTP {$code}{$hint}";
}

function epc_tjt_origin_cert(string $sniHost, string $ip): string
{
	$cmd = 'echo | openssl s_client -connect ' . escapeshellarg($ip . ':443')
		. ' -servername ' . escapeshellarg($sniHost)
		. ' 2>/dev/null | openssl x509 -noout -subject 2>/dev/null';
	$r = epc_clp_run_cmd($cmd);
	$out = trim((string) ($r['output'] ?? ''));
	return $out !== '' ? $out : '(none)';
}

echo "=== thejewellerytrend nginx + SSL fix ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

echo "=== orphan nginx configs (would quarantine) ===\n";
foreach (epc_clp_nginx_find_configs_for_hosts(array_merge($tenantHosts, $deprecatedHosts), $platformSite) as $conf) {
	echo '  ' . $conf . "\n";
}
echo "\n=== BEFORE ===\n";
echo '  origin /: ' . epc_tjt_probe('http://127.0.0.1/', $wwwHost) . "\n";
echo '  public /: ' . epc_tjt_probe('https://' . $wwwHost . '/') . "\n";
echo '  SNI: ' . epc_tjt_origin_cert($wwwHost, $platformIp) . "\n";
echo '  control taxofinca: ' . epc_tjt_probe('http://127.0.0.1/', 'www.taxofinca.com') . "\n\n";

if (!$apply) {
	echo "Dry run. Add apply=1&clp_pass=... to create CLP site, LE cert, Model C blocks.\n";
	exit;
}
if ($clpPass === '') {
	exit("apply=1 requires clp_pass=\n");
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login: OK\n\n";

foreach ($deprecatedHosts as $bad) {
	$del = epc_clp_web_delete_site($cookie, $bad);
	echo 'Remove typo site ' . $bad . ': ' . implode(' ', array_slice($del['log'], 0, 2)) . "\n";
}

echo "\n=== Dedicated CloudPanel site (shared docroot) ===\n";
$dash = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
$siteReady = epc_clp_web_site_listed($dash, $wwwHost);
if (!$siteReady) {
	$create = epc_clp_web_create_php_site($cookie, array(
		'domain' => $wwwHost,
		'site_user' => $siteUser,
		'site_user_password' => $siteUserPass,
		'php_version' => '8.3',
	));
	foreach ($create['log'] as $line) {
		echo '  create: ' . $line . "\n";
	}
	$siteReady = !empty($create['ok']);
}
if ($siteReady) {
	$repoint = epc_clp_web_set_site_docroot($cookie, $wwwHost, $platformDocroot);
	echo '  docroot: ' . implode(' | ', array_slice($repoint['log'], 0, 3)) . "\n";
	$ssl = epc_clp_web_install_ssl($cookie, $wwwHost, array($bareHost));
	echo '  LE: ' . implode(' | ', array_slice($ssl['log'], 0, 4)) . "\n";
}
$paths = epc_clp_ssl_certificate_paths($wwwHost);
echo '  cert files: ' . ($paths !== null ? $paths['crt'] : 'MISSING') . "\n";

echo "\n=== Model C on {$platformSite} (all tenants) ===\n";
$vf = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf['vhost'] !== '' && $vf['token'] !== '') {
	$scrubbed = epc_clp_vhost_scrub_tenant_misroutes($vf['vhost'], array_merge($tenantHosts, $deprecatedHosts));
	$removedReject = 0;
	$scrubbed = epc_clp_vhost_strip_ssl_reject_for_hosts($scrubbed, $tenantHosts, $removedReject);
	if ($scrubbed !== $vf['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $scrubbed, $vf['token']);
	}
}
$vh = epc_clp_vhost_configure_model_c_tenants($cookie, $platformSite, $allTenantGroups);
foreach ($vh['log'] as $line) {
	echo '  ' . $line . "\n";
}
$vf2 = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf2['vhost'] !== '' && $vf2['token'] !== '') {
	$patched = epc_clp_vhost_patch_tenant_direct_root($vf2['vhost'], $platformDocroot);
	if ($patched !== $vf2['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $patched, $vf2['token']);
	}
}
$sslRows = array();
foreach ($allTenantGroups as $g) {
	$hosts = $g['hosts'] ?? array();
	$sslRows[] = array('www' => $hosts[0] ?? '', 'bare' => $hosts[1] ?? '');
}
$sslPatch = epc_clp_vhost_install_per_tenant_ssl($cookie, $platformSite, $platformDocroot, $sslRows);
foreach ($sslPatch['log'] as $line) {
	echo '  ssl: ' . $line . "\n";
}

echo "\n=== nginx reload ===\n";
foreach (epc_clp_nginx_reload()['log'] as $line) {
	echo $line . "\n";
}

echo "\n=== server_name audit ({$platformSite}) ===\n";
$vfAudit = epc_clp_vhost_fetch($cookie, $platformSite);
foreach (epc_clp_vhost_audit_server_names($vfAudit['vhost']) as $snLine) {
	echo '  ' . $snLine . "\n";
}

echo "\n=== AFTER ===\n";
echo '  origin /: ' . epc_tjt_probe('http://127.0.0.1/', $wwwHost) . "\n";
echo '  origin /cp/: ' . epc_tjt_probe('http://127.0.0.1/cp/', $wwwHost) . "\n";
echo '  public /: ' . epc_tjt_probe('https://' . $wwwHost . '/') . "\n";
echo '  public /cp/: ' . epc_tjt_probe('https://' . $wwwHost . '/cp/') . "\n";
echo '  SNI: ' . epc_tjt_origin_cert($wwwHost, $platformIp) . "\n";
echo "\nDone.\n";
