<?php
/**
 * One-shot Model C nginx repair: per-tenant server {} blocks on www.ecomae.com vhost.
 * https://www.ecomae.com/epc-nginx-tenant-vhost-repair.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(180);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));
$apply = !empty($_GET['apply']);
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';

$tenants = array(
	'thejewellerytrend' => array('www' => 'www.thejewellerytrend.com', 'bare' => 'thejewellerytrend.com'),
	'epartscart' => array('www' => 'www.epartscart.com', 'bare' => 'epartscart.com'),
	'taxofinca' => array('www' => 'www.taxofinca.com', 'bare' => 'taxofinca.com'),
	'electronicae' => array('www' => 'www.electronicae.com', 'bare' => 'electronicae.com'),
	'stylenlook' => array('www' => 'www.stylenlook.com', 'bare' => 'stylenlook.com'),
);

function epc_ntvr_probe(string $url, string $hostHeader = ''): string
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
	$hint = '';
	if (is_string($body) && stripos($body, 'No DB connect') !== false) {
		$hint = ' [no-db]';
	}
	return "HTTP {$code}{$hint}";
}

echo "=== EPC nginx tenant vhost repair ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'platform_site=' . $platformSite . "\n\n";

if (!$apply) {
	echo "Dry run. Add apply=1&clp_pass=... to repair vhost and probe tenants.\n";
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

$allAliases = array();
$tenantGroups = array();
foreach ($tenants as $key => $t) {
	$tenantGroups[] = array('key' => $key, 'hosts' => array($t['www'], $t['bare']));
	$allAliases[] = $t['www'];
	$allAliases[] = $t['bare'];
}
$allAliases = array_values(array_unique($allAliases));

$vf = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf['vhost'] !== '' && $vf['token'] !== '') {
	$scrubbed = epc_clp_vhost_scrub_tenant_misroutes($vf['vhost'], $allAliases);
	$removedReject = 0;
	$scrubbed = epc_clp_vhost_strip_ssl_reject_for_hosts($scrubbed, $allAliases, $removedReject);
	$removed444 = 0;
	$removed3000 = 0;
	$scrubbed = epc_clp_vhost_strip_tenant_standalone_blocks($scrubbed, $allAliases, $removed444, $removed3000);
	echo "ssl_reject_handshake removed: {$removedReject}\n";
	echo "return 444 blocks removed: {$removed444}\n";
	echo "proxy :3000 blocks removed: {$removed3000}\n";
	if ($scrubbed !== $vf['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $scrubbed, $vf['token']);
		echo "Pre-scrub saved\n";
	}
}

echo "\n=== Model C per-tenant server blocks ===\n";
$vh = epc_clp_vhost_configure_model_c_tenants($cookie, $platformSite, $tenantGroups);
foreach ($vh['log'] as $line) {
	echo '  ' . $line . "\n";
}
if (empty($vh['ok'])) {
	exit("\nconfigure_model_c_tenants failed\n");
}

$vf2 = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf2['vhost'] !== '' && $vf2['token'] !== '') {
	$patched = epc_clp_vhost_patch_tenant_direct_root($vf2['vhost'], $platformDocroot);
	if ($patched !== $vf2['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $patched, $vf2['token']);
		echo "Patched tenant direct root → {$platformDocroot}\n";
		$vf2['vhost'] = $patched;
	}
}
// Ensure every tenant direct block has a real docroot (not only the first {{root}}).
if ($vf2['vhost'] !== '' && $vf2['token'] !== '' && substr_count($vf2['vhost'], '{{root}}') > 0) {
	$rootLine = '  root ' . rtrim($platformDocroot, '/') . ';';
	$allRoots = (string) preg_replace_callback(
		'/# EPC_TENANT_DIRECT_START[\s\S]*?# EPC_TENANT_DIRECT_END/',
		function (array $m) use ($rootLine) {
			$block = (string) preg_replace('/\{\{root\}\}/', $rootLine, $m[0]);
			return (string) preg_replace('/^\s*root\s+[^;]+;/m', $rootLine, $block);
		},
		$vf2['vhost']
	);
	if ($allRoots !== $vf2['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $allRoots, $vf2['token']);
		echo "Patched all tenant {{root}} placeholders\n";
	}
}

echo "\n=== Quarantine orphan per-tenant nginx files ===\n";
$quarantine = epc_clp_nginx_quarantine_orphan_configs($allAliases, $platformSite);
foreach ($quarantine['log'] as $line) {
	echo '  ' . $line . "\n";
}

if (empty($_GET['skip_ssl'])) {
	$tenantSslRows = array();
	foreach ($tenants as $t) {
		$tenantSslRows[] = array('www' => $t['www'], 'bare' => $t['bare']);
	}
	$perTenantSsl = epc_clp_vhost_install_per_tenant_ssl($cookie, $platformSite, $platformDocroot, $tenantSslRows);
	foreach ($perTenantSsl['log'] as $line) {
		echo 'SSL: ' . $line . "\n";
	}
} else {
	echo "SSL skipped (skip_ssl=1)\n";
}

echo "\n=== nginx reload ===\n";
$reload = epc_clp_nginx_reload_with_pass($clpPass);
foreach ($reload['log'] as $line) {
	echo $line . "\n";
}
if (!$reload['ok']) {
	foreach (epc_clp_nginx_reload()['log'] as $line) {
		echo 'fallback: ' . $line . "\n";
	}
}

$vfAudit = epc_clp_vhost_fetch($cookie, $platformSite);
echo "\n=== server_name audit ===\n";
foreach (epc_clp_vhost_audit_server_names($vfAudit['vhost']) as $snLine) {
	echo '  ' . $snLine . "\n";
}

echo "\n=== Probes ===\n";
echo "platform www.ecomae.com /: " . epc_ntvr_probe('http://127.0.0.1/', 'www.ecomae.com') . "\n";
echo "platform cp.ecomae.com /cp/: " . epc_ntvr_probe('http://127.0.0.1/cp/', 'cp.ecomae.com') . "\n";
foreach ($tenants as $key => $t) {
	echo "[{$key}]\n";
	echo '  origin /: ' . epc_ntvr_probe('http://127.0.0.1/', $t['www']) . "\n";
	echo '  origin /cp/: ' . epc_ntvr_probe('http://127.0.0.1/cp/', $t['www']) . "\n";
	echo '  public /: ' . epc_ntvr_probe('https://' . $t['www'] . '/') . "\n";
	echo '  public /cp/: ' . epc_ntvr_probe('https://' . $t['www'] . '/cp/') . "\n";
}
echo "\nRepair complete.\n";
