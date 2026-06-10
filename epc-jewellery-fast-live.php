<?php
/**
 * Fast jewellery go-live: Model C vhost + platform origin cert (no LE wait).
 * https://www.ecomae.com/epc-jewellery-fast-live.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(60);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$apply = !empty($_GET['apply']);
$www = 'www.thejewellerytrend.com';
$bare = 'thejewellerytrend.com';
$hosts = array($www, $bare);
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$platformCert = 'www.ecomae.com';
$ip = '31.97.216.247';

function epc_jfl_probe(string $url, string $hostHeader = ''): string
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 12, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	@file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	return $code > 0 ? "HTTP {$code}" : 'TIMEOUT';
}

function epc_jfl_sni(string $host, string $ip): string
{
	$r = epc_clp_run_cmd('echo | openssl s_client -connect ' . escapeshellarg($ip . ':443')
		. ' -servername ' . escapeshellarg($host)
		. ' 2>/dev/null | openssl x509 -noout -subject 2>/dev/null');
	$out = trim((string) ($r['output'] ?? ''));
	return $out !== '' ? $out : '(none)';
}

echo "=== jewellery fast live ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";
echo "BEFORE origin: " . epc_jfl_probe('http://127.0.0.1/', $www) . "\n";
echo "BEFORE SNI: " . epc_jfl_sni($www, $ip) . "\n";
echo "BEFORE public /en/: " . epc_jfl_probe('https://' . $www . '/en/') . "\n\n";

if (!$apply) {
	echo "Dry run. apply=1&clp_pass=...\n";
	exit;
}
if ($clpPass === '') {
	exit("apply=1 requires clp_pass=\n");
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login: OK\n";

$dash = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
foreach ($hosts as $orphan) {
	if (!epc_clp_web_site_listed($dash, $orphan)) {
		continue;
	}
	echo "Removing orphan CLP site {$orphan}...\n";
	$del = epc_clp_web_delete_site($cookie, $orphan);
	echo '  delete: ' . implode(' | ', array_slice($del['log'], 0, 3)) . "\n";
	$dash = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
}
$q = epc_clp_nginx_quarantine_orphan_configs($hosts);
foreach ($q['log'] as $line) {
	echo 'nginx: ' . $line . "\n";
}

$vf = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf['vhost'] === '' || $vf['token'] === '') {
	exit("vhost fetch failed\n");
}
$vhost = $vf['vhost'];
$removedReject = 0;
$vhost = epc_clp_vhost_strip_ssl_reject_for_hosts($vhost, $hosts, $removedReject);
$removed444 = 0;
$removed3000 = 0;
$vhost = epc_clp_vhost_strip_tenant_standalone_blocks($vhost, $hosts, $removed444, $removed3000);
echo "ssl_reject removed={$removedReject} return444={$removed444} proxy3000={$removed3000}\n";

$allTenantGroups = array(
	array('key' => 'thejewellerytrend', 'hosts' => $hosts),
	array('key' => 'epartscart', 'hosts' => array('www.epartscart.com', 'epartscart.com')),
	array('key' => 'taxofinca', 'hosts' => array('www.taxofinca.com', 'taxofinca.com')),
	array('key' => 'electronicae', 'hosts' => array('www.electronicae.com', 'electronicae.com')),
	array('key' => 'stylenlook', 'hosts' => array('www.stylenlook.com', 'stylenlook.com')),
);
$vh = epc_clp_vhost_configure_model_c_tenants($cookie, $platformSite, $allTenantGroups);
foreach (array_slice($vh['log'], 0, 8) as $line) {
	echo 'modelc: ' . $line . "\n";
}

$vf2 = epc_clp_vhost_fetch($cookie, $platformSite);
$vhost = $vf2['vhost'] !== '' ? $vf2['vhost'] : $vhost;
foreach ($allTenantGroups as $g) {
	$th = $g['hosts'] ?? array();
	$patch = epc_clp_vhost_patch_server_ssl_for_hosts($vhost, $th, $platformCert, true);
	foreach ($patch['log'] as $line) {
		echo 'ssl: ' . $line . "\n";
	}
	$vhost = $patch['vhost'];
}
$rootLine = '  root ' . rtrim($platformDocroot, '/') . ';';
$vhost = (string) preg_replace_callback(
	'/# EPC_TENANT_DIRECT_START[\s\S]*?# EPC_TENANT_DIRECT_END/',
	function (array $m) use ($rootLine) {
		$block = (string) preg_replace('/\{\{root\}\}/', $rootLine, $m[0]);
		return (string) preg_replace('/^\s*root\s+[^;]+;/m', $rootLine, $block);
	},
	$vhost
);
$splash = epc_clp_vhost_patch_failover_splash($vhost, $platformDocroot, $hosts);
foreach ($splash['log'] as $line) {
	echo 'splash: ' . $line . "\n";
}
$vhost = $splash['vhost'];

$vfJew = epc_clp_vhost_fetch($cookie, $www);
if ($vfJew['vhost'] !== '' && $vfJew['token'] !== '') {
	$jvHost = epc_clp_vhost_patch_server_ssl_for_hosts($vfJew['vhost'], $hosts, $platformCert, true)['vhost'];
	$jvHost = epc_clp_vhost_patch_failover_splash($jvHost, $platformDocroot, $hosts)['vhost'];
	$jvHost = epc_clp_vhost_patch_tenant_direct_root($jvHost, $platformDocroot);
	if (epc_clp_vhost_save($cookie, $www, $jvHost, $vfJew['token'])) {
		echo "dedicated vhost saved ({$www})\n";
	}
}

if (!epc_clp_vhost_save($cookie, $platformSite, $vhost, $vf2['token'] ?: $vf['token'])) {
	exit("vhost saved failed\n");
}
echo "vhost saved (roots + splash patched)\n";

$touch = epc_clp_web_set_site_docroot($cookie, $platformSite, $platformDocroot);
foreach (array_slice($touch['log'], 0, 4) as $line) {
	echo 'docroot-touch: ' . $line . "\n";
}
$vf3 = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf3['vhost'] !== '' && $vf3['token'] !== '') {
	epc_clp_vhost_save($cookie, $platformSite, $vhost, $vf3['token']);
	echo "vhost re-saved (trigger CLP nginx write)\n";
}

$reload = epc_clp_nginx_reload();
foreach ($reload['log'] as $line) {
	echo 'reload: ' . $line . "\n";
}
if (!$reload['ok']) {
	$sudoPass = trim((string) (getenv('EPC_SUDO_PASS') ?: ''));
	if ($sudoPass !== '') {
		$reload2 = epc_clp_nginx_reload_with_pass($sudoPass);
		foreach ($reload2['log'] as $line) {
			echo 'reload-sudo: ' . $line . "\n";
		}
	} else {
		echo "reload: CloudPanel Save required (Sites → www.ecomae.com → Save) or set EPC_SUDO_PASS on server\n";
	}
}

echo "\nAFTER origin: " . epc_jfl_probe('http://127.0.0.1/', $www) . "\n";
echo "AFTER SNI: " . epc_jfl_sni($www, $ip) . "\n";
echo "AFTER public /en/: " . epc_jfl_probe('https://' . $www . '/en/') . "\n";
echo "Done.\n";
